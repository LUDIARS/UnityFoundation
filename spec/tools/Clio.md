# Clio — MUSA アセット自動選択・タグ配置 (Curare 連携)

> ステータス: 初版実装 (2026-07-16)
> 上位設計: Ars-Musa `docs/DESIGN.md` §4-B (Clio — アセット管理・Curare 連携)

---

## 1. 目的と重視点

**目的**: レベルブロックアウトの「キューブ」を、タグに合致するアセット (リソース) へ
自動で置き換える。シーンに対象物をキューブとして置くだけでリソースが自動選択され、
タグを選んでリソースを配置できるようにする。

**重視点**:
- **キューブ = プレースホルダ**。デザイナ/AI はキューブでレイアウトするだけでよい。
- **タグが正**。選択はタグ合致スコアで決定し、名前一致は補助ヒントに留める。
- **ローカル優先**。解決はまず AssetDatabase (AssetLabel) で行い、Curare は
  タグ語彙の供給・横断検索・タグ書き戻しに使う (オフラインでもローカル解決は動く)。

## 2. 採用アーキテクチャ

```
unity/Assets/Musa/Clio/
├── Runtime/
│   ├── Musa.Clio.Runtime.asmdef   # 全プラットフォーム (シーン常駐コンポーネント用)
│   └── ClioPlaceholder.cs         # プレースホルダ MonoBehaviour (tags / fitMode)
├── ClioConfig.cs                  # musa/clio/clio_config.json (+ clio_local.json 秘匿)
├── ClioAsset.cs                   # Curare API DTO
├── ClioClient.cs                  # Curare REST クライアント (UniTask + UnityWebRequest)
├── ClioTagMapper.cs               # AssetLabel ↔ Curare タグ双方向マッピング
├── ClioTagScorer.cs               # タグ合致スコア (純ロジック・Unity 非依存)
├── ClioResourceResolver.cs        # タグ → ローカル prefab 候補解決
├── ClioPlaceholderReplacer.cs     # キューブ → prefab 置換 (Undo/transform 保持)
├── ClioCubeWatcher.cs             # 新規キューブ検出 → 自動選択 (hierarchyChanged)
├── ClioPaletteState.cs            # 選択中タグパレット (SessionState)
└── UI/ClioWindow.cs               # 検索/配置・プレースホルダ・設定 の 3 サブタブ
```

- 既存 `Musa.asmdef` (Editor 専用) に Editor 側を同居させ、シーンに残る
  `ClioPlaceholder` のみ `Musa.Clio.Runtime` (全プラットフォーム) に分離する。
  ビルドに紛れても missing script にならない。
- MusaWindow へ「Clio」メインタブを追加 (Melpomene と同じ埋め込みパターン)。

## 3. 設計判断

| 判断 | 採用 | 理由 |
|---|---|---|
| キューブ検出 | `MeshFilter.sharedMesh.name == "Cube"` の素プリミティブ | 「対象物をキューブにするだけ」の指示に忠実。追加操作ゼロ |
| 自動選択の起点 | `EditorApplication.hierarchyChanged` + delayCall | 置いた瞬間に反応する。置換は delayCall で再入を回避 |
| 置換の既定 | 自動選択 ON / 自動置換 OFF | 誤爆防止。候補提示まで自動、置換はワンクリック (設定で全自動化可) |
| タグ語彙 | Curare `assets.tags` (text[]) が正、AssetLabel はローカル写像 | Ars-Musa DESIGN §4-B の ClioTagMapper 方針どおり |
| Curare API | `GET /api/v1/search?tags=`, `GET/PATCH /api/v1/assets/:id` | 実サーバ実装 (curare-server routes) に合わせる。設計書の `POST /tags` は現行 API に存在しないため PATCH を使用 |
| 認証 | Bearer (Cernere JWT) / dev は `X-User-Id` ヘッダ | curare-server `auth/middleware.ts` の実装どおり |
| 設定 | `musa/clio/clio_config.json` (Git 管理) + `clio_local.json` (秘匿・gitignore) | Melpomene の settings / settings.local 分離と同じ |
| エンドポイント既定 | `http://localhost:8090` | curare-server `src/index.ts` の PORT 既定。Excubitor catalog に Curare 未登録のため設定ファイル正 |

## 4. 動作フロー

### 4-A. キューブ → リソース自動選択
1. ユーザがシーンにキューブを置く (GameObject > 3D Object > Cube)。
2. `ClioCubeWatcher` が新規の素キューブを検出し、`ClioPlaceholder` を付与
   (タグ = 現在のタグパレット選択 + オブジェクト名ヒント)。
3. `ClioResourceResolver` が AssetLabel 経由でローカル prefab 候補をスコアリングし、
   最良候補を `resolvedAssetPath` に記録。
4. 自動置換 ON なら `ClioPlaceholderReplacer` が即置換 (transform/親/順序を保持、Undo 可)。
   OFF ならプレースホルダタブに候補付きで並び、ワンクリック置換。

### 4-B. タグを選んで配置
1. ClioWindow「検索/配置」でタグパレットからタグを選択 (Curare タグ + ローカル label の合算語彙)。
2. [キューブ配置] → 選択タグ入りプレースホルダキューブを SceneView ピボットへ生成
   (以降は 4-A と同じ)。または検索結果の行から [配置] で直接 prefab を配置。
3. [Curare 検索] はタグ/キーワードで Curare を横断検索し、ローカル有無を突き合わせる。

## 5. タスク分解 (実装単位)

- [x] T1: spec 本書 (設計宣言 + タスク分解)
- [x] T2: `Musa.Clio.Runtime.asmdef` + `ClioPlaceholder` (runtime 分離)
- [x] T3: `ClioConfig` (config/local 分離 load/save、既定値生成)
- [x] T4: `ClioAsset` DTO + `ClioClient` (search / get / update tags / health)
- [x] T5: `ClioTagScorer` (純ロジック) + `ClioTagMapper` (label↔tag、ローカル検索)
- [x] T6: `ClioResourceResolver` (候補列挙 + 最良解決)
- [x] T7: `ClioPlaceholderReplacer` (Undo/transform 保持置換、fitMode 3 種)
- [x] T8: `ClioCubeWatcher` (新規キューブ検出 → 自動選択/自動置換)
- [x] T9: `ClioPaletteState` + `UI/ClioWindow` (検索/配置・プレースホルダ・設定)
- [x] T10: MusaWindow へ Clio タブ統合 + メニュー、`Musa.asmdef` 参照追加
- [x] T11: .gitignore (clio_local.json)、spec/tools/Musa.md へ利用手順追記
- [ ] T12: Unity Editor 上での実機確認 (コンパイル + シーン操作) — Unity 起動環境で実施

## 6. 制約・非対象

- Curare にバイナリ取得 (ダウンロード) 経路が未実装のため、**配置できるのは
  ローカルに存在する prefab のみ**。Curare 側にしかないアセットは検索結果に
  「ローカル無し」表示で出す (取得導線は Curare 側の実装後)。
- デスクトップ (Ars-Musa Tauri) 側の追従は本実装の対象外 (DESIGN §6 のとおり必要時に追従)。
