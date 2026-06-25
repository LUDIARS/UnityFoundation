// RemoteVideoSender — WebRTC 映像送出 (Unity = producer)。
//
// この機能は opt-in。Player Settings の Scripting Define Symbols に
//   FOUNDATION_REMOTE_WEBRTC
// を追加し、かつ com.unity.webrtc パッケージが入っているときだけコンパイルされる。
// 定義が無い環境 (= 既定) では空ファイル扱いになり、WS 制御パスは webrtc 不在でも動く。
//
// さらに通常のデバッグガード (#if UNITY_EDITOR || DEVELOPMENT_BUILD) も併用する。
//
// NOTE: この環境では com.unity.webrtc をコンパイル/検証できないため、API 呼び出しには
//       TODO(verify) を付す。Unity.WebRTC 3.0.0-pre.* の API を前提に記述している。
#if FOUNDATION_REMOTE_WEBRTC && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using System;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;

// NOTE: namespaceはつけないこと (Feature層規約)。

/// <summary>
/// Camera/RenderTexture を WebRTC で送出する producer。
/// protocol.md のシグナリング:
///   - Unity が rtc.offer を送る -> サーバが rtc.answer を返す
///   - 双方向に rtc.ice を交換する
/// </summary>
public sealed class RemoteVideoSender : IDisposable
{
    private readonly RemoteDebugClient _client;
    private readonly Camera _captureCamera;
    private readonly MonoBehaviour _coroutineHost; // WebRTC.Update コルーチン駆動用

    private RTCPeerConnection _pc;
    private VideoStreamTrack _videoTrack;
    private bool _started;

    public RemoteVideoSender(RemoteDebugClient client, Camera captureCamera, MonoBehaviour coroutineHost)
    {
        _client = client;
        _captureCamera = captureCamera;
        _coroutineHost = coroutineHost;
    }

    /// <summary>
    /// PeerConnection を張り、カメラトラックを載せて offer を送る。
    /// </summary>
    public void Start()
    {
        if (_started) return;
        _started = true;

        // TODO(verify): WebRTC.Initialize() / 専用 update ループの起動方法は
        //   Unity.WebRTC のバージョンで差がある。3.x では WebRTC.Update() コルーチンを
        //   常駐させるのが一般的。
        _coroutineHost.StartCoroutine(WebRTC.Update());

        var config = GetConfiguration();
        _pc = new RTCPeerConnection(ref config);

        _pc.OnIceCandidate = candidate =>
        {
            // 自分の ICE candidate を相手 (サーバ) へ送る。
            var payload = new RtcIcePayload { candidate = candidate.Candidate };
            _client.Enqueue(RemoteMessage.Build(RemoteMessageType.RtcIce, payload));
        };

        _pc.OnIceConnectionChange = state =>
            Debug.Log($"[Remote] ICE state: {state}");

        // カメラ映像をトラック化して送出方向 (SendOnly) で追加する。
        // TODO(verify): CaptureStreamTrack のシグネチャはバージョン差あり。
        //   3.x では camera.CaptureStreamTrack(width, height) が使える。
        _videoTrack = _captureCamera.CaptureStreamTrack(1280, 720);
        _pc.AddTrack(_videoTrack);

        _coroutineHost.StartCoroutine(CreateAndSendOffer());
    }

    private static RTCConfiguration GetConfiguration()
    {
        var config = default(RTCConfiguration);
        config.iceServers = new[]
        {
            new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
        };
        return config;
    }

    private IEnumerator CreateAndSendOffer()
    {
        var op = _pc.CreateOffer();
        yield return op;
        if (op.IsError)
        {
            Debug.LogWarning($"[Remote] CreateOffer error: {op.Error.message}");
            yield break;
        }

        var desc = op.Desc;
        var setLocal = _pc.SetLocalDescription(ref desc);
        yield return setLocal;
        if (setLocal.IsError)
        {
            Debug.LogWarning($"[Remote] SetLocalDescription error: {setLocal.Error.message}");
            yield break;
        }

        // offer SDP をサーバへ送る。
        var payload = new RtcSdpPayload { sdp = desc.sdp };
        _client.Enqueue(RemoteMessage.Build(RemoteMessageType.RtcOffer, payload));
    }

    /// <summary>
    /// RemoteDebugClient.OnMessage から rtc.answer / rtc.ice を受け取って処理する。
    /// メインスレッドで呼ばれる前提。
    /// </summary>
    /// <returns>このセンダが処理したら true。</returns>
    public bool Handle(string json, RemoteEnvelope header)
    {
        switch (header.type)
        {
            case RemoteMessageType.RtcAnswer:
                HandleAnswer(json);
                return true;

            case RemoteMessageType.RtcIce:
                HandleRemoteIce(json);
                return true;

            default:
                return false;
        }
    }

    private void HandleAnswer(string json)
    {
        if (_pc == null) return;
        var env = RemoteMessage.Parse<RtcSdpPayload>(json);
        var sdp = env?.payload?.sdp;
        if (string.IsNullOrEmpty(sdp)) return;

        var desc = new RTCSessionDescription
        {
            type = RTCSdpType.Answer,
            sdp = sdp,
        };
        _coroutineHost.StartCoroutine(ApplyAnswer(desc));
    }

    private IEnumerator ApplyAnswer(RTCSessionDescription desc)
    {
        var op = _pc.SetRemoteDescription(ref desc);
        yield return op;
        if (op.IsError)
        {
            Debug.LogWarning($"[Remote] SetRemoteDescription error: {op.Error.message}");
        }
    }

    private void HandleRemoteIce(string json)
    {
        if (_pc == null) return;
        var env = RemoteMessage.Parse<RtcIcePayload>(json);
        var candidate = env?.payload?.candidate;
        if (string.IsNullOrEmpty(candidate)) return;

        // TODO(verify): RTCIceCandidateInit のフィールド (sdpMid / sdpMLineIndex) は
        //   protocol.md では candidate 文字列のみ。実運用では mid/index も必要になりうる。
        var init = new RTCIceCandidateInit { candidate = candidate };
        _pc.AddIceCandidate(new RTCIceCandidate(init));
    }

    public void Dispose()
    {
        _started = false;
        try
        {
            _videoTrack?.Dispose();
            _pc?.Close();
            _pc?.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Remote] video sender dispose: {e.Message}");
        }
        _videoTrack = null;
        _pc = null;
    }
}
#endif
