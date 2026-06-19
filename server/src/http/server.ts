/**
 * HTTP + WebSocket サーバの組み立て。
 * - /ws        : 制御チャネル(WebSocket) → Hub
 * - /api/* , /master : Data Studio / Unity 互換 API
 * - それ以外   : web/ の静的配信
 */
import http from "node:http";
import { WebSocketServer } from "ws";
import type { ServerConfig } from "../config.js";
import { Hub } from "../bus/hub.js";
import { MediaRelay } from "../rtc/mediaRelay.js";
import { SheetStore } from "../datastudio/store.js";
import { handleApi, type ApiRequest } from "../datastudio/routes.js";
import { handleMelpomeneReport } from "../melpomene/relay.js";
import { makeGitHubIssueCreator } from "../melpomene/github.js";
import { serveStatic } from "./static.js";
import { WsConn } from "./wsConn.js";

export interface RunningServer {
  server: http.Server;
  hub: Hub;
  relay: MediaRelay;
  store: SheetStore;
  close(): Promise<void>;
}

async function readBody(req: http.IncomingMessage): Promise<unknown> {
  const chunks: Buffer[] = [];
  for await (const c of req) chunks.push(c as Buffer);
  if (chunks.length === 0) return undefined;
  try {
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } catch {
    return undefined;
  }
}

export async function createServer(config: ServerConfig): Promise<RunningServer> {
  const store = new SheetStore(config.dataDir);
  await store.init();
  const relay = new MediaRelay();
  const hub = new Hub(relay);

  // publish → 接続中 Unity に再読込を促す。
  const notify = (sheet: string): void => {
    hub.sendToUnity("master.reload", { sheet });
  };

  const httpServer = http.createServer((req, res) => {
    void handleHttp(req, res, config, store, notify);
  });

  const wss = new WebSocketServer({ noServer: true });
  httpServer.on("upgrade", (req, socket, head) => {
    if (new URL(req.url ?? "/", "http://x").pathname !== "/ws") {
      socket.destroy();
      return;
    }
    wss.handleUpgrade(req, socket, head, (ws) => {
      const conn = new WsConn(ws);
      ws.on("message", (data) => hub.handleRaw(conn, data.toString()));
      ws.on("close", () => hub.remove(conn));
      ws.on("error", () => hub.remove(conn));
    });
  });

  await new Promise<void>((resolve) => httpServer.listen(config.port, config.host, resolve));

  return {
    server: httpServer,
    hub,
    relay,
    store,
    close: () =>
      new Promise<void>((resolve, reject) => {
        wss.close();
        httpServer.close((e) => (e ? reject(e) : resolve()));
      }),
  };
}

async function handleHttp(
  req: http.IncomingMessage,
  res: http.ServerResponse,
  config: ServerConfig,
  store: SheetStore,
  notify: (sheet: string) => void,
): Promise<void> {
  const url = new URL(req.url ?? "/", "http://x");
  const method = req.method ?? "GET";

  // Melpomene リレー(トークンはサーバ側 env)。汎用 API ルータより先に処理する。
  if (url.pathname === "/api/melpomene/report") {
    if (method !== "POST") {
      res.writeHead(405, { "content-type": "application/json" }).end(JSON.stringify({ success: false, error: "method_not_allowed" }));
      return;
    }
    const result = await handleMelpomeneReport(await readBody(req), req.headers.authorization, {
      createIssue: makeGitHubIssueCreator({ token: config.melpomene.githubToken, repo: config.melpomene.repo }),
      relayAuth: config.melpomene.relayAuth,
    });
    res
      .writeHead(result.status, { "content-type": "application/json; charset=utf-8" })
      .end(JSON.stringify(result.body));
    return;
  }

  if (url.pathname === "/api" || url.pathname.startsWith("/api/") || url.pathname === "/master") {
    const apiReq: ApiRequest = {
      method,
      path: url.pathname,
      query: url.searchParams,
      body: method === "PUT" || method === "POST" ? await readBody(req) : undefined,
    };
    const result = await handleApi(apiReq, store, notify);
    if (result) {
      res
        .writeHead(result.status, { "content-type": "application/json; charset=utf-8" })
        .end(JSON.stringify(result.body));
      return;
    }
  }

  await serveStatic(config.webDir, url.pathname, res);
}
