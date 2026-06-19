# Foundation Debug Server

起動中の Unity ゲームを Web から遠隔操作・観察し、Unity が読むマスターデータを
Web 上で作成・配信するデバッグサーバ。

- **制御 WS バス** — ブラウザパネル ⟷ サーバ ⟷ Unity を透過中継 (`/ws`)
- **WebRTC 映像リレー** — Unity の画面を werift (Ergo 系 WebRTC スタック) で SFU 中継
- **Data Studio** — `MasterData` 互換 JSON を Web で作成 (`/master`, `/api/sheets`)
- **Melpomene リレー** — ゲーム内バグ報告を受けて GitHub へ転送 (`/api/melpomene/report`、AWS 以外の送信先の一例)
- **デバッグパネル** — Foundation UI の Web フロント (`web/`)

正本仕様: `../spec/code/Remote/` (RemoteDebug / protocol / data-studio)

## 使い方

```bash
cd server
npm install
npm run dev        # tsx watch で起動 (既定 :8787)
# ブラウザで http://localhost:8787/
```

Unity 側は `RemoteDebugBehaviour` をブートシーンに置き、host/port をサーバに向ける
(`spec/code/Remote/unity-client.md`)。Web 製マスターデータを取り込むには Unity の
`GameSettings.MasterDataAPIURI` を `http://<host>:8787/master` に設定する。

## 環境変数

| 変数 | 既定 | 説明 |
|------|------|------|
| `FOUNDATION_DEBUG_PORT` | `8787` | 待ち受けポート |
| `FOUNDATION_DEBUG_HOST` | `0.0.0.0` | バインドホスト |
| `FOUNDATION_DEBUG_DATA_DIR` | `data/sheets` | シート永続化先 |
| `FOUNDATION_DEBUG_WEB_DIR` | `web` | パネル静的ファイル |
| `FOUNDATION_DEBUG_SYNTHETIC_VIDEO` | `0` | `1` で ffmpeg 合成テスト映像を配信 (Unity 不要の映像確認用、要 ffmpeg) |
| `MELPOMENE_GITHUB_TOKEN` | (なし) | Melpomene リレー用 GitHub Issues 書込トークン。未設定でリレー無効 (503) |
| `MELPOMENE_REPO` | (なし) | `owner/repo`。リレーの転送先リポジトリ |
| `MELPOMENE_RELAY_AUTH` | (なし) | 設定時、リレー要求に `Authorization: <値>` 一致を要求 |

## スクリプト

| コマンド | 内容 |
|----------|------|
| `npm run dev` | tsx watch 起動 |
| `npm start` | tsx 単発起動 |
| `npm run typecheck` | `tsc --noEmit` |
| `npm test` | vitest |
| `npm run build` | `tsc` で `dist/` 出力 |

## エンドポイント

| メソッド | パス | 説明 |
|----------|------|------|
| WS | `/ws` | 制御チャネル + WebRTC シグナリング |
| GET | `/master` | Unity `MasterData` バージョン表 |
| GET | `/master?sheet=X` | シート本体 `{ Version, Data[] }` |
| GET/PUT/DELETE | `/api/sheets[/:name]` | スキーマ CRUD |
| GET/PUT | `/api/sheets/:name/rows` | 行 CRUD |
| POST | `/api/sheets/:name/publish` | version++ + Unity へ reload 通知 |
| POST | `/api/melpomene/report` | Melpomene バグ報告リレー (トークンはサーバ側 env)。`spec/code/Debug/MelpomeneDestination.md` |
| GET | `/` ほか | `web/` 静的配信 |
