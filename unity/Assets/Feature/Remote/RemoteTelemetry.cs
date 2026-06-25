#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

// NOTE: namespaceはつけないこと (Feature層規約)。

/// <summary>
/// 定期テレメトリ送信 + Unity ログ転送。
/// - telemetry{fps, scene, memoryMB, time} を既定 1s 間隔で送る
/// - Application.logMessageReceived を log メッセージとして転送 (error はスロットル)
///
/// 送信は client.Enqueue 経由 (WS の所有はしない)。
/// </summary>
public sealed class RemoteTelemetry : IDisposable
{
    private readonly RemoteDebugClient _client;
    private readonly float _intervalSec;

    // FPS 計測 (移動平均)。
    private float _accumTime;
    private int _accumFrames;
    private float _fps;

    // error ログのスロットル。
    private float _lastErrorSentTime;
    private const float ErrorThrottleSec = 0.5f;

    private bool _running;

    public RemoteTelemetry(RemoteDebugClient client, float intervalSec = 1f)
    {
        _client = client;
        _intervalSec = intervalSec;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        Application.logMessageReceived += OnLogMessage;
        TelemetryLoop().Forget();
    }

    /// <summary>
    /// MonoBehaviour.Update から毎フレーム呼んで FPS を更新する。
    /// </summary>
    public void Tick(float deltaTime)
    {
        _accumTime += deltaTime;
        _accumFrames++;
        if (_accumTime >= 0.5f)
        {
            _fps = _accumFrames / _accumTime;
            _accumTime = 0f;
            _accumFrames = 0;
        }
    }

    private async UniTaskVoid TelemetryLoop()
    {
        while (_running)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_intervalSec), DelayType.Realtime);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_running) break;
            if (!_client.IsConnected) continue;

            SendTelemetry();
        }
    }

    private void SendTelemetry()
    {
        // メモリは Mono ヒープ + GC 確保量の近似 (MB)。
        float memoryMB = (float)(GC.GetTotalMemory(false) / (1024.0 * 1024.0));

        var payload = new TelemetryPayload
        {
            fps = _fps,
            scene = SceneManager.GetActiveScene().name,
            memoryMB = memoryMB,
            time = RemoteMessage.NowUnixMs(),
        };
        _client.Enqueue(RemoteMessage.Build(RemoteMessageType.Telemetry, payload));
    }

    private void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (!_client.IsConnected) return;

        string level;
        switch (type)
        {
            case LogType.Warning:
                level = "warning";
                break;
            case LogType.Error:
            case LogType.Exception:
            case LogType.Assert:
                level = "error";
                // error はスパムになりやすいのでスロットルする。
                if (Time.realtimeSinceStartup - _lastErrorSentTime < ErrorThrottleSec)
                {
                    return;
                }
                _lastErrorSentTime = Time.realtimeSinceStartup;
                break;
            default:
                level = "log";
                break;
        }

        var payload = new LogPayload { level = level, message = condition };
        _client.Enqueue(RemoteMessage.Build(RemoteMessageType.Log, payload));
    }

    public void Dispose()
    {
        _running = false;
        Application.logMessageReceived -= OnLogMessage;
    }
}
#endif
