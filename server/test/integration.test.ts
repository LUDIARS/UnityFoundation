import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { promises as fs } from "node:fs";
import os from "node:os";
import path from "node:path";
import { WebSocket } from "ws";
import { createServer, type RunningServer } from "../src/http/server.js";
import { loadConfig } from "../src/config.js";

let running: RunningServer;
let port: number;
let dir: string;

function open(): Promise<WebSocket> {
  const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
  return new Promise((resolve, reject) => {
    ws.on("open", () => resolve(ws));
    ws.on("error", reject);
  });
}

function next(ws: WebSocket): Promise<Record<string, unknown>> {
  return new Promise((resolve) => ws.once("message", (d) => resolve(JSON.parse(d.toString()))));
}

function sendMsg(ws: WebSocket, type: string, payload: unknown): void {
  ws.send(JSON.stringify({ v: 1, type, ts: 0, payload }));
}

beforeAll(async () => {
  dir = await fs.mkdtemp(path.join(os.tmpdir(), "fds-int-"));
  running = await createServer(loadConfig({ port: 0, host: "127.0.0.1", dataDir: dir, webDir: dir }));
  port = (running.server.address() as { port: number }).port;
});
afterAll(async () => {
  await running.close();
  await fs.rm(dir, { recursive: true, force: true });
});

describe("WS 統合", () => {
  it("hello → welcome", async () => {
    const ws = await open();
    sendMsg(ws, "hello", { role: "panel" });
    const welcome = await next(ws);
    expect(welcome.type).toBe("welcome");
    ws.close();
  });

  it("panel→unity 中継が実 WS で成立", async () => {
    const unity = await open();
    sendMsg(unity, "hello", { role: "unity" });
    await next(unity); // welcome

    const panel = await open();
    sendMsg(panel, "hello", { role: "panel" });
    await next(panel); // welcome

    const got = next(unity);
    sendMsg(panel, "command.invoke", { name: "godmode" });
    const relayed = await got;
    expect(relayed).toMatchObject({ type: "command.invoke", payload: { name: "godmode" } });

    unity.close();
    panel.close();
  });
});
