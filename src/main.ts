import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { describePanelState, type RefreshResult } from "./panel_state.ts";
import {
  buildCommandErrorView,
  buildPanelView,
  buildTransientPanelView,
  type PanelView,
} from "./render.ts";
import {
  DEFAULT_SETTINGS,
  LOW_BATTERY_THRESHOLDS,
  REFRESH_INTERVAL_SECONDS,
  nextNumberOption,
  type AppSettings,
} from "./settings.ts";
import "./styles.css";

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("Missing app root.");
}

let lastResult: RefreshResult | null = null;
let refreshing = false;
let startupEnabled = false;
let appSettings: AppSettings = DEFAULT_SETTINGS;
let autoRefreshTimer: number | undefined;

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
          <button id="menu-refresh" class="menu-item" type="button" role="menuitem">
            <span class="fluent-icon menu-refresh-glyph" aria-hidden="true">&#xE72C;</span>
            <span>刷新</span>
          </button>
          <button id="menu-refresh-interval" class="menu-item" type="button" role="menuitem">
            <span class="fluent-icon menu-interval-glyph" aria-hidden="true">&#xE916;</span>
            <span>刷新间隔：60秒</span>
          </button>
          <button id="menu-low-battery-status" class="menu-item" type="button" role="menuitemcheckbox" aria-checked="true">
            <span class="fluent-icon menu-low-battery-glyph" aria-hidden="true">&#xE7BA;</span>
            <span>低电量状态</span>
          </button>
          <button id="menu-low-battery-threshold" class="menu-item" type="button" role="menuitem">
            <span class="fluent-icon menu-threshold-glyph" aria-hidden="true">&#xE9D9;</span>
            <span>低电量阈值：20%</span>
          </button>
          <button id="menu-startup" class="menu-item" type="button" role="menuitemcheckbox" aria-checked="false">
            <span class="fluent-icon menu-startup-glyph" aria-hidden="true">&#xE7E8;</span>
            <span>开机自启动</span>
          </button>
          <button id="menu-clear-startup" class="menu-item" type="button" role="menuitem">
            <span class="fluent-icon menu-cleanup-glyph" aria-hidden="true">&#xE74D;</span>
            <span>清理开机自启动项</span>
          </button>
          <button id="menu-reset-settings" class="menu-item" type="button" role="menuitem">
            <span class="fluent-icon menu-reset-glyph" aria-hidden="true">&#xE777;</span>
            <span>重置设置</span>
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
const refreshMenuItem = document.querySelector<HTMLButtonElement>("#menu-refresh")!;
const refreshIntervalMenuItem = document.querySelector<HTMLButtonElement>("#menu-refresh-interval")!;
const lowBatteryStatusMenuItem = document.querySelector<HTMLButtonElement>("#menu-low-battery-status")!;
const lowBatteryThresholdMenuItem = document.querySelector<HTMLButtonElement>("#menu-low-battery-threshold")!;
const startupMenuItem = document.querySelector<HTMLButtonElement>("#menu-startup")!;
const clearStartupMenuItem = document.querySelector<HTMLButtonElement>("#menu-clear-startup")!;
const resetSettingsMenuItem = document.querySelector<HTMLButtonElement>("#menu-reset-settings")!;
const diagnosticsMenuItem = document.querySelector<HTMLButtonElement>("#menu-diagnostics")!;
const diagnosticsPanel = document.querySelector<HTMLElement>("#diagnostics-panel")!;
const diagnosticsReportEl = document.querySelector<HTMLPreElement>("#diagnostics-report")!;
const diagnosticsCloseButton = document.querySelector<HTMLButtonElement>("#diagnostics-close")!;

settingsButton.addEventListener("click", (event) => {
  event.stopPropagation();
  setSettingsMenuOpen(settingsMenu.hidden === true);
  void loadSettings();
  void refreshStartupState();
});

settingsMenu.addEventListener("click", (event) => {
  event.stopPropagation();
});

refreshMenuItem.addEventListener("click", () => {
  setSettingsMenuOpen(false);
  void refreshDevices();
});

refreshIntervalMenuItem.addEventListener("click", () => {
  void updateSettings(
    {
      refreshIntervalSeconds: nextNumberOption(
        appSettings.refreshIntervalSeconds,
        REFRESH_INTERVAL_SECONDS,
      ),
    },
    "已更新刷新间隔",
  );
});

lowBatteryStatusMenuItem.addEventListener("click", () => {
  void updateSettings(
    { lowBatteryStatusEnabled: !appSettings.lowBatteryStatusEnabled },
    "已更新低电量状态",
  );
});

