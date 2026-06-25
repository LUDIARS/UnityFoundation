/**
 * 制御チャネルのルーティングハブ。
 * panel ↔ unity のメッセージを役割に基づき透過中継し、WebRTC シグナリングは
 * MediaRelay に委譲する。
 * 正本仕様: spec/code/Remote/protocol.md
 */
import {
  PANEL_TO_UNITY,
  RTC_TYPES,
  UNITY_TO_PANEL,
  parseEnvelope,
  type Envelope,
  type MessagePayloads,
  type MessageType,
  type Role,
} from "../protocol.js";
import { forward, send, type ClientConn } from "./connection.js";

/** WebRTC シグナリングを処理する相手(MediaRelay)が満たすべき契約。 */
export interface SignalSink {
  handleSignal(role: Role, conn: ClientConn, env: Envelope): void | Promise<void>;
  dropConnection(conn: ClientConn): void;
}

export class Hub {
  private readonly panels = new Set<ClientConn>();
  private unity: ClientConn | null = null;

  constructor(private readonly relay: SignalSink) {}

  get unityConnected(): boolean {
    return this.unity !== null;
  }

  get panelCount(): number {
    return this.panels.size;
  }

  /** Unity 接続があればメッセージを送る。送れたら true。 */
  sendToUnity<K extends MessageType>(type: K, payload: MessagePayloads[K]): boolean {
    if (!this.unity) return false;
    send(this.unity, type, payload);
    return true;
  }

  /** 接続から届いた生メッセージ 1 件を処理する。 */
  handleRaw(conn: ClientConn, raw: string): void {
    const env = parseEnvelope(raw);
    if (!env) {
      send(conn, "error", { code: "bad_message", message: "JSON または type が不正です" });
      return;
    }

    if (env.type === "hello") {
      this.onHello(conn, env);
      return;
    }
    if (conn.role === null) {
      send(conn, "error", { code: "bad_message", message: "hello を先に送ってください" }, env.id);
      return;
    }

    if (RTC_TYPES.has(env.type)) {
      void this.relay.handleSignal(conn.role, conn, env);
      return;
    }

    if (conn.role === "panel" && PANEL_TO_UNITY.has(env.type)) {
      this.toUnity(conn, env);
      return;
    }
    if (conn.role === "unity" && UNITY_TO_PANEL.has(env.type)) {
      this.toPanels(env);
      return;
    }

    send(conn, "error", { code: "unknown_type", message: `中継不可: ${env.type}` }, env.id);
  }

  /** 接続切断時の後始末。 */
  remove(conn: ClientConn): void {
    this.relay.dropConnection(conn);
    if (conn === this.unity) {
      this.unity = null;
      this.broadcastUnityStatus(false);
    }
    this.panels.delete(conn);
  }

  private onHello(conn: ClientConn, env: Envelope): void {
    const payload = (env.payload ?? {}) as { role?: Role; name?: string };
    const role = payload.role;
    if (role !== "unity" && role !== "panel") {
      send(conn, "error", { code: "bad_message", message: "role は unity|panel" }, env.id);
      return;
    }
    conn.role = role;
    conn.name = payload.name ?? role;
    if (role === "unity") {
      // 最新の Unity 接続を優先(古いものは切断)。
      if (this.unity && this.unity !== conn) this.unity.close();
      this.unity = conn;
      this.broadcastUnityStatus(true);
    } else {
      this.panels.add(conn);
    }
    send(conn, "welcome", { sessionId: conn.id, unityConnected: this.unityConnected }, env.id);
  }

  private toUnity(from: ClientConn, env: Envelope): void {
    if (!this.unity) {
      send(from, "error", { code: "unity_not_connected", message: "Unity 未接続" }, env.id);
      return;
    }
    forward(this.unity, env);
  }

  private toPanels(env: Envelope): void {
    for (const p of this.panels) forward(p, env);
  }

  private broadcastUnityStatus(connected: boolean): void {
    for (const p of this.panels) {
      send(p, "log", {
        level: "log",
        message: connected ? "Unity が接続しました" : "Unity が切断しました",
      });
    }
  }
}
