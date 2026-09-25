// Development-only test console for Idfy.Api. Renders a page for one endpoint (or the index), sends
// the request, and shows the URL, the request and the response of every call.
import { endpoints, groups, byId } from "./endpoints.js";

const MAX_BASE64 = 3_000_000; // Idfy:MaxBase64Length default
const DISPLAY_STRING_LIMIT = 200; // long strings (Base64) are shortened on screen only
const TOKEN = name => `@base64:${name}`;
const BASE_KEY = "idfy-test-base-url";

// ---------- small DOM helpers ----------

function h(tag, attrs = {}, ...children) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === undefined || v === null || v === false) continue;
    if (k === "class") el.className = v;
    else if (k.startsWith("on")) el.addEventListener(k.slice(2), v);
    else if (v === true) el.setAttribute(k, "");
    else el.setAttribute(k, v);
  }
  for (const c of children.flat()) {
    if (c === undefined || c === null || c === false) continue;
    el.append(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return el;
}

function copyButton(getText, label = "Copy") {
  const btn = h("button", { type: "button", class: "btn btn-small" }, label);
  btn.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(getText());
      btn.textContent = "Copied";
    } catch {
      btn.textContent = "Copy failed";
    }
    setTimeout(() => (btn.textContent = label), 1200);
  });
  return btn;
}

