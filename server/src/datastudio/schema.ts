/**
 * Data Studio のシートスキーマと型強制。
 * 正本仕様: spec/code/Remote/data-studio.md
 */

export type ColumnType = "int" | "float" | "bool" | "string";

export interface Column {
  key: string;
  type: ColumnType;
  default?: unknown;
}

export interface SheetSchema {
  name: string;
  version: number;
  /** 主キー列(任意)。指定時は行の重複検査に使う。 */
  key?: string;
  columns: Column[];
}

export type Row = Record<string, unknown>;

export interface Sheet {
  schema: SheetSchema;
  rows: Row[];
}

const NAME_RE = /^[A-Za-z0-9_]+$/;

/** シート名がファイル名/URL に安全か検査する。 */
export function isValidSheetName(name: string): boolean {
  return NAME_RE.test(name) && name.length > 0 && name.length <= 64;
}

export class SchemaError extends Error {}

/** 単一セル値を列の型に強制する。空/未定義は default → 型の零値。 */
export function coerceValue(type: ColumnType, value: unknown, def?: unknown): unknown {
  const v = value ?? def;
  switch (type) {
    case "int": {
      if (v === undefined || v === null || v === "") return 0;
      const n = typeof v === "number" ? v : Number.parseInt(String(v), 10);
      if (!Number.isFinite(n)) throw new SchemaError(`int に変換不能: ${String(value)}`);
      return Math.trunc(n);
    }
    case "float": {
      if (v === undefined || v === null || v === "") return 0;
      const n = typeof v === "number" ? v : Number.parseFloat(String(v));
      if (!Number.isFinite(n)) throw new SchemaError(`float に変換不能: ${String(value)}`);
      return n;
    }
    case "bool": {
      if (typeof v === "boolean") return v;
      if (v === undefined || v === null || v === "") return false;
      const s = String(v).toLowerCase();
      return s === "true" || s === "1" || s === "yes";
    }
    case "string":
      return v === undefined || v === null ? "" : String(v);
  }
}

/** スキーマ定義そのものを検証(列名重複・型・主キー存在)。 */
export function validateSchema(schema: SheetSchema): void {
  if (!isValidSheetName(schema.name)) {
    throw new SchemaError(`不正なシート名: ${schema.name}`);
  }
  if (!Number.isInteger(schema.version) || schema.version < 0) {
    throw new SchemaError(`version は 0 以上の整数: ${schema.version}`);
  }
  if (!Array.isArray(schema.columns) || schema.columns.length === 0) {
    throw new SchemaError("columns が空です");
  }
  const seen = new Set<string>();
  for (const c of schema.columns) {
    if (!c.key || typeof c.key !== "string") throw new SchemaError("列 key が不正です");
    if (seen.has(c.key)) throw new SchemaError(`列名が重複: ${c.key}`);
    seen.add(c.key);
    if (!["int", "float", "bool", "string"].includes(c.type)) {
      throw new SchemaError(`不正な型: ${c.key}:${c.type}`);
    }
  }
  if (schema.key && !seen.has(schema.key)) {
    throw new SchemaError(`主キー列が存在しません: ${schema.key}`);
  }
}

/**
 * 行配列をスキーマで正規化する。
 * - 各列を型強制し、スキーマ外のキーは捨てる。
 * - 主キー指定時は重複を弾く。
 */
export function normalizeRows(schema: SheetSchema, rows: Row[]): Row[] {
  const out: Row[] = [];
  const keys = schema.key ? new Set<string>() : null;
  for (const raw of rows) {
    const row: Row = {};
    for (const c of schema.columns) {
      row[c.key] = coerceValue(c.type, raw[c.key], c.default);
    }
    if (keys && schema.key) {
      const pk = String(row[schema.key]);
      if (keys.has(pk)) throw new SchemaError(`主キー重複: ${schema.key}=${pk}`);
      keys.add(pk);
    }
    out.push(row);
  }
  return out;
}
