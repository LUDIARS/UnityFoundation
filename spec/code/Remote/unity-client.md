# Remote Debug — Unity クライアント

`protocol.md` の制御チャネル (WebSocket) + 映像チャネル (WebRTC) を Unity 側で実装する。
コードは `unity/Assets/Feature/Remote/` 配下。Feature 層規約に従い **namespace は付けない**。
全コードは `#if UNITY_EDITOR || DEVELOPMENT_BUILD` ガード下で、リリースビルドには含まれない。

## クラス構成 (SRP で分割)

| ファイル | 役割 |
|----------|------|
| `RemoteMessage.cs` | エンベロープ `{v,type,id,ts,payload}` と各 type の [Serializable] payload DTO。`JsonUtility` は任意 dictionary を扱えないため、型ごとに具象 payload クラスを用意し「2 パスパース」(1 回目で `type` を読み、2 回目で型付き payload を読む)。`RemoteMessage.Build/ParseHeader/Parse<T>` ヘルパ。 |
| `RemoteDebugClient.cs` | `ClientWebSocket` を所有。`ws://<host>:<port>/ws` へ接続し `hello{role:"unity"}` を送る。受信ループ (UniTask)、送受信キュー、指数バックオフ自動再接続。受信生 JSON は `ConcurrentQueue` に積み、`DrainInbound()` (メインスレッド tick) で `OnMessage` を発火 = スレッドマーシャリング。 |
| `RemoteCommandBridge.cs` | `IReadOnlyList<IDebugCommand>` を参照。`command.list.request`→`command.list` (kind は `DebugToggleCommandBase` なら `"toggle"`、他は `"action"`)、`command.invoke`→`Execute()` (toggle は `payload.value` を `IsEnabled` へ)、結果を `command.result` で返す。 |
| `RemoteSceneBridge.cs` | `scene.load`→`SceneLoader.LoadScene(scene)`、`blackboard.set` (TODO: Blackboard は型付き API のみで汎用文字列 setter 無し)、`master.reload` (TODO: per-sheet 再読込 API 無し → `MasterData.Instance.Setup()` で全体再読込)。 |
| `RemoteTelemetry.cs` | 既定 1s で `telemetry{fps,scene,memoryMB,time}` を送信。`Application.logMessageReceived` を `log{level,message}` へ転送 (error はスロットル)。FPS は `Tick(deltaTime)` で移動平均。 |
| `RemoteVideoSender.cs` | `com.unity.webrtc` で Camera を `CaptureStreamTrack` 化し `RTCPeerConnection` で送出 (Unity=producer)。`rtc.offer` 送信→`rtc.answer`/`rtc.ice` 処理。**`FOUNDATION_REMOTE_WEBRTC` 定義時のみコンパイル**。 |
| `RemoteDebugBehaviour.cs` | ドロップイン MonoBehaviour。host/port (既定 `127.0.0.1:8787`)/clientName を serialized field で持ち、client + 各 bridge + telemetry (+ 定義時は video sender) を生成・配線する単一エントリポイント。`SetCommands(prompt.Commands)` で DebugPrompt のコマンドを注入。 |

## スクリプティング定義: `FOUNDATION_REMOTE_WEBRTC`

映像送出は **opt-in**。Player Settings → Scripting Define Symbols に
`FOUNDATION_REMOTE_WEBRTC` を追加し、かつ `com.unity.webrtc` (manifest.json に
`"com.unity.webrtc": "3.0.0-pre.8"` を追加済) がインポートされているときのみ
`RemoteVideoSender` がコンパイルされる。定義が無い既定状態では映像コードは丸ごと
除外され、WebSocket 制御パスは webrtc パッケージ不在でも動作する。

## ドロップイン手順 (`RemoteDebugBehaviour`)

1. ブートストラップシーンの空 GameObject に `RemoteDebugBehaviour` を 1 つ追加。
2. インスペクタで Host / Port (既定 `127.0.0.1:8787`) を設定。
3. デバッグコマンドを公開したい場合は、`DebugPrompt` 構築後に
   `behaviour.SetCommands(prompt.Commands)` を呼ぶ。
4. 映像も送る場合: Scripting Define に `FOUNDATION_REMOTE_WEBRTC` を追加 + capture camera を指定。

リリースビルド (`UNITY_EDITOR` でも `DEVELOPMENT_BUILD` でもない) では全機能が
コンパイル除外され、空 MonoBehaviour として無害に残る。

## 未確認 API (Unity 実機で要検証 — `// TODO(verify):`)

- `blackboard.set`: `Blackboard` は `Register/Subscribe<T>(ReactiveProperty<T>)` の
  強い型付き API のみで、文字列キー→任意値の汎用 setter が無い。専用 `SetByKey` か
  自動生成側 (`BlackboardCodeBuilder`) の動的セッタ追加が必要。現状は受信ログのみ。
- `master.reload`: per-sheet 再読込の public API が無い (`LoadMasterData<T>` は具象型必須、
  `MasterDataLoad()` は private)。最も近いのは `MasterData.Instance.Setup()` (全体再読込)。
- `GameManager.IsDebug`: coding.md はデバッグ機能をこれでゲートする想定だが本リポに未実装。
  暫定で `#if UNITY_EDITOR || DEVELOPMENT_BUILD` のみで制御。
- `Unity.WebRTC` の `CaptureStreamTrack` / `RTCIceCandidateInit` 等のシグネチャは
  3.0.0-pre.* 前提。実パッケージ導入時に要確認。
