// UnityFoundation の .unitypackage ビルダー（Unity 不要）。
//
// .unitypackage = gzip(tar)。各アセットを GUID 名ディレクトリに格納し、
//   <guid>/pathname    … 取り込み先パス（Assets/...）
//   <guid>/asset       … ファイル本体（フォルダは無し）
//   <guid>/asset.meta  … .meta 内容
// で構成する。
//
// 1 つのパッケージに全機能を入れつつ、トップフォルダ Assets/UnityFoundation/<機能>/ に
// 再配置しているので、Unity の Import 時にフォルダ単位で選択インストールできる。
//
// 使い方: リポジトリルートで `node tools/build-unitypackage.mjs`
//   → dist/UnityFoundation.unitypackage を生成。

import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";

const ROOT = process.cwd();
const OUT = path.join(ROOT, "dist", "UnityFoundation.unitypackage");

// 取り込み先フォルダ（GUID は固定。トップフォルダ分割の単位）。
const FOLDERS = {
  "Assets/UnityFoundation": "68a21873f2b841e7a53eaef0a16bd80f",
  "Assets/UnityFoundation/Melpomene": "3a5ed3f453b64439a736541e7e157b24",
  "Assets/UnityFoundation/Melpomene/Resources": "f4528115d2ba48119eabde5a0db1091b",
  "Assets/UnityFoundation/GameEvent": "7040a163ffbd49968ce51a5595f732cb",
  "Assets/UnityFoundation/GameEvent/Review": "ead4718113114babafe64c69bed92170",
  "Assets/UnityFoundation/Network": "d9192dcccc5c4f9da1a5484b0246dd9e",
  "Assets/UnityFoundation/Common": "459a4d993c894aaf81f312963a4c98f7",
};

// src（リポ内パス） → dst（取り込み先パス）。
const M = "unity/Assets/Scripts/BaseSystem/Foundation/Melpomene";
const G = "unity/Assets/Scripts/BaseSystem/GameEvent";
const N = "unity/Assets/Scripts/BaseSystem/Foundation/Network";
const FILES = [
  // Melpomene（ゲーム内バグ報告）
  [`${M}/MelpomeneRuntimeConfig.cs`, "Assets/UnityFoundation/Melpomene/MelpomeneRuntimeConfig.cs"],
  [`${M}/MelpomeneReportTicket.cs`, "Assets/UnityFoundation/Melpomene/MelpomeneReportTicket.cs"],
  [`${M}/MelpomeneIssueClient.cs`, "Assets/UnityFoundation/Melpomene/MelpomeneIssueClient.cs"],
  [`${M}/MelpomeneReporter.cs`, "Assets/UnityFoundation/Melpomene/MelpomeneReporter.cs"],
  [`${M}/MelpomeneRuntimeBootstrap.cs`, "Assets/UnityFoundation/Melpomene/MelpomeneRuntimeBootstrap.cs"],
  ["unity/Assets/Resources/MelpomeneRuntimeConfig.asset", "Assets/UnityFoundation/Melpomene/Resources/MelpomeneRuntimeConfig.asset"],
  // GameEvent（レビューダイアログ + AWS イベントログ）
  [`${G}/EventData.cs`, "Assets/UnityFoundation/GameEvent/EventData.cs"],
  [`${G}/EventTest.cs`, "Assets/UnityFoundation/GameEvent/EventTest.cs"],
  [`${G}/GameEventRecorder.cs`, "Assets/UnityFoundation/GameEvent/GameEventRecorder.cs"],
  [`${G}/ReviewStar.cs`, "Assets/UnityFoundation/GameEvent/ReviewStar.cs"],
  [`${G}/ReviewTest.cs`, "Assets/UnityFoundation/GameEvent/ReviewTest.cs"],
  [`${G}/ReviewWindow.cs`, "Assets/UnityFoundation/GameEvent/ReviewWindow.cs"],
  // Review プレハブ（GameEvent/Review/ に再配置）
  ["unity/Assets/Prefabs/Review/Review.prefab", "Assets/UnityFoundation/GameEvent/Review/Review.prefab"],
  ["unity/Assets/Prefabs/Review/Star.prefab", "Assets/UnityFoundation/GameEvent/Review/Star.prefab"],
  // Network（HTTP クライアント層）
  [`${N}/HTTPRequest.cs`, "Assets/UnityFoundation/Network/HTTPRequest.cs"],
  [`${N}/SequenceBridge.cs`, "Assets/UnityFoundation/Network/SequenceBridge.cs"],
  [`${N}/TaskRequestWorker.cs`, "Assets/UnityFoundation/Network/TaskRequestWorker.cs"],
  [`${N}/WebRequest.cs`, "Assets/UnityFoundation/Network/WebRequest.cs"],
  // Common（小ヘルパ）
  ["unity/Assets/Scripts/BaseSystem/Dynamic/BuildState.cs", "Assets/UnityFoundation/Common/BuildState.cs"],
  ["unity/Assets/Scripts/BaseSystem/Foundation/Utility.cs", "Assets/UnityFoundation/Common/Utility.cs"],
];

