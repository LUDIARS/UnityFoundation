// Foundation Debug Panel — 制御 WS + WebRTC 受信 + Data Studio
// 仕様: spec/code/Remote/protocol.md / data-studio.md

const $ = (id) => document.getElementById(id);
const V = 1;

// ---------------- 制御 WebSocket ----------------
let ws = null;
let pendingReqId = 0;
const handlers = new Map(); // type -> fn(payload, env)

function wsUrl() {
  const proto = location.protocol === "https:" ? "wss:" : "ws:";
  return `${proto}//${location.host}/ws`;
}

function connect() {
  ws = new WebSocket(wsUrl());
  ws.onopen = () => {
    setDot("dot-ws", true);
    sendMsg("hello", { role: "panel", name: "panel" });
    sendMsg("command.list.request", {});
  };
  ws.onclose = () => {
    setDot("dot-ws", false);
    setDot("dot-unity", false);
    setTimeout(connect, 1500);
  };
  ws.onmessage = (ev) => {
    let env;
    try { env = JSON.parse(ev.data); } catch { return; }
    const fn = handlers.get(env.type);
    if (fn) fn(env.payload ?? {}, env);
  };
}

function sendMsg(type, payload, id) {
  if (!ws || ws.readyState !== WebSocket.OPEN) return null;
  const reqId = id ?? `r${++pendingReqId}`;
  ws.send(JSON.stringify({ v: V, type, id: reqId, ts: Date.now(), payload }));
  return reqId;
}

function on(type, fn) { handlers.set(type, fn); }
function setDot(id, on) { $(id).classList.toggle("on", !!on); }

// ---------------- ログ / テレメトリ ----------------
function logLine(level, message) {
  const el = $("log");
  const div = document.createElement("div");
  div.className = level;
  const t = new Date().toLocaleTimeString();
  div.textContent = `[${t}] ${message}`;
  el.appendChild(div);
  el.scrollTop = el.scrollHeight;
  while (el.childElementCount > 300) el.removeChild(el.firstChild);
}

on("welcome", (p) => { setDot("dot-unity", p.unityConnected); logLine("log", `接続: ${p.sessionId}`); });
on("log", (p) => {
  logLine(p.level ?? "log", p.message ?? "");
  if (/Unity が接続/.test(p.message)) setDot("dot-unity", true);
  if (/Unity が切断/.test(p.message)) setDot("dot-unity", false);
});
on("telemetry", (p) => {
  $("m-fps").textContent = Math.round(p.fps ?? 0);
  $("m-mem").textContent = Math.round(p.memoryMB ?? 0);
  $("m-scene").textContent = p.scene ?? "-";
  $("m-time").textContent = Math.round(p.time ?? 0);
  setDot("dot-unity", true);
});
on("error", (p, env) => logLine("error", `error[${p.code}] ${p.message} ${env.id ?? ""}`));

// ---------------- 遠隔操作 ----------------
on("command.list", (p) => renderCommands(p.commands ?? []));
on("command.result", (p) => logLine(p.ok ? "log" : "error", `cmd ${p.name}: ${p.ok ? "ok" : "fail"} ${p.message ?? ""}`));

function renderCommands(commands) {
  const list = $("cmd-list");
  list.innerHTML = "";
  if (commands.length === 0) { list.innerHTML = '<div class="muted">コマンドなし (Unity 未接続?)</div>'; return; }
  for (const c of commands) {
    const div = document.createElement("div");
    div.className = "cmd";
    if (c.kind === "toggle") {
      const cb = document.createElement("input");
      cb.type = "checkbox";
      cb.onchange = () => sendMsg("command.invoke", { name: c.name, value: cb.checked });
      div.appendChild(cb);
    }
    const name = document.createElement("span");
    name.className = "name grow";
    name.textContent = c.name;
    div.appendChild(name);
    const desc = document.createElement("span");
    desc.className = "desc";
    desc.textContent = c.description ?? "";
    div.appendChild(desc);
    if (c.kind !== "toggle") {
      const btn = document.createElement("button");
      btn.textContent = "実行";
      btn.onclick = () => sendMsg("command.invoke", { name: c.name });
      div.appendChild(btn);
    }
    list.appendChild(div);
  }
}

$("btn-refresh-cmd").onclick = () => sendMsg("command.list.request", {});
$("btn-scene").onclick = () => {
  const scene = $("scene-name").value.trim();
  if (scene) sendMsg("scene.load", { scene });
};

// ---------------- WebRTC 受信 ----------------
let pc = null;
on("rtc.answer", async (p) => { if (pc) await pc.setRemoteDescription({ type: "answer", sdp: p.sdp }); });

async function startWatch() {
  stopWatch();
  pc = new RTCPeerConnection();
  pc.addTransceiver("video", { direction: "recvonly" });
  pc.ontrack = (ev) => { $("video").srcObject = ev.streams[0] ?? new MediaStream([ev.track]); $("video-state").textContent = "受信中"; };
  pc.onicecandidate = (ev) => { if (ev.candidate) sendMsg("rtc.ice", { candidate: ev.candidate.toJSON() }); };
  pc.onconnectionstatechange = () => { $("video-state").textContent = pc.connectionState; };
  const offer = await pc.createOffer();
  await pc.setLocalDescription(offer);
  sendMsg("rtc.offer", { sdp: offer.sdp });
}
function stopWatch() {
  if (pc) { pc.close(); pc = null; }
  $("video").srcObject = null;
  $("video-state").textContent = "未接続";
}
$("btn-watch").onclick = () => startWatch().catch((e) => logLine("error", `rtc: ${e.message}`));
$("btn-stop-watch").onclick = stopWatch;

