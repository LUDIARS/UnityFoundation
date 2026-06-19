using System.Collections.Generic;
using UnityEngine;

// NOTE: namespaceはつけないこと (Feature層規約)。

/// <summary>
/// Remote Debug のドロップイン エントリポイント。
/// ブートストラップシーンにこの MonoBehaviour を 1 つ置くだけで、
/// WS 制御チャネル + コマンド/シーン橋渡し + テレメトリ (+ 任意で映像送出) が起動する。
///
/// デバッグビルドでのみ動作する (#if UNITY_EDITOR || DEVELOPMENT_BUILD)。
/// それ以外のビルドでは中身が全てコンパイル除外され、空の MonoBehaviour になる。
/// </summary>
public sealed class RemoteDebugBehaviour : MonoBehaviour
{
    [Header("接続先 (Remote Debug サーバ)")]
    [SerializeField] private string _host = "127.0.0.1";
    [SerializeField] private int _port = 8787;
    [SerializeField] private string _clientName = "unity";

    [Header("テレメトリ")]
    [SerializeField] private float _telemetryIntervalSec = 1f;

#if FOUNDATION_REMOTE_WEBRTC
    [Header("映像送出 (FOUNDATION_REMOTE_WEBRTC 有効時)")]
    [SerializeField] private Camera _captureCamera;
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private RemoteDebugClient _client;
    private RemoteCommandBridge _commandBridge;
    private RemoteSceneBridge _sceneBridge;
    private RemoteTelemetry _telemetry;
#if FOUNDATION_REMOTE_WEBRTC
    private RemoteVideoSender _videoSender;
#endif

    /// <summary>
    /// 公開コマンド一覧。DebugPrompt を使う場合は外部から
    /// SetCommands(prompt.Commands) を呼んで差し込む。null の場合は空一覧。
    /// </summary>
    private IReadOnlyList<IDebugCommand> _commands = new List<IDebugCommand>();

    /// <summary>
    /// DebugPrompt 等が持つコマンド一覧を注入する (Awake 前/後どちらでも可)。
    /// 既に起動済みなら command bridge を作り直す。
    /// </summary>
    public void SetCommands(IReadOnlyList<IDebugCommand> commands)
    {
        _commands = commands ?? new List<IDebugCommand>();
        if (_client != null)
        {
            _commandBridge = new RemoteCommandBridge(_client, _commands);
        }
    }

    private void Awake()
    {
        // TODO(verify): 仕様 (coding.md) ではデバッグ機能は GameManager.IsDebug で
        //   ゲートする想定だが、本リポに GameManager.IsDebug は未実装。
        //   暫定で #if UNITY_EDITOR || DEVELOPMENT_BUILD のコンパイルガードのみで制御する。
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _client = new RemoteDebugClient(_host, _port, _clientName);
        _commandBridge = new RemoteCommandBridge(_client, _commands);
        _sceneBridge = new RemoteSceneBridge();
        _telemetry = new RemoteTelemetry(_client, _telemetryIntervalSec);

        _client.OnMessage += OnMessage;
        _client.OnConnectionChanged += connected =>
            Debug.Log($"[Remote] connection: {connected}");

#if FOUNDATION_REMOTE_WEBRTC
        var cam = _captureCamera != null ? _captureCamera : Camera.main;
        if (cam != null)
        {
            _videoSender = new RemoteVideoSender(_client, cam, this);
        }
        else
        {
            Debug.LogWarning("[Remote] FOUNDATION_REMOTE_WEBRTC 有効だが capture camera が無い");
        }
#endif

        _client.Start();
        _telemetry.Start();
#if FOUNDATION_REMOTE_WEBRTC
        _videoSender?.Start();
#endif
    }

    private void Update()
    {
        // 受信キューをメインスレッドで排出 (bridge が UnityEngine API を触るため)。
        _client?.DrainInbound();
        _telemetry?.Tick(Time.unscaledDeltaTime);
    }

    private void OnMessage(string json)
    {
        var header = RemoteMessage.ParseHeader(json);
        if (header == null)
        {
            Debug.LogWarning("[Remote] received message with no header");
            return;
        }

        if (_commandBridge != null && _commandBridge.Handle(json, header)) return;
        if (_sceneBridge != null && _sceneBridge.Handle(json, header)) return;
#if FOUNDATION_REMOTE_WEBRTC
        if (_videoSender != null && _videoSender.Handle(json, header)) return;
#endif

        // welcome / error / telemetry(自送) など unity 側で処理不要な type は無視。
    }

    private void OnDestroy()
    {
        _telemetry?.Dispose();
#if FOUNDATION_REMOTE_WEBRTC
        _videoSender?.Dispose();
#endif
        _client?.Dispose();
    }
#endif // UNITY_EDITOR || DEVELOPMENT_BUILD
}
