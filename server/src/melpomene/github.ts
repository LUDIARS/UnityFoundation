/**
 * GitHub Issues API への実送信(IssueCreator 実装)。
 * トークン/リポジトリはサーバ側 env から受け取り、クライアントには載せない。
 */
import type { IssueCreator, MelpomeneRelayRequest } from "./types.js";

export interface GitHubRelayOptions {
  /** Issues 書込権限トークン */
  token: string;
  /** "owner/repo" */
  repo: string;
}

interface GitHubIssueResponse {
  number: number;
  html_url: string;
}

/** env から GitHub 用 IssueCreator を組み立てる。未設定なら null。 */
export function makeGitHubIssueCreator(opts: Partial<GitHubRelayOptions>): IssueCreator | null {
  if (!opts.token || !opts.repo) return null;
  const { token, repo } = opts as GitHubRelayOptions;

  return async (req: MelpomeneRelayRequest) => {
    const res = await fetch(`https://api.github.com/repos/${repo}/issues`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${token}`,
        accept: "application/vnd.github+json",
        "user-agent": "Melpomene-Relay",
      },
      body: JSON.stringify({
        title: req.title,
        body: req.body,
        labels: req.labels ?? [],
      }),
    });
    if (!res.ok) {
      const text = await res.text().catch(() => "");
      throw new Error(`GitHub ${res.status}: ${text.slice(0, 200)}`);
    }
    const data = (await res.json()) as GitHubIssueResponse;
    return { issueNumber: data.number, issueUrl: data.html_url };
  };
}
