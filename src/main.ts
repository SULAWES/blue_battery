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
let startupEnabled = false;

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
    <section id="diagnostics-panel" class="diagnostics-panel" aria-label="诊断信息" hidden>
      <div class="diagnostics-header">
        <span>诊断信息</span>
        <button id="diagnostics-close" class="diagnostics-close" type="button" title="关闭" aria-label="关闭诊断信息">
          <span class="fluent-icon" aria-hidden="true">&#xE711;</span>
        </button>
      </div>
      <pre id="diagnostics-report" class="diagnostics-report"></pre>
    </section>

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
          <button id="menu-startup" class="menu-item" type="button" role="menuitemcheckbox" aria-checked="false">
            <span class="fluent-icon menu-check-glyph" aria-hidden="true">&#xE73E;</span>
            <span>开机自启动</span>
          </button>
          <button id="menu-refresh" class="menu-item" type="button" role="menuitem">
            <span class="fluent-icon menu-refresh-glyph" aria-hidden="true">&#xE72C;</span>
            <span>刷新</span>
          </button>
          <button id="menu-diagnostics" class="menu-item" type="button" role="menuitem">
            <span class="fluent-icon menu-diagnostics-glyph" aria-hidden="true">&#xE946;</span>
            <span>诊断信息</span>
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
const startupMenuItem = document.querySelector<HTMLButtonElement>("#menu-startup")!;
const refreshMenuItem = document.querySelector<HTMLButtonElement>("#menu-refresh")!;
const diagnosticsMenuItem = document.querySelector<HTMLButtonElement>("#menu-diagnostics")!;
const diagnosticsPanel = document.querySelector<HTMLElement>("#diagnostics-panel")!;
const diagnosticsReportEl = document.querySelector<HTMLPreElement>("#diagnostics-report")!;
const diagnosticsCloseButton = document.querySelector<HTMLButtonElement>("#diagnostics-close")!;

settingsButton.addEventListener("click", (event) => {
  event.stopPropagation();
  setSettingsMenuOpen(settingsMenu.hidden === true);
  void refreshStartupState();
});

settingsMenu.addEventListener("click", (event) => {
  event.stopPropagation();
});

refreshMenuItem.addEventListener("click", () => {
  setSettingsMenuOpen(false);
  void refreshDevices();
});

startupMenuItem.addEventListener("click", () => {
  void setStartupEnabled(!startupEnabled);
});

diagnosticsMenuItem.addEventListener("click", () => {
  setSettingsMenuOpen(false);
  void showDiagnostics();
});

diagnosticsCloseButton.addEventListener("click", () => {
  setDiagnosticsOpen(false);
});

document.addEventListener("click", () => {
  setSettingsMenuOpen(false);
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    const hasInnerFlyoutOpen = settingsMenu.hidden === false || diagnosticsPanel.hidden === false;

    setSettingsMenuOpen(false);
    setDiagnosticsOpen(false);

    if (!hasInnerFlyoutOpen) {
      void hidePanel();
    }
  }
});

void listen<RefreshResult>("devices-refreshed", (event) => {
  lastResult = event.payload;
  render(event.payload);
});

void refreshDevices();
void refreshStartupState();
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

async function refreshStartupState() {
  try {
    setStartupState(await invoke<boolean>("get_startup_enabled"));
  } catch {
    setStartupState(false);
  }
}

async function setStartupEnabled(enabled: boolean) {
  startupMenuItem.disabled = true;

  try {
    setStartupState(await invoke<boolean>("set_startup_enabled", { enabled }));
    footerStatusEl.textContent = startupEnabled ? "已启用开机自启动" : "已关闭开机自启动";
  } catch {
    footerStatusEl.textContent = "更新开机自启动失败";
  } finally {
    startupMenuItem.disabled = false;
  }
}

function setStartupState(enabled: boolean) {
  startupEnabled = enabled;
  startupMenuItem.setAttribute("aria-checked", String(enabled));
}

async function showDiagnostics() {
  setDiagnosticsOpen(true);
  diagnosticsReportEl.textContent = "正在读取诊断信息...";

  try {
    diagnosticsReportEl.textContent = await invoke<string>("get_diagnostics_report");
  } catch (error) {
    diagnosticsReportEl.textContent = error instanceof Error ? error.message : String(error);
  }
}

function setDiagnosticsOpen(open: boolean) {
  diagnosticsPanel.hidden = !open;
}

async function hidePanel() {
  try {
    await invoke<void>("hide_panel");
  } catch {
    footerStatusEl.textContent = "关闭面板失败";
  }
}
