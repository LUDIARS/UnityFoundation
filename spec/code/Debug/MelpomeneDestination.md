# Melpomene 送信先の選択 (Submit Destination)

ランタイム Melpomene のバグ報告送信先を**選択式**にする。従来は GitHub Issues API
へ直接送信(PAT 同梱)していたが、配布ビルドにトークンを載せないため、**任意の
リレーサーバ**(AWS Lambda でも自前サーバでも何でも)へ送れるようにする。

## 送信モード

`MelpomeneRuntimeConfig.submitMode`:

| モード | 説明 | 必須設定 |
|--------|------|----------|
| `GitHubDirect` | GitHub Issues API へ直接 POST(PAT 同梱)。プレイテスト限定。 | repositoryOwner / repositoryName / accessToken |
| `Relay` | 任意のリレーサーバへ POST。トークンはサーバ側に置きクライアントへ載せない。 | relayUrl |

`Relay` は URL が任意なので **AWS に限定されない**。`relayUrl` を自前サーバや
`foundation-debug-server` の `/api/melpomene/report` に向ければよい。

## クライアント設計 (Unity)

- `IMelpomeneSubmitTarget` — `UniTask<MelpomeneSubmitResult> SubmitAsync(ticket)` + `DisplayName`。
- `MelpomeneGitHubTarget` — GitHub 直送(従来 `MelpomeneIssueClient` 相当)。
- `MelpomeneRelayTarget` — `relayUrl` へリレー要求 JSON を POST。`relayAuthHeader` が
  非空なら `Authorization` ヘッダに載せる。
- `MelpomeneSubmitTargetFactory.Create(config)` — `submitMode` から実装を選ぶ(純ロジック→単体テスト対象)。

`MelpomeneRuntimeConfig.IsValid` はモード依存にする(GitHubDirect=owner/repo/token、Relay=relayUrl)。

## リレー契約 (クライアント ⟷ サーバ共有)

### リクエスト `POST {relayUrl}`

```jsonc
{
  "title": "[Melpomene] ...",
  "body": "...(markdown)...",
  "labels": ["melpomene", "in-game-report", "high", "bug"],
  "category": "Bug",
  "priority": "High",
  "userName": "...",
  "sceneName": "...",
  "platform": "...",
  "appVersion": "...",
  "screenInfo": "...",
  "timestamp": "...",
  "source": "melpomene-runtime",
  "clientVersion": "1.0.0-runtime"
}
```

`relayAuthHeader` 設定時は HTTP ヘッダ `Authorization: <relayAuthHeader>` を付与する。

### レスポンス(正規化)

```jsonc
{ "success": true, "issueNumber": 123, "issueUrl": "https://...", "error": null }
```

HTTP 2xx かつ `success:true` で成功。それ以外は失敗として `error` を表示する。

## リファレンス・リレー (foundation-debug-server)

`server/` に `POST /api/melpomene/report` を追加する。GitHub トークンは**サーバ側 env**
で保持し、クライアントには載せない。

| env | 説明 |
|-----|------|
| `MELPOMENE_GITHUB_TOKEN` | Issues 書込権限のトークン |
| `MELPOMENE_REPO` | `owner/repo` |
| `MELPOMENE_RELAY_AUTH` | 任意。設定時、`Authorization` ヘッダ一致を要求 |

未設定時は `503 { success:false, error:"relay_not_configured" }`。これは「AWS 以外の
サーバへ送る」具体例の 1 つで、`relayUrl` を別サーバに向ければ差し替え可能。
