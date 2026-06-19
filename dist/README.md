# UnityFoundation.unitypackage

ゲーム内バグ報告(Melpomene ランタイム)+ レビューダイアログ + AWS イベントログを
**1 つの `.unitypackage`** にまとめたもの。Import 時に**トップフォルダ単位で選択インストール**できる。

生成: リポジトリルートで `node tools/build-unitypackage.mjs` → `dist/UnityFoundation.unitypackage`

## 取り込み方

1. Unity で `Assets > Import Package > Custom Package...` から `UnityFoundation.unitypackage` を選択。
2. Import ダイアログのツリーで**要らないフォルダのチェックを外す**と、その機能だけ入れられる。

## 同梱フォルダ(選択単位)

| フォルダ | 内容 | 主な依存(同梱内) |
|----------|------|------------------|
| `Assets/UnityFoundation/Melpomene/` | ゲーム内バグ報告→GitHub Issue(ビルド後も動作) | Common(任意) |
| `Assets/UnityFoundation/GameEvent/` | レビューダイアログ(2回目スキップ) + AWS イベントログ + Review プレハブ | Network, Common |
| `Assets/UnityFoundation/Network/` | ワーカープール HTTP クライアント | ― |
| `Assets/UnityFoundation/Common/` | `BuildState` / `Utility`(小ヘルパ) | ― |

依存の目安: **GameEvent を入れるなら Network と Common も必要**。Melpomene は単独で動く
(報告者名等に Common は使わない)。

## 外部パッケージ依存(取り込み先プロジェクトに必要)

| パッケージ | 用途 |
|-----------|------|
| UniTask (`com.cysharp.unitask`) | 非同期(Melpomene / Network / GameEvent) |
| Input System (`com.unity.inputsystem`) | Melpomene 開閉キー / Review の入力制御 |
| Addressables (`com.unity.addressables`) | Review プレハブのロード(GameEvent) |
| TextMeshPro (`com.unity.ugui` 同梱) | Review UI |

## セットアップ補足

- **Melpomene**: `Assets/UnityFoundation/Melpomene/Resources/MelpomeneRuntimeConfig.asset` に
  リポジトリと GitHub トークンを設定(トークン同梱はプレイテスト限定)。詳細は
  `spec/code/Debug/MelpomeneRuntime.md`。
- **Review プレハブ(Addressables)**: `Assets/UnityFoundation/GameEvent/Review/Review.prefab` を
  Addressable に登録し、アドレスを `Assets/UnityFoundation/GameEvent/Review/Review.prefab` にする
  (`ReviewWindow.Build` がこのアドレスでロードする)。

## 検証メモ

このパッケージは Unity 無しのスクリプト(`tools/build-unitypackage.mjs`)で生成しており、
アーカイブ構造(GUID 別 `pathname`/`asset`/`asset.meta`)は検証済みだが、
**実際の Unity への Import 動作確認は未実施**。取り込み先での確認を推奨。
