/** 最小の静的ファイル配信。webDir 外への参照(パストラバーサル)は拒否する。 */
import { promises as fs } from "node:fs";
import path from "node:path";
import type { ServerResponse } from "node:http";

const MIME: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".ico": "image/x-icon",
};

export async function serveStatic(webDir: string, urlPath: string, res: ServerResponse): Promise<void> {
  const rel = urlPath === "/" ? "index.html" : urlPath.replace(/^\/+/, "");
  const root = path.resolve(webDir);
  const target = path.resolve(root, rel);
  if (target !== root && !target.startsWith(root + path.sep)) {
    res.writeHead(403).end("forbidden");
    return;
  }
  try {
    const data = await fs.readFile(target);
    const mime = MIME[path.extname(target).toLowerCase()] ?? "application/octet-stream";
    res.writeHead(200, { "content-type": mime }).end(data);
  } catch {
    res.writeHead(404).end("not found");
  }
}
