import { describe, expect, it } from "vitest";
import {
  SchemaError,
  coerceValue,
  isValidSheetName,
  normalizeRows,
  validateSchema,
  type SheetSchema,
} from "../src/datastudio/schema.js";

describe("coerceValue", () => {
  it("int を切り捨て変換", () => {
    expect(coerceValue("int", "12.9")).toBe(12);
    expect(coerceValue("int", 5)).toBe(5);
    expect(coerceValue("int", "")).toBe(0);
    expect(coerceValue("int", undefined, 7)).toBe(7);
  });
  it("float", () => {
    expect(coerceValue("float", "1.5")).toBe(1.5);
    expect(coerceValue("float", "")).toBe(0);
  });
  it("bool は truthy 文字列を解釈", () => {
    expect(coerceValue("bool", "true")).toBe(true);
    expect(coerceValue("bool", "1")).toBe(true);
    expect(coerceValue("bool", "no")).toBe(false);
    expect(coerceValue("bool", false)).toBe(false);
  });
  it("string", () => {
    expect(coerceValue("string", 5)).toBe("5");
    expect(coerceValue("string", null)).toBe("");
  });
  it("変換不能な int は SchemaError", () => {
    expect(() => coerceValue("int", "abc")).toThrow(SchemaError);
  });
});

describe("isValidSheetName", () => {
  it("英数_ のみ許可", () => {
    expect(isValidSheetName("Enemy_01")).toBe(true);
    expect(isValidSheetName("../etc")).toBe(false);
    expect(isValidSheetName("")).toBe(false);
  });
});

const schema: SheetSchema = {
  name: "Enemy",
  version: 1,
  key: "Id",
  columns: [
    { key: "Id", type: "int" },
    { key: "Name", type: "string" },
    { key: "Hp", type: "int", default: 100 },
  ],
};

describe("validateSchema", () => {
  it("正常系", () => expect(() => validateSchema(schema)).not.toThrow());
  it("列名重複を弾く", () => {
    const bad = { ...schema, columns: [...schema.columns, { key: "Id", type: "int" as const }] };
    expect(() => validateSchema(bad)).toThrow(SchemaError);
  });
  it("主キーが列に無いと弾く", () => {
    expect(() => validateSchema({ ...schema, key: "Missing" })).toThrow(SchemaError);
  });
});

describe("normalizeRows", () => {
  it("型強制 + 既定値補完 + スキーマ外キー除去", () => {
    const rows = normalizeRows(schema, [{ Id: "1", Name: "slime", junk: "x" }]);
    expect(rows).toEqual([{ Id: 1, Name: "slime", Hp: 100 }]);
  });
  it("主キー重複を弾く", () => {
    expect(() => normalizeRows(schema, [{ Id: 1 }, { Id: 1 }])).toThrow(SchemaError);
  });
});