// 再配置に伴う最小のソース書き換え（プレハブの Addressables アドレス追従）。
const PATCHES = {
  "Assets/UnityFoundation/GameEvent/ReviewWindow.cs": [
    ["Assets/Prefabs/Review/Review.prefab", "Assets/UnityFoundation/GameEvent/Review/Review.prefab"],
  ],
};

function folderMeta(guid) {
  return (
    "fileFormatVersion: 2\n" +
    `guid: ${guid}\n` +
    "folderAsset: yes\n" +
    "DefaultImporter:\n" +
    "  externalObjects: {}\n" +
    "  userData:\n" +
    "  assetBundleName:\n" +
    "  assetBundleVariant:\n"
  );
}

function readMetaGuid(metaPath) {
  const txt = fs.readFileSync(metaPath, "utf8");
  const m = txt.match(/^guid:\s*([0-9a-fA-F]{32})/m);
  if (!m) throw new Error(`guid not found in ${metaPath}`);
  return { guid: m[1], text: txt };
}

// ---- 最小 tar(ustar) ライタ ----
function octal(n, len) {
  return n.toString(8).padStart(len - 1, "0") + "\0";
}

function tarHeader(name, size, typeflag) {
  const h = Buffer.alloc(512, 0);
  h.write(name, 0, "utf8"); // name(100)
  h.write("0000" + (typeflag === "5" ? "755" : "644") + "\0", 100); // mode(8)
  h.write("0000000\0", 108); // uid(8)
  h.write("0000000\0", 116); // gid(8)
  h.write(octal(size, 12), 124); // size(12)
  h.write(octal(0, 12), 136); // mtime(12) = 0（決定的）
  h.write("        ", 148); // chksum 仮（空白8）
  h.write(typeflag, 156); // typeflag(1)
  h.write("ustar\0", 257); // magic(6)
  h.write("00", 263); // version(2)
  let sum = 0;
  for (let i = 0; i < 512; i++) sum += h[i];
  h.write(sum.toString(8).padStart(6, "0") + "\0 ", 148); // chksum(8)
  return h;
}

const chunks = [];
function addEntry(name, content, typeflag) {
  const body = typeflag === "5" ? Buffer.alloc(0) : Buffer.isBuffer(content) ? content : Buffer.from(content, "utf8");
  chunks.push(tarHeader(name, body.length, typeflag));
  if (body.length > 0) {
    chunks.push(body);
    const pad = (512 - (body.length % 512)) % 512;
    if (pad) chunks.push(Buffer.alloc(pad, 0));
  }
}

function addAsset(guid, pathname, assetBuf, metaText) {
  addEntry(`${guid}/`, null, "5");
  addEntry(`${guid}/pathname`, pathname + "\n", "0");
  if (assetBuf !== null) addEntry(`${guid}/asset`, assetBuf, "0");
  addEntry(`${guid}/asset.meta`, metaText, "0");
}

// フォルダ（親→子の順）
for (const [folderPath, guid] of Object.entries(FOLDERS)) {
  addAsset(guid, folderPath, null, folderMeta(guid));
}

// ファイル
let count = 0;
for (const [src, dst] of FILES) {
  const srcAbs = path.join(ROOT, src);
  const metaAbs = srcAbs + ".meta";
  const { guid, text: metaText } = readMetaGuid(metaAbs);
  let assetBuf = fs.readFileSync(srcAbs);
  const patches = PATCHES[dst];
  if (patches) {
    let s = assetBuf.toString("utf8");
    for (const [from, to] of patches) {
      if (!s.includes(from)) throw new Error(`patch target not found in ${dst}: ${from}`);
      s = s.split(from).join(to);
    }
    assetBuf = Buffer.from(s, "utf8");
    console.log(`  patched: ${dst}`);
  }
  addAsset(guid, dst, assetBuf, metaText);
  count++;
}

// tar 終端（512 ゼロブロック x2）
chunks.push(Buffer.alloc(1024, 0));

const tar = Buffer.concat(chunks);
const gz = zlib.gzipSync(tar, { level: 9 });

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, gz);
console.log(`\nWrote ${OUT}`);
console.log(`  folders: ${Object.keys(FOLDERS).length}, files: ${count}, size: ${(gz.length / 1024).toFixed(1)} KB`);
