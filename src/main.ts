import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { describePanelState, type RefreshResult } from "./panel_state.ts";
import {
  buildCommandErrorView,
  buildPanelView,
  buildTransientPanelView,
  type PanelView,
} from "./render.ts";
import "./styles.css";

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("Missing app root.");
}

let lastResult: RefreshResult | null = null;
let refreshing = false;

app.innerHTML = `
  <main class="shell">
    <header class="topbar">
      <div>
        <div class="app-title">Blue Battery</div>
        <div id="summary" class="summary">正在读取</div>
      </div>
      <button id="refresh" class="icon-button" type="button" title="刷新" aria-label="刷新">
        <span class="refresh-glyph" aria-hidden="true"></span>
      </button>
    </header>

    <section id="content" class="content" aria-live="polite"></section>

    <footer class="footer">
      <span id="timestamp">--</span>
      <span id="connected-count">0 个已连接 BLE 设备</span>
    </footer>
  </main>
`;

const summaryEl = document.querySelector<HTMLDivElement>("#summary")!;
const contentEl = document.querySelector<HTMLElement>("#content")!;
const timestampEl = document.querySelector<HTMLSpanElement>("#timestamp")!;
const connectedCountEl = document.querySelector<HTMLSpanElement>("#connected-count")!;
const refreshButton = document.querySelector<HTMLButtonElement>("#refresh")!;

refreshButton.addEventListener("click", () => {
  void refreshDevices();
});

void listen<RefreshResult>("devices-refreshed", (event) => {
  lastResult = event.payload;
  render(event.payload);
});

void refreshDevices();
window.setInterval(() => {
  void refreshDevices();
}, 60_000);

async function refreshDevices() {
  if (refreshing) {
    return;
  }

  refreshing = true;
  refreshButton.classList.add("is-loading");
  if (!lastResult) {
    applyPanelView(buildTransientPanelView(describePanelState(null, true)));
  }

  try {
    const result = await invoke<RefreshResult>("refresh_devices");
    lastResult = result;
    render(result);
  } catch (error) {
    renderError(error instanceof Error ? error.message : String(error));
  } finally {
    refreshing = false;
    refreshButton.classList.remove("is-loading");
  }
}

function render(result: RefreshResult) {
  applyPanelView(buildPanelView(result));
}

function renderError(message: string) {
  applyPanelView(buildCommandErrorView(message, lastResult));
}

function applyPanelView(view: PanelView) {
  summaryEl.textContent = view.summary;
  contentEl.innerHTML = view.contentHtml;
  timestampEl.textContent = view.timestamp;
  connectedCountEl.textContent = view.connectedCount;
}
