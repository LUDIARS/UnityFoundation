import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { promises as fs } from "node:fs";
import os from "node:os";
import path from "node:path";
import { SheetStore } from "../src/datastudio/store.js";
import { handleApi, type ApiRequest } from "../src/datastudio/routes.js";

let dir: string;
let store: SheetStore;
let published: string[];

const notify = (s: string): void => void published.push(s);

function req(method: string, pathname: string, body?: unknown, query = ""): ApiRequest {
  return { method, path: pathname, query: new URLSearchParams(query), body };
}

const schema = {
  name: "Item",
  version: 1,
  key: "Id",
  columns: [
    { key: "Id", type: "int" as const },
    { key: "Name", type: "string" as const },
  ],
};

beforeEach(async () => {
  dir = await fs.mkdtemp(path.join(os.tmpdir(), "fds-r-"));
  store = new SheetStore(dir);
  await store.init();
  published = [];
});
afterEach(async () => {
  await fs.rm(dir, { recursive: true, force: true });
});

describe("handleApi", () => {
  it("無関係パスは null (静的フォールバック)", async () => {
    expect(await handleApi(req("GET", "/index.html"), store, notify)).toBeNull();
  });

  it("シート CRUD と /master 互換", async () => {
    expect((await handleApi(req("PUT", "/api/sheets/Item", schema), store, notify))!.status).toBe(200);
    expect((await handleApi(req("GET", "/api/sheets"), store, notify))!.body).toEqual({ sheets: ["Item"] });

    await handleApi(req("PUT", "/api/sheets/Item/rows", { rows: [{ Id: 1, Name: "potion" }] }), store, notify);

    const master = await handleApi(req("GET", "/master", undefined, "sheet=Item"), store, notify);
    expect(master!.body).toEqual({ Version: 1, Data: [{ Id: 1, Name: "potion" }] });

    const ver = await handleApi(req("GET", "/master"), store, notify);
    expect((ver!.body as Record<string, number>).Item).toBe(1);
  });

  it("publish は version++ して notify", async () => {
    await handleApi(req("PUT", "/api/sheets/Item", schema), store, notify);
    const r = await handleApi(req("POST", "/api/sheets/Item/publish"), store, notify);
    expect(r!.body).toEqual({ name: "Item", version: 2 });
    expect(published).toEqual(["Item"]);
  });

  it("name 不一致 PUT は 400", async () => {
    const r = await handleApi(req("PUT", "/api/sheets/Item", { ...schema, name: "Other" }), store, notify);
    expect(r!.status).toBe(400);
  });

  it("存在しないシートの /master?sheet= は 404", async () => {
    const r = await handleApi(req("GET", "/master", undefined, "sheet=Nope"), store, notify);
    expect(r!.status).toBe(404);
  });
});