// ---------------- Data Studio ----------------
async function api(method, path, body) {
  const res = await fetch(path, {
    method,
    headers: body ? { "content-type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(json.message || json.error || res.statusText);
  return json;
}

async function reloadSheets() {
  const { sheets } = await api("GET", "/api/sheets");
  const sel = $("sheet-select");
  const cur = sel.value;
  sel.innerHTML = "";
  for (const name of sheets) {
    const opt = document.createElement("option");
    opt.value = opt.textContent = name;
    sel.appendChild(opt);
  }
  if (sheets.includes(cur)) sel.value = cur;
  if (sel.value) loadSheet(sel.value);
  else $("sheet-editor").innerHTML = '<div class="muted">シートがありません。新規作成してください。</div>';
}

let current = null; // { schema, rows }

async function loadSheet(name) {
  current = await api("GET", `/api/sheets/${name}`);
  renderEditor();
}

function renderEditor() {
  const host = $("sheet-editor");
  host.innerHTML = "";
  if (!current) return;
  const { schema, rows } = current;

  const meta = document.createElement("div");
  meta.className = "row";
  meta.innerHTML = `<span class="muted grow">${schema.name} — v${schema.version} — ${schema.columns.length} 列 / ${rows.length} 行</span>`;
  host.appendChild(meta);
  host.appendChild(document.createElement("div")).className = "spacer";

  const table = document.createElement("table");
  const thead = document.createElement("tr");
  for (const c of schema.columns) {
    const th = document.createElement("th");
    th.textContent = `${c.key}:${c.type}`;
    thead.appendChild(th);
  }
  thead.appendChild(document.createElement("th"));
  table.appendChild(thead);

  rows.forEach((row, ri) => table.appendChild(rowTr(schema, row, ri)));
  host.appendChild(table);

  const ctl = document.createElement("div");
  ctl.className = "row";
  ctl.style.marginTop = "8px";
  ctl.innerHTML = `
    <button id="ds-addrow" class="ghost">+ 行追加</button>
    <button id="ds-save">保存</button>
    <button id="ds-publish">Publish → Unity</button>
    <button id="ds-addcol" class="ghost">+ 列追加</button>`;
  host.appendChild(ctl);

  $("ds-addrow").onclick = () => { current.rows.push(Object.fromEntries(schema.columns.map((c) => [c.key, c.default ?? ""]))); renderEditor(); };
  $("ds-save").onclick = saveRows;
  $("ds-publish").onclick = publishSheet;
  $("ds-addcol").onclick = addColumn;
}

function rowTr(schema, row, ri) {
  const tr = document.createElement("tr");
  for (const c of schema.columns) {
    const td = document.createElement("td");
    const input = document.createElement("input");
    input.value = row[c.key] ?? "";
    if (c.type === "int" || c.type === "float") input.type = "number";
    input.oninput = () => { current.rows[ri][c.key] = input.value; };
    td.appendChild(input);
    tr.appendChild(td);
  }
  const del = document.createElement("td");
  const btn = document.createElement("button");
  btn.className = "ghost";
  btn.textContent = "×";
  btn.onclick = () => { current.rows.splice(ri, 1); renderEditor(); };
  del.appendChild(btn);
  tr.appendChild(del);
  return tr;
}

async function saveRows() {
  try {
    current = await api("PUT", `/api/sheets/${current.schema.name}/rows`, { rows: current.rows });
    renderEditor();
    logLine("log", `保存: ${current.schema.name}`);
  } catch (e) { logLine("error", `保存失敗: ${e.message}`); }
}

async function publishSheet() {
  try {
    const r = await api("POST", `/api/sheets/${current.schema.name}/publish`);
    logLine("log", `publish: ${r.name} v${r.version} (Unity に reload 通知)`);
    await loadSheet(current.schema.name);
  } catch (e) { logLine("error", `publish 失敗: ${e.message}`); }
}

async function addColumn() {
  const key = prompt("列名 (英数_):");
  if (!key) return;
  const type = prompt("型 (int/float/bool/string):", "string");
  if (!["int", "float", "bool", "string"].includes(type)) { logLine("error", "不正な型"); return; }
  current.schema.columns.push({ key, type });
  try {
    current = await api("PUT", `/api/sheets/${current.schema.name}`, current.schema);
    renderEditor();
  } catch (e) { logLine("error", `列追加失敗: ${e.message}`); }
}

$("btn-reload-sheets").onclick = () => reloadSheets().catch((e) => logLine("error", e.message));
$("sheet-select").onchange = (e) => loadSheet(e.target.value).catch((err) => logLine("error", err.message));
$("btn-new-sheet").onclick = async () => {
  const name = $("new-sheet-name").value.trim();
  if (!/^[A-Za-z0-9_]+$/.test(name)) { logLine("error", "英数_ のみ"); return; }
  const schema = { name, version: 1, key: "Id", columns: [{ key: "Id", type: "int" }, { key: "Name", type: "string" }] };
  try {
    await api("PUT", `/api/sheets/${name}`, schema);
    $("new-sheet-name").value = "";
    await reloadSheets();
    $("sheet-select").value = name;
    await loadSheet(name);
  } catch (e) { logLine("error", `作成失敗: ${e.message}`); }
};

// ---------------- 起動 ----------------
connect();
reloadSheets().catch((e) => logLine("error", e.message));
