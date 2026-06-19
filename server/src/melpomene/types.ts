/**
 * Melpomene リレー契約の型。
 * 正本仕様: spec/code/Debug/MelpomeneDestination.md
 * Unity クライアント(MelpomeneRelayTarget)と共有する。
 */

export interface MelpomeneRelayRequest {
  title: string;
  body: string;
  labels?: string[];
  category?: string;
  priority?: string;
  userName?: string;
  sceneName?: string;
  platform?: string;
  appVersion?: string;
  screenInfo?: string;
  timestamp?: string;
  source?: string;
  clientVersion?: string;
}

export interface MelpomeneRelayResponse {
  success: boolean;
  issueNumber: number;
  issueUrl: string;
  error: string | null;
}

/** GitHub(等)へ Issue を作成する処理。リレーから差し替え可能にしてテストする。 */
export type IssueCreator = (
  req: MelpomeneRelayRequest,
) => Promise<{ issueNumber: number; issueUrl: string }>;

export function relayOk(issueNumber: number, issueUrl: string): MelpomeneRelayResponse {
  return { success: true, issueNumber, issueUrl, error: null };
}

export function relayFail(error: string): MelpomeneRelayResponse {
  return { success: false, issueNumber: 0, issueUrl: "", error };
}
