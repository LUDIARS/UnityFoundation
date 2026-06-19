# Foundation Remote Debug

起動中の Unity ゲームを Web ブラウザから遠隔操作・観察し、さらに Unity が読む
マスターデータを Web 上で抽象的に作成・配信するためのサブシステム。

3 つの構成要素からなる。

| 要素 | 場所 | 言語 | 役割 |
|------|------|------|------|
| Foundation Remote (Unity クライアント) | `unity/Assets/Feature/Remote/` | C# | デバッグサーバへ接続し、コマンド実行・テレメトリ送信・画面 WebRTC 送出 |
| Debug Server | `server/` | TypeScript (Node) | 制御 WS バス + WebRTC 映像リレー + Data Studio + パネル配信。WebRTC は Ergo 系の werift スタックで構築 |
| Debug Panel / Data Studio | `server/web/` | Web (Foundation UI) | ブラウザ UI。映像表示・遠隔操作・マスターデータ編集 |

## データフロー

```
[Browser Panel] <--WS(control)/WebRTC(video)--> [Debug Server] <--WS(control)/WebRTC(video)--> [Unity Game]
                                                       |
                                                       +-- /master?sheet=X  (MasterData API 互換)
                                                       +-- /api/sheets       (Data Studio CRUD)
```

- **遠隔操作**: パネルの操作 → サーバ → Unity の `IDebugCommand` レジストリ / シーン遷移 /
  ブラックボード / マスターデータ hot-push を叩く。Unity はテレメトリ・ログを逆方向に流す。
- **映像**: Unity が RenderTexture を `com.unity.webrtc` で H.264 エンコードしてサーバへ
  送出。サーバ (werift) が SFU としてパネルへ中継する。Unity 未接続時はサーバが合成テスト
  映像を出すため、サーバ単体でも動作確認できる。
- **データ抽象化**: Data Studio で「シート定義 (列スキーマ) + 行データ」を Web で編集する。
  サーバはこれを **既存 `MasterData` が読む JSON 契約**でそのまま配信するため、Unity 側の
  改修なしに `GameSettings.MasterDataAPIURI` をサーバへ向けるだけで Web 製データを取り込める。

## なぜこの形か

- 既存 `MasterData.cs` は `GetRequest(MasterDataAPIURI?sheet=X)` で `{ Version, Data[] }` を
  取得する。サーバがこの契約を満たせば「Web で Unity データを作る」は新規プロトコル不要で成立する。
- 既存 `IDebugCommand` / `DebugPrompt` レジストリがあるため、遠隔操作はこのレジストリへの
  ブリッジに徹すればよく、コマンド体系を二重に持たない。
- WebRTC は LUDIARS 既定の werift (`[[feedback_werift_h264_rtp]]`) を踏襲し、Ergo の WebRTC
  デバッグ配信と同じ流儀でサーバを建てる。

詳細は [[protocol]] / [[data-studio]] / [[unity-client]] と `server/README.md` を参照。
