# Melpomene Runtime（ゲーム内バグ報告）

ビルド後のゲーム（プレイヤービルド）でも動作する、ゲーム内バグ報告機能。
入力内容をその場で **GitHub Issue** として作成する。

Editor 専用の [Melpomene](./Melpomene.md)（シーンビューから Issue を作るデバッグツール）とは別物で、
こちらは **ランタイム**で動く最小スコープ版（バグ報告フォームのみ）。両者は併存できる。

## 構成

`unity/Assets/Scripts/BaseSystem/Foundation/Melpomene/`

| ファイル | 役割 |
|----------|------|
| `MelpomeneRuntimeConfig.cs` | GitHub 接続設定（ScriptableObject、Resources からロード） |
| `MelpomeneReportTicket.cs` | 報告チケット（Issue タイトル/本文を生成） |
| `MelpomeneIssueClient.cs` | GitHub Issues API クライアント（`UnityWebRequest` + UniTask、ランタイム動作） |
| `MelpomeneReporter.cs` | ゲーム内 UI（IMGUI オーバーレイ）。入力→送信 |
| `MelpomeneRuntimeBootstrap.cs` | 起動時に常駐レポーターを自動生成 |
| `Assets/Resources/MelpomeneRuntimeConfig.asset` | 設定アセット（トークンは空。利用者が記入） |

UI は **IMGUI（`OnGUI`）** で実装。Canvas / EventSystem / フォント / Addressables 等の追加配線なしで、
ビルド後プレイヤー上でも確実に表示・入力できる。

## なぜ Editor 版はそのままビルドで動かないか

Editor 版 Melpomene（`unity/Assets/Musa/Melpomene/`）は全ファイルが `#if UNITY_EDITOR` で囲まれ、
`EditorWindow` / `EditorGUILayout` / `SceneView` / `AssetDatabase` 等の Editor 専用 API に依存しているため、
プレイヤービルドには含まれず動作しない。ランタイム版は GitHub Issue 作成ロジック（`UnityWebRequest`+UniTask）だけを
ランタイム安全な形で取り出し、IMGUI の入力フォームを新規実装したもの。

## セットアップ

1. `Assets/Resources/MelpomeneRuntimeConfig.asset` を Inspector で開く。
2. `Repository Owner` / `Repository Name` を対象リポジトリに設定。
3. `Access Token` に **Issues 書き込み権限**を持つ GitHub Personal Access Token を設定。
4. 必要なら `Toggle Key`（Input System の Key）で開閉キーを指定（既定は画面右上の `🐞 Report` ボタンで開閉）。

### 可用性ゲート

`MelpomeneRuntimeBootstrap` は以下のいずれかでのみレポーターを有効化する:

- Unity エディタ上（`Application.isEditor`）
- 開発ビルド（`Development Build` = `Debug.isDebugBuild`）
- 設定 `Enable In Release Build = true`

リリースビルドでは既定で無効。

## セキュリティ上の注意

> ⚠ Access Token をビルドに同梱すると、配布物からトークンを抽出され得る。
> **プレイテスト用のクローズドな配布に限定**し、配布ビルドには含めないこと。
> 公開配布で利用する場合は、GitHub 直叩きではなくサーバ（例: AWS API Gateway 経由）に
> 中継させる方式へ切り替えること（本リポの AWS イベントログ送信と同様の構成）。

`MelpomeneRuntimeConfig.asset` はトークン空でコミットしている。実トークンはコミットしないこと。

## レビューダイアログのスキップ（2回目以降）

`GameEvent/GameEventRecorder.cs` に、レビューダイアログをリピートプレイヤーへ出さない制御を追加した。

- `PlayerPrefs` にプレイ回数 (`Melpomene_PlayCount`) とレビュー済みフラグ (`Melpomene_Reviewed`) を永続化。
- `GameStart()` でプレイ回数を加算。
- `ShouldShowReview()` … 初回プレイ（`PlayCount <= ReviewMaxPlayCount`、既定 1）かつ未レビューのときのみ true。
- `GameReview()` … スキップ条件のときはダイアログを出さずコールバックを即実行して続行。
- レビュー送信成功時（`SendReview`）にレビュー済みフラグを立て、以降は出さない。

しきい値は `GameEventRecorder.ReviewMaxPlayCount` で調整可能。

## 検証メモ

- 当変更は静的実装まで。Unity 実機でのコンパイル/動作確認（ビルドして `🐞 Report`→Issue 作成、
  2回目プレイでダイアログ非表示）は Unity エディタ側での確認が必要。