lowBatteryThresholdMenuItem.addEventListener("click", () => {
  void updateSettings(
    {
      lowBatteryThreshold: nextNumberOption(
        appSettings.lowBatteryThreshold,
        LOW_BATTERY_THRESHOLDS,
      ),
    },
    "已更新低电量阈值",
  );
});

startupMenuItem.addEventListener("click", () => {
  void setStartupEnabled(!startupEnabled);
});

clearStartupMenuItem.addEventListener("click", () => {
  void clearStartupEntry();
});

resetSettingsMenuItem.addEventListener("click", () => {
  void resetSettings();
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

void initialize();

async function initialize() {
  await loadSettings();
  await refreshStartupState();
  await refreshDevices();
  scheduleAutoRefresh();
}

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

async function loadSettings() {
  try {
    applySettings(await invoke<AppSettings>("get_settings"));
  } catch {
    applySettings(DEFAULT_SETTINGS);
    footerStatusEl.textContent = "读取设置失败，使用默认设置";
  }
}

async function updateSettings(patch: Partial<AppSettings>, successMessage: string) {
  setSettingsControlsDisabled(true);

  try {
    const settings = await invoke<AppSettings>("update_settings", {
      settings: { ...appSettings, ...patch },
    });
    applySettings(settings);
    footerStatusEl.textContent = successMessage;
  } catch {
    footerStatusEl.textContent = "更新设置失败";
  } finally {
    setSettingsControlsDisabled(false);
  }
}

async function resetSettings() {
  setSettingsControlsDisabled(true);

  try {
    applySettings(await invoke<AppSettings>("reset_settings"));
    footerStatusEl.textContent = "已重置设置";
  } catch {
    footerStatusEl.textContent = "重置设置失败";
  } finally {
    setSettingsControlsDisabled(false);
  }
}

function applySettings(settings: AppSettings) {
  appSettings = settings;
  updateSettingsMenuState();
  scheduleAutoRefresh();

  if (lastResult) {
    render(lastResult);
  }
}

function scheduleAutoRefresh() {
  if (autoRefreshTimer !== undefined) {
    window.clearInterval(autoRefreshTimer);
  }

  autoRefreshTimer = window.setInterval(() => {
    void refreshDevices();
  }, appSettings.refreshIntervalSeconds * 1000);
}

function updateSettingsMenuState() {
  refreshIntervalMenuItem.querySelector("span:last-child")!.textContent =
    `刷新间隔：${appSettings.refreshIntervalSeconds}秒`;
  lowBatteryStatusMenuItem.setAttribute(
    "aria-checked",
    String(appSettings.lowBatteryStatusEnabled),
  );
  lowBatteryThresholdMenuItem.querySelector("span:last-child")!.textContent =
    `低电量阈值：${appSettings.lowBatteryThreshold}%`;
}

function setSettingsControlsDisabled(disabled: boolean) {
  refreshIntervalMenuItem.disabled = disabled;
  lowBatteryStatusMenuItem.disabled = disabled;
  lowBatteryThresholdMenuItem.disabled = disabled;
  resetSettingsMenuItem.disabled = disabled;
}

function render(result: RefreshResult) {
  applyPanelView(buildPanelView(result, appSettings));
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
  clearStartupMenuItem.disabled = true;

  try {
    setStartupState(await invoke<boolean>("set_startup_enabled", { enabled }));
    footerStatusEl.textContent = startupEnabled ? "已启用开机自启动" : "已关闭开机自启动";
  } catch {
    footerStatusEl.textContent = "更新开机自启动失败";
  } finally {
    startupMenuItem.disabled = false;
    clearStartupMenuItem.disabled = false;
  }
}

async function clearStartupEntry() {
  const confirmed = window.confirm(
    "删除 Blue Battery 写入的开机自启动注册表项？这不会删除应用文件，也不会影响当前运行。",
  );

  if (!confirmed) {
    return;
  }

  startupMenuItem.disabled = true;
  clearStartupMenuItem.disabled = true;

  try {
    const removed = await invoke<boolean>("clear_startup_entry");
    setStartupState(false);
    footerStatusEl.textContent = removed ? "已清理开机自启动项" : "未发现开机自启动项";
    setSettingsMenuOpen(false);
  } catch {
    footerStatusEl.textContent = "清理开机自启动项失败";
  } finally {
    startupMenuItem.disabled = false;
    clearStartupMenuItem.disabled = false;
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
