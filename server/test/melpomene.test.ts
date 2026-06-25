import { describe, expect, it, vi } from "vitest";
import { handleMelpomeneReport } from "../src/melpomene/relay.js";
import type { MelpomeneRelayRequest } from "../src/melpomene/types.js";

const valid: MelpomeneRelayRequest = {
  title: "[Melpomene] crash on boot",
  body: "詳細...",
  labels: ["melpomene", "bug"],
  category: "Bug",
  priority: "High",
};

const okCreator = vi.fn(async () => ({ issueNumber: 42, issueUrl: "https://x/issues/42" }));

describe("handleMelpomeneReport", () => {
  it("成功時 200 + 正規化レスポンス", async () => {
    const r = await handleMelpomeneReport(valid, undefined, { createIssue: okCreator });
    expect(r.status).toBe(200);
    expect(r.body).toEqual({ success: true, issueNumber: 42, issueUrl: "https://x/issues/42", error: null });
  });

  it("未設定(createIssue=null)は 503 relay_not_configured", async () => {
    const r = await handleMelpomeneReport(valid, undefined, { createIssue: null });
    expect(r.status).toBe(503);
    expect((r.body as { error: string }).error).toBe("relay_not_configured");
  });

  it("title 欠落は 400", async () => {
    const r = await handleMelpomeneReport({ body: "x" }, undefined, { createIssue: okCreator });
    expect(r.status).toBe(400);
  });

  it("relayAuth 設定時、ヘッダ不一致は 401", async () => {
    const r = await handleMelpomeneReport(valid, "Bearer wrong", { createIssue: okCreator, relayAuth: "Bearer secret" });
    expect(r.status).toBe(401);
  });

  it("relayAuth 設定時、ヘッダ一致は 200", async () => {
    const r = await handleMelpomeneReport(valid, "Bearer secret", { createIssue: okCreator, relayAuth: "Bearer secret" });
    expect(r.status).toBe(200);
  });

  it("upstream 例外は 502", async () => {
    const r = await handleMelpomeneReport(valid, undefined, {
      createIssue: async () => {
        throw new Error("GitHub 401");
      },
    });
    expect(r.status).toBe(502);
    expect((r.body as { error: string }).error).toContain("upstream_error");
  });
});
