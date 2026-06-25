/** サーバ設定。環境変数で上書き可能。 */
export interface ServerConfig {
  /** HTTP + WS の待ち受けポート */
  port: number;
  /** バインドホスト */
  host: string;
  /** Data Studio のシート永続化ディレクトリ */
  dataDir: string;
  /** Web パネルの静的ファイルディレクトリ */
  webDir: string;
  /**
   * Unity 未接続時に合成テスト映像(ffmpeg testsrc)を流すか。
   * ffmpeg が PATH に必要。既定 off。
   */
  syntheticVideo: boolean;
  /** Melpomene リレー設定。トークンはサーバ側のみで保持しクライアントへ載せない。 */
  melpomene: {
    /** GitHub Issues 書込トークン(未設定ならリレー無効) */
    githubToken?: string;
    /** "owner/repo" */
    repo?: string;
    /** 設定時、リクエストの Authorization ヘッダ一致を要求 */
    relayAuth?: string;
  };
}

function envInt(name: string, fallback: number): number {
  const v = process.env[name];
  if (!v) return fallback;
  const n = Number.parseInt(v, 10);
  return Number.isFinite(n) ? n : fallback;
}

export function loadConfig(overrides: Partial<ServerConfig> = {}): ServerConfig {
  return {
    port: envInt("FOUNDATION_DEBUG_PORT", 8787),
    host: process.env.FOUNDATION_DEBUG_HOST ?? "0.0.0.0",
    dataDir: process.env.FOUNDATION_DEBUG_DATA_DIR ?? "data/sheets",
    webDir: process.env.FOUNDATION_DEBUG_WEB_DIR ?? "web",
    syntheticVideo: process.env.FOUNDATION_DEBUG_SYNTHETIC_VIDEO === "1",
    melpomene: {
      githubToken: process.env.MELPOMENE_GITHUB_TOKEN,
      repo: process.env.MELPOMENE_REPO,
      relayAuth: process.env.MELPOMENE_RELAY_AUTH,
    },
    ...overrides,
  };
}
