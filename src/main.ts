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
      <div class="title-stack">
        <div class="app-title">Blue Battery</div>
        <div id="summary" class="summary">正在读取</div>
      </div>
      <div id="connected-badge" class="topbar-count">0 BLE</div>
    </header>

    <section id="content" class="content" aria-live="polite"></section>

    <footer class="footer">
      <span id="footer-status">就绪</span>
      <div class="settings-area">
        <button
          id="settings"
          class="settings-button"
          type="button"
          title="设置"
          aria-label="设置"
          aria-haspopup="menu"
          aria-expanded="false"
        >
          <span class="fluent-icon settings-glyph" aria-hidden="true">&#xE713;</span>
        </button>
        <div id="settings-menu" class="settings-menu" role="menu" hidden>
          <button id="menu-refresh" class="menu-item" type="button" role="menuitem">
            <span class="fluent-icon menu-refresh-glyph" aria-hidden="true">&#xE72C;</span>
            <span>刷新</span>
          </button>
        </div>
      </div>
    </footer>
  </main>
`;

const summaryEl = document.querySelector<HTMLDivElement>("#summary")!;
const contentEl = document.querySelector<HTMLElement>("#content")!;
const connectedBadgeEl = document.querySelector<HTMLDivElement>("#connected-badge")!;
const footerStatusEl = document.querySelector<HTMLSpanElement>("#footer-status")!;
const settingsButton = document.querySelector<HTMLButtonElement>("#settings")!;
const settingsMenu = document.querySelector<HTMLDivElement>("#settings-menu")!;
const refreshMenuItem = document.querySelector<HTMLButtonElement>("#menu-refresh")!;

settingsButton.addEventListener("click", (event) => {
  event.stopPropagation();
  setSettingsMenuOpen(settingsMenu.hidden === true);
});

settingsMenu.addEventListener("click", (event) => {
  event.stopPropagation();
});

refreshMenuItem.addEventListener("click", () => {
  setSettingsMenuOpen(false);
  void refreshDevices();
});

document.addEventListener("click", () => {
  setSettingsMenuOpen(false);
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    setSettingsMenuOpen(false);
  }
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
  refreshMenuItem.disabled = true;
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
    refreshMenuItem.disabled = false;
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
  connectedBadgeEl.textContent = view.connectedBadge;
  footerStatusEl.textContent = view.footerStatus;
}

function setSettingsMenuOpen(open: boolean) {
  settingsMenu.hidden = !open;
  settingsButton.setAttribute("aria-expanded", String(open));
}
