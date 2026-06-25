/** Foundation Debug Server エントリポイント。 */
import { loadConfig } from "./config.js";
import { createServer } from "./http/server.js";
import { startSyntheticSource, type SyntheticSource } from "./rtc/syntheticSource.js";

async function main(): Promise<void> {
  const config = loadConfig();
  const running = await createServer(config);

  let synthetic: SyntheticSource | null = null;
  if (config.syntheticVideo) {
    synthetic = startSyntheticSource(running.relay);
  }

  const addr = `${config.host}:${config.port}`;
  console.log(`[foundation-debug] listening on http://${addr}`);
  console.log(`[foundation-debug] panel:  http://localhost:${config.port}/`);
  console.log(`[foundation-debug] ws:     ws://localhost:${config.port}/ws`);
  console.log(`[foundation-debug] master: http://localhost:${config.port}/master`);
  if (config.syntheticVideo) console.log("[foundation-debug] synthetic video: ON");

  const shutdown = async (): Promise<void> => {
    console.log("\n[foundation-debug] shutting down...");
    synthetic?.stop();
    await running.close();
    process.exit(0);
  };
  process.on("SIGINT", () => void shutdown());
  process.on("SIGTERM", () => void shutdown());
}

main().catch((e) => {
  console.error("[foundation-debug] fatal:", e);
  process.exit(1);
});