function formatBytes(n) {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / 1024 / 1024).toFixed(2)} MB`;
}

const shorten = s =>
  typeof s === "string" && s.length > DISPLAY_STRING_LIMIT
    ? `${s.slice(0, 60)}… (${s.length.toLocaleString()} chars)`
    : s;

// JSON pretty-printer with syntax highlighting; builds text nodes only (never innerHTML).
function jsonView(value, { shortenStrings = true } = {}) {
  const text = JSON.stringify(value, (_, v) => (shortenStrings ? shorten(v) : v), 2) ?? "";
  const pre = h("pre", { class: "code" });
  const re = /("(?:\\u[a-fA-F0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(?:true|false)\b|\bnull\b|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)/g;
  let last = 0;
  for (const m of text.matchAll(re)) {
    if (m.index > last) pre.append(text.slice(last, m.index));
    const tok = m[0];
    const cls = tok.startsWith('"') ? (m[2] ? "j-key" : "j-str") : tok === "null" ? "j-null" : /true|false/.test(tok) ? "j-bool" : "j-num";
    pre.append(h("span", { class: cls }, tok));
    last = m.index + tok.length;
  }
  pre.append(text.slice(last));
  return pre;
}

function textView(text) {
  return h("pre", { class: "code" }, text === "" ? "(empty body)" : text);
}

// ---------- base URL ----------

function getBaseUrl() {
  try {
    return localStorage.getItem(BASE_KEY) || location.origin;
  } catch {
    return location.origin;
  }
}

function setBaseUrl(v) {
  try {
    if (!v || v === location.origin) localStorage.removeItem(BASE_KEY);
    else localStorage.setItem(BASE_KEY, v);
  } catch { /* storage unavailable: keep for this page only */ }
}

function topBar(state) {
  const input = h("input", { class: "input base-input", value: state.baseUrl, spellcheck: "false", "aria-label": "API base URL" });
  input.addEventListener("change", () => {
    state.baseUrl = input.value.trim().replace(/\/+$/, "") || location.origin;
    input.value = state.baseUrl;
    setBaseUrl(state.baseUrl);
    state.onBaseChange?.();
  });
  return h("header", { class: "topbar" },
    h("a", { class: "brand", href: "./" }, "Idfy.Api ", h("span", { class: "brand-sub" }, "test console")),
    h("label", { class: "base" }, h("span", { class: "base-label" }, "API base URL"), input));
}

// ---------- index page ----------

export function mountIndex() {
  const state = { baseUrl: getBaseUrl() };
  document.title = "Idfy.Api test console";
  const main = h("main", { class: "wrap" },
    h("h1", {}, "Idfy.Api test console"),
    h("p", { class: "lede" },
      "One page per endpoint. Each page builds the request, sends it to this API, and shows the URL, the request and the response. ",
      "Available in the Development environment only."),
    h("div", { class: "notice" },
      h("strong", {}, "Real calls. "),
      "Requests go to the IDfy account configured on this server and can use IDfy credits. Use test documents."),
    ...groups.map(([gid, gtitle]) => {
      const items = endpoints.filter(e => e.group === gid);
      return h("section", { class: "group" },
        h("h2", {}, gtitle),
        h("ul", { class: "endpoint-list" }, items.map(e =>
          h("li", {},
            h("a", { class: "endpoint-link", href: `${e.id}.html` },
              h("span", { class: `method m-${e.method.toLowerCase()}` }, e.method),
              h("code", { class: "path" }, e.path),
              h("span", { class: "endpoint-title" }, e.title))))));
    }));
  document.body.append(topBar(state), main);
}

// ---------- endpoint page ----------

export function mountEndpoint(id) {
  const ep = byId[id];
  if (!ep) {
    document.body.append(h("main", { class: "wrap" }, h("p", {}, `Unknown endpoint: ${id}`)));
    return;
  }

  const state = {
    baseUrl: getBaseUrl(),
    values: {},        // field name -> string value from the form
    base64: {},        // image field name -> { data, name, type, size, width, height }
    files: {},         // upload field name -> File (+ width/height)
    rawEdited: false,  // JSON body edited by hand
    polling: null,
    history: [],
  };

  document.title = `${ep.method} ${ep.path} · Idfy.Api test`;

  const bodyEditor = h("textarea", { class: "input body-editor", spellcheck: "false", rows: "12", readonly: true, "aria-label": "Request body" });
  const bodyError = h("p", { class: "field-error", hidden: true });
  const urlPreview = h("code", { class: "url-preview" });
  const exchanges = h("div", { class: "exchanges" },
    h("p", { class: "empty" }, "No requests yet. Fill in the form and press Send."));
  const sendBtn = h("button", { type: "submit", class: "btn btn-primary" }, "Send");
  const pollBtn = ep.kind === "get" && ep.fields.some(f => f.route)
    ? h("button", { type: "button", class: "btn", onclick: () => togglePolling() }, "Poll until done")
    : null;

  // ----- fields -----

  const fieldEls = ep.fields.map(f => renderField(f));

  function renderField(f) {
    const label = f.label ?? f.name;
    const idAttr = `f-${f.name}`;
    const help = f.help ? h("p", { class: "help" }, f.help) : null;
    const req = f.required ? h("span", { class: "req", title: "Required" }, "required") : h("span", { class: "opt" }, "optional");
    const head = h("div", { class: "field-head" }, h("label", { for: idAttr }, h("code", {}, label)), req);
    const set = v => { state.values[f.name] = v; refresh(); };

    if (f.default !== undefined) state.values[f.name] = f.default;

    if (f.type === "bool" || f.type === "select") {
      const options = f.type === "bool" ? ["true", "false"] : f.options;
      const select = h("select", { id: idAttr, class: "input", onchange: e => set(e.target.value) },
        h("option", { value: "" }, "(omit)"),
        options.map(o => h("option", { value: o, selected: state.values[f.name] === o }, o)));
      return { f, el: h("div", { class: "field" }, head, select, help), setValue: v => { select.value = v ?? ""; state.values[f.name] = select.value; } };
    }

    if (f.type === "json") {
      const ta = h("textarea", { id: idAttr, class: "input", rows: "3", spellcheck: "false", placeholder: '{"key": true}', oninput: e => set(e.target.value) });
      return { f, el: h("div", { class: "field" }, head, ta, help), setValue: v => { ta.value = v ?? ""; state.values[f.name] = ta.value; } };
    }

    if (f.type === "file") {
      const info = h("div", { class: "file-info" });
      const input = h("input", { id: idAttr, type: "file", accept: "image/*", class: "input-file" });
      input.addEventListener("change", async () => {
        const file = input.files[0];
        info.replaceChildren();
        if (!file) { delete state.files[f.name]; refresh(); return; }
        const dims = await imageSize(file);
        state.files[f.name] = { file, ...dims };
        info.append(fileSummary(file, dims, (file.size + 2) / 3 * 4));
        refresh();
      });
      const clear = h("button", { type: "button", class: "btn btn-small", onclick: () => { input.value = ""; input.dispatchEvent(new Event("change")); } }, "Clear");
      return { f, el: h("div", { class: "field" }, head, h("div", { class: "row" }, input, clear), info, help), setValue: () => {} };
    }

    if (f.type === "image") {
      const input = h("input", { id: idAttr, class: "input", spellcheck: "false", placeholder: "https://… or Base64" });
      const info = h("div", { class: "file-info" });
      const picker = h("input", { type: "file", accept: "image/*", hidden: true });
      input.addEventListener("input", () => {
        delete state.base64[f.name];
        info.replaceChildren();
        input.readOnly = false;
        set(input.value.trim());
      });
      picker.addEventListener("change", async () => {
        const file = picker.files[0];
        if (!file) return;
        const [data, dims] = await Promise.all([readBase64(file), imageSize(file)]);
        state.base64[f.name] = { data, name: file.name, type: file.type, size: file.size, ...dims };
        input.value = `[Base64 of ${file.name}]`;
        input.readOnly = true;
        info.replaceChildren(fileSummary(file, dims, data.length));
        set(TOKEN(f.name));
        picker.value = "";
      });
      const choose = h("button", { type: "button", class: "btn btn-small", onclick: () => picker.click() }, "Use a file as Base64…");
      const clear = h("button", { type: "button", class: "btn btn-small", onclick: () => { input.readOnly = false; input.value = ""; input.dispatchEvent(new Event("input")); } }, "Clear");
      return {
        f,
        el: h("div", { class: "field" }, head, input, h("div", { class: "row" }, choose, clear, picker), info, help),
        setValue: v => { delete state.base64[f.name]; info.replaceChildren(); input.readOnly = false; input.value = v ?? ""; state.values[f.name] = input.value; },
      };
    }

    // text, date, guid
    const input = h("input", {
      id: idAttr, class: "input", spellcheck: "false",
      placeholder: f.type === "date" ? "YYYY-MM-DD" : f.type === "guid" ? "00000000-0000-0000-0000-000000000000" : "",
      value: f.route ? new URLSearchParams(location.search).get(f.name) ?? "" : "",
      oninput: e => set(e.target.value.trim()),
    });
    if (f.route) state.values[f.name] = input.value;
    const extra = f.type === "guid"
      ? h("button", { type: "button", class: "btn btn-small", onclick: () => { input.value = crypto.randomUUID(); set(input.value); } }, "Generate")
      : null;
    return { f, el: h("div", { class: "field" }, head, extra ? h("div", { class: "row" }, input, extra) : input, help), setValue: v => { input.value = v ?? ""; state.values[f.name] = input.value; } };
  }

  // ----- build the request -----

  function buildUrl() {
    let path = ep.path;
    for (const f of ep.fields.filter(f => f.route))
      path = path.replace(`{${f.name}}`, encodeURIComponent(state.values[f.name] ?? ""));
    return state.baseUrl + path;
  }

  function buildJsonBody() {
    const body = {};
    const errors = [];
    for (const f of ep.fields) {
      const v = state.values[f.name];
      if (v === undefined || v === "") continue;
      if (f.type === "bool") body[f.name] = v === "true";
      else if (f.type === "json") {
        try { body[f.name] = JSON.parse(v); } catch { errors.push(`${f.name} is not valid JSON; it is left out of the body.`); }
      } else body[f.name] = v;
    }
    return { body, errors };
  }

  function refresh() {
    urlPreview.textContent = `${ep.method} ${buildUrl()}`;
    if (ep.kind !== "json" || state.rawEdited) return;
    const { body, errors } = buildJsonBody();
    bodyEditor.value = JSON.stringify(body, null, 2);
    bodyError.hidden = errors.length === 0;
    bodyError.textContent = errors.join(" ");
  }

  // Replaces "@base64:field" tokens with the real Base64 (only the send/copy path sees the full data).
  function resolveTokens(text) {
    return text.replace(/"@base64:([A-Za-z0-9]+)"/g, (m, name) => (state.base64[name] ? `"${state.base64[name].data}"` : m));
  }

  function snapshotRequest() {
    const url = buildUrl();
    if (ep.kind === "json") {
      const raw = bodyEditor.value;
      const sendText = resolveTokens(raw);
      let displayBody;
      try { displayBody = { json: JSON.parse(sendText) }; } catch { displayBody = { text: raw }; }
      return {
        url, method: ep.method,
        headers: { "Content-Type": "application/json" },
        body: sendText, displayBody,
        size: new Blob([sendText]).size,
        curl: `curl -X ${ep.method} '${url}' \\\n  -H 'Content-Type: application/json' \\\n  --data-binary '${sendText.replaceAll("'", "'\\''")}'`,
      };
    }
    if (ep.kind === "upload") {
      const form = new FormData();
      const parts = [];
      const curlParts = [];
      for (const f of ep.fields) {
        if (f.type === "file") {
          const entry = state.files[f.name];
          if (!entry) continue;
          form.append(f.name, entry.file, entry.file.name);
          parts.push({ name: f.name, file: entry.file.name, type: entry.file.type || "(none)", size: entry.file.size, width: entry.width, height: entry.height });
          curlParts.push(`-F '${f.name}=@${entry.file.name};type=${entry.file.type}'`);
        } else {
          const v = state.values[f.name];
          if (v === undefined || v === "") continue;
          form.append(f.name, v);
          parts.push({ name: f.name, value: v });
          curlParts.push(`-F '${f.name}=${v}'`);
        }
      }
      return {
        url, method: ep.method,
        headers: { "Content-Type": "multipart/form-data; boundary=… (set by the browser)" },
        body: form, parts,
        size: parts.reduce((n, p) => n + (p.size ?? String(p.value).length), 0),
        curl: [`curl -X ${ep.method} '${url}'`, ...curlParts].join(" \\\n  "),
      };
    }
    return { url, method: ep.method, headers: {}, body: undefined, curl: `curl '${url}'` };
  }

  // ----- send -----

  async function send() {
    const req = snapshotRequest();
    const entry = { req, started: new Date(), res: null, error: null };
    const card = renderExchange(entry);
    exchanges.querySelector(".empty")?.remove();
    exchanges.prepend(card.el);
    while (exchanges.children.length > 20) exchanges.lastElementChild.remove();

    const t0 = performance.now();
    try {
      const response = await fetch(req.url, {
        method: req.method,
        headers: ep.kind === "json" ? req.headers : undefined,
        body: req.body,
        cache: "no-store",
      });
      const text = await response.text();
      const headers = [...response.headers.entries()];
      let json;
      try { json = text ? JSON.parse(text) : undefined; } catch { json = undefined; }
      entry.res = { status: response.status, statusText: response.statusText, headers, text, json, ms: Math.round(performance.now() - t0) };
    } catch (err) {
      entry.error = { message: String(err?.message ?? err), ms: Math.round(performance.now() - t0) };
    }
    card.fill(entry);
    return entry;
  }

  // ----- poll until done (async poll pages) -----

  async function togglePolling() {
    if (state.polling) { state.polling.stop = true; return; }
    const run = { stop: false };
    state.polling = run;
    pollBtn.textContent = "Stop polling";
    sendBtn.disabled = true;
    try {
      let delay = 2000;
      for (let i = 0; i < 40 && !run.stop; i++) {
        const entry = await send();
        if (entry.error || entry.res.status !== 202) break;
        await new Promise(r => setTimeout(r, delay));
        delay = Math.min(delay * 2, 15000);
      }
    } finally {
      state.polling = null;
      pollBtn.textContent = "Poll until done";
      sendBtn.disabled = false;
    }
  }

  // ----- exchange card: URL, request, response -----

  function renderExchange(entry) {
    const { req } = entry;
    const statusEl = h("span", { class: "status pending" }, "Sending…");
    const time = h("span", { class: "muted" }, entry.started.toLocaleTimeString());
    const responseBox = h("div", { class: "section-body" }, h("p", { class: "muted" }, "Waiting for the response…"));

    const requestBody = ep.kind === "json"
      ? (req.displayBody.json !== undefined ? jsonView(req.displayBody.json) : textView(req.displayBody.text))
      : ep.kind === "upload"
        ? (req.parts.length === 0 ? h("p", { class: "muted" }, "(no parts)") : partsTable(req.parts))
        : h("p", { class: "muted" }, "(no body)");

    const el = h("article", { class: "exchange" },
      h("div", { class: "exchange-head" }, statusEl, time),
      section("URL", h("div", { class: "row wrap-row" },
        h("code", { class: "url" }, h("span", { class: `method m-${req.method.toLowerCase()}` }, req.method), " ", req.url),
        copyButton(() => req.url))),
      section("Request",
        headersTable(Object.entries(req.headers)),
        req.size !== undefined ? h("p", { class: "muted small" }, `Body size: ${formatBytes(req.size)}${ep.kind === "json" && req.size > 1000 ? " (long Base64 strings are shortened on screen; the full value is sent)" : ""}`) : null,
        requestBody,
        h("div", { class: "row" }, copyButton(() => req.curl, "Copy as curl"))),
      section("Response", responseBox));

    function fill(entry) {
      responseBox.replaceChildren();
      if (entry.error) {
        statusEl.className = "status s-5xx";
        statusEl.textContent = "Network error";
        responseBox.append(
          h("p", {}, entry.error.message),
          h("p", { class: "muted small" }, "If the base URL points to another host, that server must allow this page's origin (CORS)."));
        return;
      }
      const r = entry.res;
      const band = r.status >= 500 ? "s-5xx" : r.status >= 400 ? "s-4xx" : r.status === 202 ? "s-202" : "s-2xx";
      statusEl.className = `status ${band}`;
      statusEl.textContent = `${r.status} ${r.statusText || ""}`.trim();
      time.textContent = `${entry.started.toLocaleTimeString()} · ${r.ms} ms`;

      const traceId = r.json?.traceId;
      const requestId = r.json?.request_id ?? r.json?.requestId;
      responseBox.append(
        h("div", { class: "resp-meta" },
          h("span", { class: `status ${band}` }, `${r.status} ${r.statusText || ""}`.trim()),
          h("span", { class: "muted" }, `${r.ms} ms`),
          traceId ? h("span", { class: "chip" }, "traceId ", h("code", {}, traceId), copyButton(() => traceId)) : null),
        h("details", { class: "headers" },
          h("summary", {}, `Response headers (${r.headers.length})`),
          headersTable(r.headers)),
        r.json !== undefined ? jsonView(r.json) : textView(r.text),
        h("div", { class: "row" },
          copyButton(() => (r.json !== undefined ? JSON.stringify(r.json, null, 2) : r.text), "Copy full body"),
          ep.pollPage && r.status === 200 && requestId
            ? h("a", { class: "btn btn-small btn-primary", href: `${ep.pollPage}.html?requestId=${encodeURIComponent(requestId)}` }, "Poll this request →")
            : null));
    }

    return { el, fill };
  }

  function section(title, ...children) {
    return h("section", { class: "section" }, h("h3", {}, title), h("div", { class: "section-body" }, ...children));
  }

  function headersTable(rows) {
    if (rows.length === 0) return h("p", { class: "muted small" }, "No headers set by the page.");
    return h("table", { class: "kv" }, h("tbody", {}, rows.map(([k, v]) => h("tr", {}, h("th", {}, k), h("td", {}, h("code", {}, v))))));
  }

  function partsTable(parts) {
    return h("table", { class: "kv" }, h("thead", {}, h("tr", {}, h("th", {}, "Part"), h("th", {}, "Value"))),
      h("tbody", {}, parts.map(p => h("tr", {},
        h("th", {}, h("code", {}, p.name)),
        h("td", {}, p.file
          ? `${p.file} · ${p.type} · ${formatBytes(p.size)}${p.width ? ` · ${p.width}×${p.height}px` : ""}`
          : h("code", {}, p.value))))));
  }

  // ----- layout -----

  const form = h("form", { class: "card form", novalidate: true },
    h("div", { class: "form-head" },
      h("h2", {}, "Request"),
      ep.sample ? h("button", { type: "button", class: "btn btn-small", onclick: fillSample }, "Fill sample") : null),
    h("p", { class: "url-line" }, urlPreview),
    fieldEls.length ? fieldEls.map(x => x.el) : h("p", { class: "muted" }, "This endpoint takes no input."),
    ep.kind === "json" ? h("div", { class: "field" },
      h("div", { class: "field-head" }, h("label", {}, "JSON body"),
        h("label", { class: "toggle" },
          h("input", { type: "checkbox", onchange: e => { state.rawEdited = e.target.checked; bodyEditor.readOnly = !e.target.checked; refresh(); } }),
          " Edit by hand")),
      bodyEditor,
      bodyError,
      h("p", { class: "help" }, "Built from the fields above. Tick “Edit by hand” to send anything (unknown or duplicate fields, bad JSON). ",
        "Values like ", h("code", {}, '"@base64:document"'), " are replaced with the chosen file's Base64 when sending.")) : null,
    h("div", { class: "actions" }, sendBtn, pollBtn,
      ep.kind === "upload" ? h("p", { class: "help" }, "Uploads over ~2.25 MB return 413; JPEG/PNG outside the resolution limits return 422.") : null));

  form.addEventListener("submit", e => { e.preventDefault(); if (!state.polling) send(); });

  function fillSample() {
    for (const x of fieldEls) {
      if (x.f.type === "file") continue;
      const v = ep.sample[x.f.name];
      x.setValue(v === undefined ? (x.f.default ?? "") : String(v));
    }
    refresh();
  }

  state.onBaseChange = refresh;

  document.body.append(
    topBar(state),
    h("main", { class: "wrap" },
      h("nav", { class: "crumbs" }, h("a", { href: "./" }, "All endpoints"), " / ", ep.title),
      h("div", { class: "title" },
        h("span", { class: `method big m-${ep.method.toLowerCase()}` }, ep.method),
        h("h1", {}, h("code", {}, ep.path))),
      h("p", { class: "lede" }, ep.description),
      ep.noIdfy ? null : h("div", { class: "notice" }, h("strong", {}, "Real call. "),
        "This goes to the IDfy account configured on this server and can use IDfy credits. Use test documents."),
      h("div", { class: "layout" },
        form,
        h("div", { class: "card" }, h("h2", {}, "Calls"), h("p", { class: "muted small" }, "Newest first. Each call shows the URL, the request and the response."), exchanges))));

  refresh();
}

// ---------- file helpers ----------

function readBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result).replace(/^data:[^,]*,/, ""));
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

function imageSize(file) {
  return new Promise(resolve => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => { resolve({ width: img.naturalWidth, height: img.naturalHeight, previewUrl: url }); };
    img.onerror = () => { URL.revokeObjectURL(url); resolve({}); };
    img.src = url;
  });
}

function fileSummary(file, dims, base64Length) {
  const over = base64Length > MAX_BASE64;
  return h("div", { class: "file-summary" },
    dims.previewUrl ? h("img", { src: dims.previewUrl, alt: "", class: "thumb" }) : null,
    h("div", {},
      h("div", {}, h("strong", {}, file.name)),
      h("div", { class: "muted small" },
        [file.type || "unknown type", formatBytes(file.size), dims.width ? `${dims.width}×${dims.height}px` : "dimensions unknown"].join(" · ")),
      h("div", { class: over ? "warn small" : "muted small" },
        `${Math.round(base64Length).toLocaleString()} chars as Base64${over ? " — over the 3,000,000 limit (expect 413)" : ""}`)));
}
