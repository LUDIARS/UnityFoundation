/**
 * 制御チャネルの 1 接続を表す抽象。
 * ws の WebSocket を直接持たず、テストから差し替え可能なインターフェースにする。
 */
import type { Envelope, MessagePayloads, MessageType, Role } from "../protocol.js";
import { makeEnvelope } from "../protocol.js";

export interface ClientConn {
  readonly id: string;
  role: Role | null;
  name: string;
  /** 生 JSON 文字列を送る。 */
  sendRaw(data: string): void;
  /** 接続を閉じる。 */
  close(): void;
}

/** 型付きエンベロープを送る補助。 */
export function send<K extends MessageType>(
  conn: ClientConn,
  type: K,
  payload: MessagePayloads[K],
  id?: string,
): void {
  conn.sendRaw(JSON.stringify(makeEnvelope(type, payload, id)));
}

/** 受信済みエンベロープをそのまま転送する。 */
export function forward(conn: ClientConn, env: Envelope): void {
  conn.sendRaw(JSON.stringify(env));
}
