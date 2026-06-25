/**
 * WebRTC 映像リレー(SFU)。Ergo 系の werift スタックで構築する。
 * 正本仕様: spec/code/Remote/protocol.md / [[feedback_werift_h264_rtp]]
 *
 * - Unity = producer。rtc.offer を受けてサーバ側 PC を張り、届いた映像トラックの
 *   RTP を共有 producerTrack に書き込む。
 * - panel = consumer。rtc.offer を受けてサーバ側 PC に producerTrack を載せて answer する。
 * - シグナリングは非トリクル(localDescription に candidate を含めて返す)。リモートからの
 *   rtc.ice は addIceCandidate で受ける。
 */
import {
  MediaStreamTrack,
  RTCPeerConnection,
  RTCRtpCodecParameters,
  type RtpPacket,
} from "werift";
import type { Envelope, Role } from "../protocol.js";
import { send, type ClientConn } from "../bus/connection.js";
import type { SignalSink } from "../bus/hub.js";

const H264_CODEC = new RTCRtpCodecParameters({
  mimeType: "video/H264",
  clockRate: 90000,
  rtcpFeedback: [
    { type: "nack" },
    { type: "nack", parameter: "pli" },
    { type: "goog-remb" },
  ],
});

interface PeerState {
  pc: RTCPeerConnection;
}

export class MediaRelay implements SignalSink {
  /** Unity から中継された映像。consumer は全員これを購読する。 */
  private readonly producerTrack = new MediaStreamTrack({ kind: "video" });
  private readonly peers = new Map<ClientConn, PeerState>();

  /** 直近に映像 RTP が届いた時刻(監視用)。 */
  private lastRtpAt = 0;

  get hasVideo(): boolean {
    return this.lastRtpAt > 0 && Date.now() - this.lastRtpAt < 5000;
  }

  /** producerTrack に外部ソース(合成テスト映像など)から RTP を流し込む。 */
  pushRtp(rtp: RtpPacket): void {
    this.lastRtpAt = Date.now();
    this.producerTrack.writeRtp(rtp);
  }

  async handleSignal(role: Role, conn: ClientConn, env: Envelope): Promise<void> {
    switch (env.type) {
      case "rtc.offer":
        await this.onOffer(role, conn, env);
        break;
      case "rtc.ice":
        await this.onIce(conn, env);
        break;
      // rtc.answer はサーバが offer を出さない設計のため受け取らない。
    }
  }

  dropConnection(conn: ClientConn): void {
    const state = this.peers.get(conn);
    if (state) {
      void state.pc.close();
      this.peers.delete(conn);
    }
  }

  private async onOffer(role: Role, conn: ClientConn, env: Envelope): Promise<void> {
    const { sdp } = (env.payload ?? {}) as { sdp?: string };
    if (!sdp) {
      send(conn, "error", { code: "bad_message", message: "rtc.offer に sdp がありません" }, env.id);
      return;
    }

    // 既存 PC があれば張り直す。
    this.dropConnection(conn);

    const pc = new RTCPeerConnection({ codecs: { video: [H264_CODEC] } });
    this.peers.set(conn, { pc });

    if (role === "unity") {
      // Unity は映像送信側。届いたトラックの RTP を producerTrack に転送する。
      pc.onTrack.subscribe((track) => {
        track.onReceiveRtp.subscribe((rtp) => this.pushRtp(rtp));
      });
    } else {
      // panel は受信側。producerTrack を載せて返す。
      pc.addTrack(this.producerTrack);
    }

    await pc.setRemoteDescription({ type: "offer", sdp });
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    // 非トリクル: setLocalDescription 後の localDescription に candidate が含まれる。
    const local = pc.localDescription;
    if (!local) {
      send(conn, "error", { code: "internal", message: "localDescription 生成に失敗" }, env.id);
      return;
    }
    send(conn, "rtc.answer", { sdp: local.sdp }, env.id);
  }

  private async onIce(conn: ClientConn, env: Envelope): Promise<void> {
    const state = this.peers.get(conn);
    if (!state) return;
    const { candidate } = (env.payload ?? {}) as { candidate?: unknown };
    if (!candidate) return;
    try {
      await state.pc.addIceCandidate(candidate as never);
    } catch {
      // candidate 形式差異は致命的でないため握りつぶす。
    }
  }
}
