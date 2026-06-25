import { describe, expect, it } from "vitest";
import { Hub, type SignalSink } from "../src/bus/hub.js";
import type { ClientConn } from "../src/bus/connection.js";
import type { Envelope, Role } from "../src/protocol.js";

class FakeConn implements ClientConn {
  role: Role | null = null;
  name = "";
  sent: Envelope[] = [];
  closed = false;
  constructor(readonly id: string) {}
  sendRaw(data: string): void {
    this.sent.push(JSON.parse(data) as Envelope);
  }
  close(): void {
    this.closed = true;
  }
  last(): Envelope {
    return this.sent[this.sent.length - 1];
  }
}

class FakeRelay implements SignalSink {
  signals: { role: Role; type: string }[] = [];
  dropped: string[] = [];
  handleSignal(role: Role, _conn: ClientConn, env: Envelope): void {
    this.signals.push({ role, type: env.type });
  }
  dropConnection(conn: ClientConn): void {
    this.dropped.push(conn.id);
  }
}

function msg(type: string, payload: unknown, id?: string): string {
  return JSON.stringify({ v: 1, type, id, ts: 0, payload });
}

describe("Hub", () => {
  it("hello で welcome を返し role を設定", () => {
    const hub = new Hub(new FakeRelay());
    const panel = new FakeConn("p1");
    hub.handleRaw(panel, msg("hello", { role: "panel" }));
    expect(panel.role).toBe("panel");
    expect(panel.last().type).toBe("welcome");
  });

  it("hello 前のメッセージは弾く", () => {
    const hub = new Hub(new FakeRelay());
    const c = new FakeConn("x");
    hub.handleRaw(c, msg("command.invoke", { name: "a" }));
    expect(c.last().type).toBe("error");
  });

  it("panel→unity を中継、unity 不在なら error", () => {
    const hub = new Hub(new FakeRelay());
    const panel = new FakeConn("p");
    hub.handleRaw(panel, msg("hello", { role: "panel" }));
    hub.handleRaw(panel, msg("command.invoke", { name: "kill" }, "r1"));
    expect(panel.last()).toMatchObject({ type: "error", payload: { code: "unity_not_connected" } });

    const unity = new FakeConn("u");
    hub.handleRaw(unity, msg("hello", { role: "unity" }));
    hub.handleRaw(panel, msg("command.invoke", { name: "kill" }, "r2"));
    expect(unity.last()).toMatchObject({ type: "command.invoke", payload: { name: "kill" } });
  });

  it("unity→panel をブロードキャスト", () => {
    const hub = new Hub(new FakeRelay());
    const panel = new FakeConn("p");
    const unity = new FakeConn("u");
    hub.handleRaw(panel, msg("hello", { role: "panel" }));
    hub.handleRaw(unity, msg("hello", { role: "unity" }));
    hub.handleRaw(unity, msg("telemetry", { fps: 60, scene: "Main", memoryMB: 10, time: 1 }));
    expect(panel.last()).toMatchObject({ type: "telemetry", payload: { fps: 60 } });
  });

  it("rtc.* は relay へ委譲", () => {
    const relay = new FakeRelay();
    const hub = new Hub(relay);
    const unity = new FakeConn("u");
    hub.handleRaw(unity, msg("hello", { role: "unity" }));
    hub.handleRaw(unity, msg("rtc.offer", { sdp: "x" }));
    expect(relay.signals).toContainEqual({ role: "unity", type: "rtc.offer" });
  });

  it("unity 切断で panel に通知し relay に drop", () => {
    const relay = new FakeRelay();
    const hub = new Hub(relay);
    const panel = new FakeConn("p");
    const unity = new FakeConn("u");
    hub.handleRaw(panel, msg("hello", { role: "panel" }));
    hub.handleRaw(unity, msg("hello", { role: "unity" }));
    hub.remove(unity);
    expect(relay.dropped).toContain("u");
    expect(hub.unityConnected).toBe(false);
    expect(panel.last()).toMatchObject({ type: "log" });
  });

  it("sendToUnity は接続有無を返す", () => {
    const hub = new Hub(new FakeRelay());
    expect(hub.sendToUnity("master.reload", { sheet: "X" })).toBe(false);
    const unity = new FakeConn("u");
    hub.handleRaw(unity, msg("hello", { role: "unity" }));
    expect(hub.sendToUnity("master.reload", { sheet: "X" })).toBe(true);
    expect(unity.last()).toMatchObject({ type: "master.reload", payload: { sheet: "X" } });
  });
});
