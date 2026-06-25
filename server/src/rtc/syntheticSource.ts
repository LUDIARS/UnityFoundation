/**
 * 合成テスト映像ソース(任意)。ffmpeg の testsrc を H.264/RTP で吐かせ、UDP で
 * 受けて werift の RtpPacket に変換し MediaRelay へ流し込む。
 * Unity 未接続でもパネルの映像経路を確認できるようにするための開発補助。
 * ffmpeg が PATH に必要。[[feedback_werift_h264_rtp]] の流儀(ffmpeg -f rtp)に従う。
 */
import { spawn, type ChildProcess } from "node:child_process";
import dgram from "node:dgram";
import { RtpPacket } from "werift";
import type { MediaRelay } from "./mediaRelay.js";

export interface SyntheticSource {
  stop(): void;
}

export function startSyntheticSource(relay: MediaRelay, port = 5004): SyntheticSource {
  const socket = dgram.createSocket("udp4");
  socket.on("message", (buf) => {
    try {
      relay.pushRtp(RtpPacket.deSerialize(buf));
    } catch {
      /* 不正パケットは無視 */
    }
  });
  socket.bind(port, "127.0.0.1");

  const args = [
    "-re",
    "-f", "lavfi",
    "-i", "testsrc=size=640x360:rate=30",
    "-vf", "drawtext=text='Foundation Debug (synthetic)':x=10:y=10:fontsize=20:fontcolor=white",
    "-c:v", "libx264",
    "-profile:v", "baseline",
    "-tune", "zerolatency",
    "-pix_fmt", "yuv420p",
    "-g", "30",
    "-f", "rtp",
    `rtp://127.0.0.1:${port}?pkt_size=1200`,
  ];

  let proc: ChildProcess | null = null;
  try {
    proc = spawn("ffmpeg", args, { stdio: ["ignore", "ignore", "ignore"] });
    proc.on("error", () => {
      console.warn("[synthetic] ffmpeg を起動できません(PATH 未設定?)。合成映像は無効です。");
    });
  } catch {
    console.warn("[synthetic] ffmpeg 起動失敗。合成映像は無効です。");
  }

  return {
    stop(): void {
      proc?.kill("SIGKILL");
      socket.close();
    },
  };
}
