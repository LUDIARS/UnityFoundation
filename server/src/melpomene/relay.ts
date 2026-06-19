/**
 * Melpomene リレーのリクエスト処理(HTTP 実装非依存)。
 * トークンはサーバ側に置き、クライアントには載せない。GitHub への実送信は
 * IssueCreator として注入し、テストで差し替えられるようにする。
 * 正本仕様: spec/code/Debug/MelpomeneDestination.md
 */
import type { ApiResult } from "../datastudio/routes.js";
import {
  relayFail,
  relayOk,
  type IssueCreator,
  type MelpomeneRelayRequest,
} from "./types.js";

export interface RelayDeps {
  /** 設定済みなら Issue 作成処理。未設定(=リレー無効)なら null。 */
  createIssue: IssueCreator | null;
  /** 設定時、リクエストの Authorization ヘッダがこの値と一致しなければ拒否。 */
  relayAuth?: string;
}

const json = (status: number, body: unknown): ApiResult => ({ status, body });

function isValidRequest(body: unknown): body is MelpomeneRelayRequest {
  if (typeof body !== "object" || body === null) return false;
  const b = body as Record<string, unknown>;
  return typeof b.title === "string" && b.title.trim().length > 0 && typeof b.body === "string";
}

/**
 * POST /api/melpomene/report を処理する。
 * @param body  パース済み JSON ボディ
 * @param authHeader  リクエストの Authorization ヘッダ(無ければ undefined)
 */
export async function handleMelpomeneReport(
  body: unknown,
  authHeader: string | undefined,
  deps: RelayDeps,
): Promise<ApiResult> {
  if (deps.relayAuth && authHeader !== deps.relayAuth) {
    return json(401, relayFail("unauthorized"));
  }
  if (!isValidRequest(body)) {
    return json(400, relayFail("invalid_request: title と body は必須です"));
  }
  if (!deps.createIssue) {
    return json(503, relayFail("relay_not_configured"));
  }
  try {
    const { issueNumber, issueUrl } = await deps.createIssue(body);
    return json(200, relayOk(issueNumber, issueUrl));
  } catch (e) {
    return json(502, relayFail(`upstream_error: ${(e as Error).message}`));
  }
}
