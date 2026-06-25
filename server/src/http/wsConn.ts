/** ws の WebSocket を ClientConn に適合させるアダプタ。 */
import type { WebSocket } from "ws";
import type { ClientConn } from "../bus/connection.js";
import type { Role } from "../protocol.js";

let counter = 0;

export class WsConn implements ClientConn {
  readonly id: string;
  role: Role | null = null;
  name = "";

  constructor(private readonly ws: WebSocket) {
    this.id = `c${++counter}-${Date.now().toString(36)}`;
  }

  sendRaw(data: string): void {
    if (this.ws.readyState === this.ws.OPEN) this.ws.send(data);
  }

  close(): void {
    try {
      this.ws.close();
    } catch {
      /* noop */
    }
  }
}
