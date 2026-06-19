# Data Studio (Web データ抽象化層)

Unity の `MasterData` が読むシートデータを、Web 上でスキーマ付きで作成・編集し、
**Unity 改修ゼロ**で配信する層。サーバは `MasterData` の取得契約をそのまま実装する。

## Unity 側の既存契約 (mirror 対象)

`MasterData.cs` より:

- バージョン一覧: `GET {MasterDataAPIURI}` → `MasterDataVersion` JSON
  (`{ TimeStamp: number(ticks), <sheet>: <version:int>, ... }` 相当)
- シート本体: `GET {MasterDataAPIURI}?sheet=<name>` → `SpreadSheetDataObject` 派生 JSON
  (`{ Version: int, Data: [ {...row...}, ... ] }`)

サーバはこれを **`/master`** に実装する:

| エンドポイント | 返却 |
|----------------|------|
| `GET /master` | `{ TimeStamp, "<sheet>": <version>, ... }` |
| `GET /master?sheet=<name>` | `{ Version, Data: [...] }` |

→ Unity は `GameSettings.MasterDataAPIURI` をサーバの `/master` に向けるだけで Web 製
データを取り込める。

## スキーマモデル

```jsonc
// sheet schema
{
  "name": "Enemy",
  "version": 3,
  "key": "Id",                       // 主キー列 (任意)
  "columns": [
    { "key": "Id",   "type": "int" },
    { "key": "Name", "type": "string" },
    { "key": "ResourceName", "type": "string" },
    { "key": "Hp",   "type": "int", "default": 100 }
  ]
}
```

`type`: `int` | `float` | `bool` | `string`。行データは列スキーマで型強制・既定値補完する。
スキーマ・行はサーバの `data/sheets/<name>.json` に永続化する (人手編集・git diff 可)。

## Data Studio REST

| メソッド | パス | 説明 |
|----------|------|------|
| `GET` | `/api/sheets` | スキーマ一覧 |
| `GET` | `/api/sheets/:name` | スキーマ + 行 |
| `PUT` | `/api/sheets/:name` | スキーマ upsert (version++ は明示) |
| `DELETE` | `/api/sheets/:name` | 削除 |
| `GET` | `/api/sheets/:name/rows` | 行一覧 |
| `PUT` | `/api/sheets/:name/rows` | 行一括置換 (型強制 + 主キー重複検査) |
| `POST` | `/api/sheets/:name/publish` | version++ し、接続中 Unity に `master.reload` を送る |

## 抽象化のねらい

- 列スキーマを持つことで Web UI が型に応じた入力 (数値/真偽/文字列) を出せる。
- Unity の C# クラス (`SpreadSheet.EnemyData` 等) と列名を一致させれば `JsonUtility`
  でそのままデシリアライズできる。スキーマ↔C# の対応は手動 (将来 C# 生成も可)。
- `publish` で「編集 → Unity 即時反映」まで閉じる。
