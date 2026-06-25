# Remote Debug プロトコル

制御チャネルは WebSocket、映像チャネルは WebRTC。両方とも同じ WS で完結する
(WebRTC のシグナリングも制御 WS に相乗りする)。

## 接続

- WS エンドポイント: `ws://<host>:<port>/ws`
- 接続直後に `hello` を送って役割を宣言する。

```jsonc
// クライアント -> サーバ
{ "v": 1, "type": "hello", "payload": { "role": "unity" | "panel", "name": "..." } }
// サーバ -> クライアント
{ "v": 1, "type": "welcome", "payload": { "sessionId": "...", "unityConnected": true } }
```

## エンベロープ

全メッセージ共通:

```jsonc
{
  "v": 1,            // プロトコルバージョン
  "type": "string",  // メッセージ種別
  "id": "string?",   // 任意。リクエスト/レスポンス対応付け
  "ts": 0,           // 送信側 unix ms
  "payload": {}      // 種別ごとの本体
}
```

## 制御メッセージ

| type | 方向 | payload | 説明 |
|------|------|---------|------|
| `command.list.request` | panel→unity | `{}` | コマンド一覧要求 |
| `command.list` | unity→panel | `{ commands: [{ name, description, kind }] }` | `kind`: `"action"` \| `"toggle"` |
| `command.invoke` | panel→unity | `{ name, value? }` | コマンド実行 (toggle は `value:boolean`) |
| `command.result` | unity→panel | `{ name, ok, message? }` | 実行結果 |
| `scene.load` | panel→unity | `{ scene }` | シーン遷移 |
| `blackboard.set` | panel→unity | `{ key, value }` | ブラックボード値書き込み |
| `master.reload` | panel→unity | `{ sheet }` | 指定シートを再取得させる (Data Studio 更新の反映) |
| `telemetry` | unity→panel | `{ fps, scene, memoryMB, time }` | 定期送信 (既定 1s) |
| `log` | unity→panel | `{ level, message }` | Unity ログ転送 (`level`: log/warning/error) |

サーバはパネル↔Unity 間を**透過中継**する (role でルーティング)。Unity 不在時、
panel→unity メッセージはサーバがバッファせず `error` (`unity_not_connected`) を返す。

## WebRTC シグナリング (映像)

Unity が producer (映像送出)、panel が consumer。サーバが SFU として中継する。

| type | 方向 | payload |
|------|------|---------|
| `rtc.offer` | producer/consumer→server | `{ sdp }` |
| `rtc.answer` | server→producer/consumer | `{ sdp }` |
| `rtc.ice` | both | `{ candidate }` |

- Unity 接続時: Unity が `rtc.offer` を送る → サーバが受信用 PC を張り `rtc.answer`。
  Unity のトラックをサーバが保持する。
- panel が映像を要求: panel が `rtc.offer` → サーバが送信用 PC で Unity トラック
  (無ければ合成テスト映像) を載せて `rtc.answer`。

## エラー

```jsonc
{ "v": 1, "type": "error", "id": "<対応 id>", "payload": { "code": "string", "message": "string" } }
```

`code`: `unity_not_connected` | `bad_message` | `unknown_type` | `internal`。
