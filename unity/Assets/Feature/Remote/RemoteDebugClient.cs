#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// NOTE: namespaceはつけないこと (Feature層規約)。

/// <summary>
/// Remote Debug の制御チャネル (WebSocket) を所有するクライアント。
/// - ws://&lt;host&gt;:&lt;port&gt;/ws へ接続し hello{role:"unity"} を送る
/// - 受信ループ (UniTask) を回し、受信した生 JSON をメインスレッドへ marshal して通知
/// - 自動再接続 (指数バックオフ)
/// - 送信は内部キュー経由 (受信ループと送信を分離)
///
/// 解析やコマンド実行はこのクラスでは行わない (SRP)。受信 JSON は OnMessage で外へ流し、
/// RemoteCommandBridge / RemoteSceneBridge 等が解釈する。
/// </summary>
public sealed class RemoteDebugClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _clientName;

    private ClientWebSocket _socket;
    private CancellationTokenSource _cts;

    // 受信した生 JSON をメインスレッドで処理するためのキュー。
    private readonly ConcurrentQueue<string> _inbound = new ConcurrentQueue<string>();
    // 送信待ち JSON。
    private readonly ConcurrentQueue<string> _outbound = new ConcurrentQueue<string>();

    /// <summary>受信メッセージ (生 JSON) をメインスレッドで通知する。</summary>
    public event Action<string> OnMessage;

    /// <summary>接続状態変化通知 (true=接続確立)。メインスレッドで発火。</summary>
    public event Action<bool> OnConnectionChanged;

    public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;

    public RemoteDebugClient(string host, int port, string clientName = "unity")
    {
        _host = host;
        _port = port;
        _clientName = clientName;
    }

    /// <summary>
    /// 接続ループを開始する (再接続を含む)。多重起動はしない。
    /// </summary>
    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        ConnectLoop(_cts.Token).Forget();
    }

    /// <summary>
    /// 送信キューへ積む。実送信は SendLoop が行う。
    /// </summary>
    public void Enqueue(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        _outbound.Enqueue(json);
    }

    /// <summary>
    /// メインスレッドの tick から呼ぶ。受信キューを排出して OnMessage を発火する。
    /// (UnityEngine API を触る bridge をメインスレッドで動かすため)
    /// </summary>
    public void DrainInbound()
    {
        while (_inbound.TryDequeue(out var json))
        {
            try
            {
                OnMessage?.Invoke(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Remote] inbound handler threw: {e}");
            }
        }
    }

    private async UniTask ConnectLoop(CancellationToken token)
    {
        int backoffMs = 500;
        const int maxBackoffMs = 10000;
        var uri = new Uri($"ws://{_host}:{_port}/ws");

        while (!token.IsCancellationRequested)
        {
            try
            {
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(uri, token);

                // 接続直後に hello を送る。
                SendImmediate(RemoteMessage.Build(RemoteMessageType.Hello,
                    new HelloPayload { role = "unity", name = _clientName }), token).Forget();

                await UniTask.SwitchToMainThread();
                OnConnectionChanged?.Invoke(true);

                backoffMs = 500; // 成功したらバックオフをリセット

                // 受信と送信を並行で回し、どちらかが終わるまで待つ。
                var receive = ReceiveLoop(token);
                var send = SendLoop(token);
                await UniTask.WhenAny(receive, send);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Remote] connect/loop error: {e.Message}");
            }
            finally
            {
                CloseSocketQuietly();
                await UniTask.SwitchToMainThread();
                OnConnectionChanged?.Invoke(false);
            }

            if (token.IsCancellationRequested) break;

            // 指数バックオフで再接続。
            try
            {
                await UniTask.Delay(backoffMs, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            backoffMs = Mathf.Min(backoffMs * 2, maxBackoffMs);
        }
    }

    private async UniTask ReceiveLoop(CancellationToken token)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        while (!token.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Remote] receive error: {e.Message}");
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (result.EndOfMessage)
            {
                var json = sb.ToString();
                sb.Clear();
                // メインスレッドの DrainInbound で OnMessage を発火させる。
                _inbound.Enqueue(json);
            }
        }
    }

    private async UniTask SendLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
        {
            if (_outbound.TryDequeue(out var json))
            {
                await SendImmediate(json, token);
            }
            else
            {
                try
                {
                    await UniTask.Delay(16, cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async UniTask SendImmediate(string json, CancellationToken token)
    {
        if (_socket == null || _socket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            await _socket.SendAsync(new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text, true, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Remote] send error: {e.Message}");
        }
    }

    private void CloseSocketQuietly()
    {
        if (_socket == null) return;
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                // 受信ループは既に抜けている前提なので、ベストエフォートで閉じる。
                _socket.Abort();
            }
        }
        catch { /* ignore */ }
        finally
        {
            _socket.Dispose();
            _socket = null;
        }
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { /* ignore */ }
        CloseSocketQuietly();
        _cts?.Dispose();
        _cts = null;
    }
}
#endif
