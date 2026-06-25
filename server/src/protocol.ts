/**
 * Remote Debug プロトコル型定義。
 * 正本仕様: spec/code/Remote/protocol.md
 *
 * 制御チャネル(WebSocket)上を流れる全メッセージは共通エンベロープを持つ。
 * WebRTC のシグナリングも同じ WS に相乗りする。
 */

export const PROTOCOL_VERSION = 1 as const;

export type Role = "unity" | "panel";

/** 全メッセージ共通エンベロープ */
export interface Envelope<T = unknown> {
  v: number;
  type: string;
  /** リクエスト/レスポンス対応付け(任意) */
  id?: string;
  /** 送信側 unix ms */
  ts: number;
  payload: T;
}

export type CommandKind = "action" | "toggle";

export interface CommandInfo {
  name: string;
  description: string;
  kind: CommandKind;
}

export interface Telemetry {
  fps: number;
  scene: string;
  memoryMB: number;
  time: number;
}

export type LogLevel = "log" | "warning" | "error";

export type ErrorCode =
  | "unity_not_connected"
  | "bad_message"
  | "unknown_type"
  | "internal";

/** payload 型の対応表(送受信の型付けに使う) */
export interface MessagePayloads {
  hello: { role: Role; name?: string };
  welcome: { sessionId: string; unityConnected: boolean };
  "command.list.request": Record<string, never>;
  "command.list": { commands: CommandInfo[] };
  "command.invoke": { name: string; value?: boolean };
  "command.result": { name: string; ok: boolean; message?: string };
  "scene.load": { scene: string };
  "blackboard.set": { key: string; value: unknown };
  "master.reload": { sheet: string };
  telemetry: Telemetry;
  log: { level: LogLevel; message: string };
  "rtc.offer": { sdp: string };
  "rtc.answer": { sdp: string };
  "rtc.ice": { candidate: unknown };
  error: { code: ErrorCode; message: string };
}

export type MessageType = keyof MessagePayloads;

/** panel -> unity に中継すべきメッセージ種別 */
export const PANEL_TO_UNITY: ReadonlySet<string> = new Set<MessageType>([
  "command.list.request",
  "command.invoke",
  "scene.load",
  "blackboard.set",
  "master.reload",
]);

/** unity -> panel に中継すべきメッセージ種別 */
export const UNITY_TO_PANEL: ReadonlySet<string> = new Set<MessageType>([
  "command.list",
  "command.result",
  "telemetry",
  "log",
]);

/** WebRTC シグナリング種別 */
export const RTC_TYPES: ReadonlySet<string> = new Set<MessageType>([
  "rtc.offer",
  "rtc.answer",
  "rtc.ice",
]);

export function makeEnvelope<K extends MessageType>(
  type: K,
  payload: MessagePayloads[K],
  id?: string,
): Envelope<MessagePayloads[K]> {
  return { v: PROTOCOL_VERSION, type, id, ts: Date.now(), payload };
}

/** 受信した生文字列をエンベロープに復号する。失敗時は null。 */
export function parseEnvelope(raw: string): Envelope | null {
  let obj: unknown;
  try {
    obj = JSON.parse(raw);
  } catch {
    return null;
  }
  if (typeof obj !== "object" || obj === null) return null;
  const e = obj as Record<string, unknown>;
  if (typeof e.type !== "string") return null;
  if (typeof e.v !== "number") return null;
  return {
    v: e.v,
    type: e.type,
    id: typeof e.id === "string" ? e.id : undefined,
    ts: typeof e.ts === "number" ? e.ts : Date.now(),
    payload: "payload" in e ? e.payload : {},
  };
}
