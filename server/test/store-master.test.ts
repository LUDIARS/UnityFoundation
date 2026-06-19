import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { promises as fs } from "node:fs";
import os from "node:os";
import path from "node:path";
import { SheetStore } from "../src/datastudio/store.js";
import { buildSheetDoc, buildVersionDoc, toDotNetTicks } from "../src/datastudio/master.js";
import type { SheetSchema } from "../src/datastudio/schema.js";

let dir: string;
let store: SheetStore;

const schema: SheetSchema = {
  name: "Enemy",
  version: 2,
  key: "Id",
  columns: [
    { key: "Id", type: "int" },
    { key: "Name", type: "string" },
  ],
};

beforeEach(async () => {
  dir = await fs.mkdtemp(path.join(os.tmpdir(), "fds-"));
  store = new SheetStore(dir);
  await store.init();
});
afterEach(async () => {
  await fs.rm(dir, { recursive: true, force: true });
});

describe("SheetStore", () => {
  it("putSchema → get → putRows → list", async () => {
    await store.putSchema(schema);
    expect(await store.list()).toEqual(["Enemy"]);
    await store.putRows("Enemy", [{ Id: "3", Name: "orc", extra: 1 }]);
    const sheet = await store.get("Enemy");
    expect(sheet?.rows).toEqual([{ Id: 3, Name: "orc" }]);
  });

  it("既存行はスキーマ更新時に正規化して保持", async () => {
    await store.putSchema(schema);
    await store.putRows("Enemy", [{ Id: 1, Name: "a" }]);
    await store.putSchema({ ...schema, columns: [...schema.columns, { key: "Hp", type: "int", default: 5 }] });
    const sheet = await store.get("Enemy");
    expect(sheet?.rows).toEqual([{ Id: 1, Name: "a", Hp: 5 }]);
  });

  it("bumpVersion / remove", async () => {
    await store.putSchema(schema);
    expect(await store.bumpVersion("Enemy")).toBe(3);
    expect(await store.remove("Enemy")).toBe(true);
    expect(await store.get("Enemy")).toBeNull();
  });
});

describe("master 互換ドキュメント", () => {
  it("toDotNetTicks は epoch=0 で .NET エポック差", () => {
    expect(toDotNetTicks(0)).toBe(621355968000000000);
  });
  it("buildVersionDoc は全シートの version 表", async () => {
    await store.putSchema(schema);
    const doc = await buildVersionDoc(store, 0);
    expect(doc.Enemy).toBe(2);
    expect(doc.TimeStamp).toBe(621355968000000000);
  });
  it("buildSheetDoc は { Version, Data }", async () => {
    await store.putSchema(schema);
    await store.putRows("Enemy", [{ Id: 1, Name: "a" }]);
    expect(await buildSheetDoc(store, "Enemy")).toEqual({ Version: 2, Data: [{ Id: 1, Name: "a" }] });
    expect(await buildSheetDoc(store, "None")).toBeNull();
  });
});
