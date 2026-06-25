/**
 * Data Studio の REST ルーティングと Unity 互換 /master を、HTTP 実装非依存の
 * 純ハンドラとして提供する。戻り値が null のときは「このモジュールの担当外」
 * (= 静的配信へフォールバック)を意味する。
 * 正本仕様: spec/code/Remote/data-studio.md
 */
import { SchemaError, type Row, type SheetSchema } from "./schema.js";
import { buildSheetDoc, buildVersionDoc } from "./master.js";
import type { SheetStore } from "./store.js";

export interface ApiResult {
  status: number;
  body: unknown;
}

export interface ApiRequest {
  method: string;
  /** クエリを除いたパス(例 /api/sheets/Enemy) */
  path: string;
  /** クエリパラメータ */
  query: URLSearchParams;
  /** パース済み JSON ボディ(無ければ undefined) */
  body?: unknown;
}

/** publish 時に接続中 Unity へ master.reload を送るためのフック。 */
export type PublishNotifier = (sheet: string) => void;

const json = (status: number, body: unknown): ApiResult => ({ status, body });

export async function handleApi(
  req: ApiRequest,
  store: SheetStore,
  notify: PublishNotifier,
): Promise<ApiResult | null> {
  const { method, path } = req;

  // --- Unity 互換 MasterData API ---
  if (path === "/master") {
    if (method !== "GET") return json(405, { error: "method_not_allowed" });
    const sheet = req.query.get("sheet");
    if (sheet) {
      const doc = await buildSheetDoc(store, sheet);
      return doc ? json(200, doc) : json(404, { error: "sheet_not_found" });
    }
    return json(200, await buildVersionDoc(store, Date.now()));
  }

  // --- Data Studio REST ---
  if (path === "/api/sheets") {
    if (method === "GET") return json(200, { sheets: await store.list() });
    return json(405, { error: "method_not_allowed" });
  }

  const m = path.match(/^\/api\/sheets\/([A-Za-z0-9_]+)(\/rows|\/publish)?$/);
  if (!m) return null;
  const name = m[1];
  const sub = m[2];

  try {
    if (!sub) {
      if (method === "GET") {
        const sheet = await store.get(name);
        return sheet ? json(200, sheet) : json(404, { error: "sheet_not_found" });
      }
      if (method === "PUT") {
        const schema = req.body as SheetSchema;
        if (!schema || schema.name !== name) {
          return json(400, { error: "name_mismatch" });
        }
        return json(200, await store.putSchema(schema));
      }
      if (method === "DELETE") {
        return json(200, { removed: await store.remove(name) });
      }
      return json(405, { error: "method_not_allowed" });
    }

    if (sub === "/rows") {
      if (method === "GET") {
        const sheet = await store.get(name);
        return sheet ? json(200, { rows: sheet.rows }) : json(404, { error: "sheet_not_found" });
      }
      if (method === "PUT") {
        const rows = (req.body as { rows?: Row[] })?.rows ?? (req.body as Row[]);
        if (!Array.isArray(rows)) return json(400, { error: "rows_must_be_array" });
        return json(200, await store.putRows(name, rows));
      }
      return json(405, { error: "method_not_allowed" });
    }

    if (sub === "/publish") {
      if (method !== "POST") return json(405, { error: "method_not_allowed" });
      const version = await store.bumpVersion(name);
      notify(name);
      return json(200, { name, version });
    }
  } catch (e) {
    if (e instanceof SchemaError) return json(400, { error: "schema_error", message: e.message });
    return json(400, { error: "bad_request", message: (e as Error).message });
  }

  return null;
}
