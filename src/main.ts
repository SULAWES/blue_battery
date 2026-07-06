import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import {
  describePanelState,
  type DeviceReadIssue,
  type RefreshResult,
} from "./panel_state.ts";
import {
  buildCommandErrorView,
  buildPanelView,
  buildTransientPanelView,
  type PanelView,
} from "./render.ts";
import {
  DEFAULT_SETTINGS,
  type AppSettings,
} from "./settings.ts";
import "./styles.css";

type SettingsMenuView =
  | "main"
  | "refresh"
  | "lowBattery"
  | "threshold"
  | "startup"
  | "diagnostics"
  | "about";

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("Missing app root.");
}

let lastResult: RefreshResult | null = null;
let refreshing = false;
let startupEnabled = false;
let appSettings: AppSettings = DEFAULT_SETTINGS;
let appVersion = "0.1.0";
let settingsMenuViewStack: SettingsMenuView[] = ["main"];

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
          <div id="settings-menu-main" class="settings-menu-view" data-menu-view="main" role="menu">
            <button id="menu-refresh" class="menu-item" type="button" role="menuitem">
              <span class="fluent-icon menu-refresh-glyph" aria-hidden="true">&#xE72C;</span>
              <span class="menu-text">刷新</span>
            </button>
            <button id="menu-open-refresh-interval" class="menu-item" type="button" role="menuitem" data-open-menu-view="refresh">
              <span class="fluent-icon menu-interval-glyph" aria-hidden="true">&#xE916;</span>
              <span class="menu-text">刷新频率</span>
              <span id="menu-refresh-interval-summary" class="menu-value">60 秒</span>
              <span class="fluent-icon menu-chevron" aria-hidden="true">&#xE974;</span>
            </button>
            <button id="menu-open-low-battery" class="menu-item" type="button" role="menuitem" data-open-menu-view="lowBattery">
              <span class="fluent-icon menu-low-battery-glyph" aria-hidden="true">&#xE7BA;</span>
              <span class="menu-text">低电量提醒</span>
              <span id="menu-low-battery-summary" class="menu-value">20%</span>
              <span class="fluent-icon menu-chevron" aria-hidden="true">&#xE974;</span>
            </button>
            <button id="menu-open-startup" class="menu-item" type="button" role="menuitem" data-open-menu-view="startup">
              <span class="fluent-icon menu-startup-glyph" aria-hidden="true">&#xE7E8;</span>
              <span class="menu-text">启动设置</span>
              <span id="menu-startup-summary" class="menu-value">关闭</span>
              <span class="fluent-icon menu-chevron" aria-hidden="true">&#xE974;</span>
            </button>
            <button id="menu-open-diagnostics" class="menu-item" type="button" role="menuitem" data-open-menu-view="diagnostics">
              <span class="fluent-icon menu-diagnostics-glyph" aria-hidden="true">&#xE946;</span>
              <span class="menu-text">诊断信息</span>
              <span class="fluent-icon menu-chevron" aria-hidden="true">&#xE974;</span>
            </button>
            <button id="menu-open-about" class="menu-item" type="button" role="menuitem" data-open-menu-view="about">
              <span class="fluent-icon menu-about-glyph" aria-hidden="true">&#xE946;</span>
              <span class="menu-text">关于 Blue Battery</span>
              <span class="fluent-icon menu-chevron" aria-hidden="true">&#xE974;</span>
            </button>
            <button id="menu-reset-settings" class="menu-item" type="button" role="menuitem">
              <span class="fluent-icon menu-reset-glyph" aria-hidden="true">&#xE777;</span>
              <span class="menu-text">重置设置</span>
            </button>
            <button id="menu-exit" class="menu-item" type="button" role="menuitem">
              <span class="fluent-icon menu-exit-glyph" aria-hidden="true">&#xE8BB;</span>
              <span class="menu-text">退出</span>
            </button>
          </div>

          <div id="settings-menu-refresh" class="settings-menu-view" data-menu-view="refresh" role="menu" hidden>
            <button class="menu-back settings-menu-header" type="button" role="menuitem" data-menu-back>
              <span class="fluent-icon" aria-hidden="true">&#xE72B;</span>
              <span>刷新频率</span>
            </button>
            <button id="menu-refresh-interval-120" class="menu-item" type="button" role="menuitemradio" aria-checked="false" data-refresh-interval="120">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">省电：120 秒</span>
            </button>
            <button id="menu-refresh-interval-60" class="menu-item" type="button" role="menuitemradio" aria-checked="true" data-refresh-interval="60">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">标准：60 秒</span>
            </button>
            <button id="menu-refresh-interval-30" class="menu-item" type="button" role="menuitemradio" aria-checked="false" data-refresh-interval="30">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">快速：30 秒</span>
            </button>
          </div>

          <div id="settings-menu-low-battery" class="settings-menu-view" data-menu-view="lowBattery" role="menu" hidden>
            <button class="menu-back settings-menu-header" type="button" role="menuitem" data-menu-back>
              <span class="fluent-icon" aria-hidden="true">&#xE72B;</span>
              <span>低电量提醒</span>
            </button>
            <button id="menu-low-battery-status" class="menu-item" type="button" role="menuitemcheckbox" aria-checked="true">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">显示低电量状态</span>
            </button>
            <button id="menu-low-battery-system-notification" class="menu-item" type="button" role="menuitemcheckbox" aria-checked="false">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">低电量通知</span>
            </button>
            <button id="menu-open-low-battery-threshold" class="menu-item" type="button" role="menuitem" data-open-menu-view="threshold">
              <span class="fluent-icon menu-threshold-glyph" aria-hidden="true">&#xE9D9;</span>
              <span class="menu-text">阈值</span>
              <span id="menu-low-battery-threshold-summary" class="menu-value">20%</span>
              <span class="fluent-icon menu-chevron" aria-hidden="true">&#xE974;</span>
            </button>
          </div>

          <div id="settings-menu-threshold" class="settings-menu-view" data-menu-view="threshold" role="menu" hidden>
            <button class="menu-back settings-menu-header" type="button" role="menuitem" data-menu-back>
              <span class="fluent-icon" aria-hidden="true">&#xE72B;</span>
              <span>低电量阈值</span>
            </button>
            <button id="menu-low-battery-threshold-10" class="menu-item" type="button" role="menuitemradio" aria-checked="false" data-low-battery-threshold="10">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">10%</span>
            </button>
            <button id="menu-low-battery-threshold-15" class="menu-item" type="button" role="menuitemradio" aria-checked="false" data-low-battery-threshold="15">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">15%</span>
            </button>
            <button id="menu-low-battery-threshold-20" class="menu-item" type="button" role="menuitemradio" aria-checked="true" data-low-battery-threshold="20">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">20%</span>
            </button>
            <button id="menu-low-battery-threshold-25" class="menu-item" type="button" role="menuitemradio" aria-checked="false" data-low-battery-threshold="25">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">25%</span>
            </button>
          </div>

          <div id="settings-menu-startup" class="settings-menu-view" data-menu-view="startup" role="menu" hidden>
            <button class="menu-back settings-menu-header" type="button" role="menuitem" data-menu-back>
              <span class="fluent-icon" aria-hidden="true">&#xE72B;</span>
              <span>启动设置</span>
            </button>
            <button id="menu-startup" class="menu-item" type="button" role="menuitemcheckbox" aria-checked="false">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">开机自启动</span>
            </button>
            <button id="menu-show-panel-on-startup" class="menu-item" type="button" role="menuitemcheckbox" aria-checked="false">
              <span class="fluent-icon menu-check" aria-hidden="true">&#xE73E;</span>
              <span class="menu-text">启动时显示面板</span>
            </button>
            <button id="menu-clear-startup" class="menu-item" type="button" role="menuitem">
              <span class="fluent-icon menu-cleanup-glyph" aria-hidden="true">&#xE74D;</span>
              <span class="menu-text">清理开机自启动项</span>
            </button>
          </div>

          <div id="settings-menu-diagnostics" class="settings-menu-view" data-menu-view="diagnostics" role="menu" hidden>
            <button class="menu-back settings-menu-header" type="button" role="menuitem" data-menu-back>
              <span class="fluent-icon" aria-hidden="true">&#xE72B;</span>
              <span>诊断信息</span>
            </button>
            <button id="menu-diagnostics" class="menu-item" type="button" role="menuitem">
              <span class="fluent-icon menu-view-glyph" aria-hidden="true">&#xE890;</span>
              <span class="menu-text">查看诊断信息</span>
            </button>
            <button id="menu-copy-diagnostics" class="menu-item" type="button" role="menuitem">
              <span class="fluent-icon menu-copy-glyph" aria-hidden="true">&#xE8C8;</span>
              <span class="menu-text">复制诊断信息</span>
            </button>
            <button id="menu-copy-device-summary" class="menu-item" type="button" role="menuitem">
              <span class="fluent-icon menu-copy-glyph" aria-hidden="true">&#xE8C8;</span>
              <span class="menu-text">复制设备摘要</span>
            </button>
          </div>

          <div id="settings-menu-about" class="settings-menu-view" data-menu-view="about" role="menu" hidden>
            <button class="menu-back settings-menu-header" type="button" role="menuitem" data-menu-back>
              <span class="fluent-icon" aria-hidden="true">&#xE72B;</span>
              <span>关于 Blue Battery</span>
            </button>
            <div class="menu-info-item">
              <span id="menu-about-version">版本 0.1.0</span>
            </div>
            <div class="menu-info-item">
              <span>只显示 Windows 能读取的标准 BLE Battery Service 电量</span>
            </div>
            <div class="menu-info-item">
              <span>不支持厂商私有协议、配对或连接管理</span>
            </div>
          </div>
        </div>
      </div>
    </footer>
  </main>
`;

const summaryEl = document.querySelector<HTMLDivElement>("#summary")!;
const shellEl = document.querySelector<HTMLElement>(".shell")!;
const contentEl = document.querySelector<HTMLElement>("#content")!;
const connectedBadgeEl = document.querySelector<HTMLDivElement>("#connected-badge")!;
const footerStatusEl = document.querySelector<HTMLSpanElement>("#footer-status")!;
const settingsButton = document.querySelector<HTMLButtonElement>("#settings")!;
const settingsMenu = document.querySelector<HTMLDivElement>("#settings-menu")!;
const settingsMenuViews = Array.from(
  document.querySelectorAll<HTMLElement>("[data-menu-view]"),
);
const refreshMenuItem = document.querySelector<HTMLButtonElement>("#menu-refresh")!;
const refreshIntervalSummaryEl = document.querySelector<HTMLSpanElement>(
  "#menu-refresh-interval-summary",
)!;
const lowBatterySummaryEl = document.querySelector<HTMLSpanElement>(
  "#menu-low-battery-summary",
)!;
const lowBatteryThresholdSummaryEl = document.querySelector<HTMLSpanElement>(
  "#menu-low-battery-threshold-summary",
)!;
const startupSummaryEl = document.querySelector<HTMLSpanElement>("#menu-startup-summary")!;
const lowBatteryStatusMenuItem = document.querySelector<HTMLButtonElement>(
  "#menu-low-battery-status",
)!;
const lowBatterySystemNotificationMenuItem = document.querySelector<HTMLButtonElement>(
  "#menu-low-battery-system-notification",
)!;
const startupMenuItem = document.querySelector<HTMLButtonElement>("#menu-startup")!;
const showPanelOnStartupMenuItem = document.querySelector<HTMLButtonElement>(
  "#menu-show-panel-on-startup",
)!;
const clearStartupMenuItem = document.querySelector<HTMLButtonElement>("#menu-clear-startup")!;
const resetSettingsMenuItem = document.querySelector<HTMLButtonElement>("#menu-reset-settings")!;
const exitMenuItem = document.querySelector<HTMLButtonElement>("#menu-exit")!;
const diagnosticsMenuItem = document.querySelector<HTMLButtonElement>("#menu-diagnostics")!;
const copyDiagnosticsMenuItem = document.querySelector<HTMLButtonElement>(
  "#menu-copy-diagnostics",
)!;
const copyDeviceSummaryMenuItem = document.querySelector<HTMLButtonElement>(
  "#menu-copy-device-summary",
)!;
const aboutVersionEl = document.querySelector<HTMLSpanElement>("#menu-about-version")!;
const diagnosticsPanel = document.querySelector<HTMLElement>("#diagnostics-panel")!;
const diagnosticsReportEl = document.querySelector<HTMLPreElement>("#diagnostics-report")!;
const diagnosticsCloseButton = document.querySelector<HTMLButtonElement>("#diagnostics-close")!;
const refreshIntervalMenuItems = Array.from(
  document.querySelectorAll<HTMLButtonElement>("[data-refresh-interval]"),
);
const lowBatteryThresholdMenuItems = Array.from(
  document.querySelectorAll<HTMLButtonElement>("[data-low-battery-threshold]"),
);
const openMenuViewItems = Array.from(
  document.querySelectorAll<HTMLButtonElement>("[data-open-menu-view]"),
);
const backMenuItems = Array.from(
  document.querySelectorAll<HTMLButtonElement>("[data-menu-back]"),
);

settingsButton.addEventListener("click", (event) => {
  event.stopPropagation();
  const willOpen = settingsMenu.hidden === true;
  setSettingsMenuOpen(willOpen);

  if (willOpen) {
    void loadSettings();
    void refreshStartupState();
    void loadAboutInfo();
  }
});

settingsMenu.addEventListener("click", (event) => {
  event.stopPropagation();
});

openMenuViewItems.forEach((item) => {
  item.addEventListener("click", () => {
    openSettingsMenuView(item.dataset.openMenuView as SettingsMenuView);
  });
});

backMenuItems.forEach((item) => {
  item.addEventListener("click", () => {
    backSettingsMenuView();
  });
});

refreshMenuItem.addEventListener("click", () => {
  setSettingsMenuOpen(false);
  void refreshDevices();
});

refreshIntervalMenuItems.forEach((item) => {
  item.addEventListener("click", () => {
    void updateSettings(
      { refreshIntervalSeconds: Number(item.dataset.refreshInterval) },
      "已更新刷新频率",
    );
  });
});

lowBatteryStatusMenuItem.addEventListener("click", () => {
  void updateSettings(
    { lowBatteryStatusEnabled: !appSettings.lowBatteryStatusEnabled },
    "已更新低电量状态",
  );
});

lowBatterySystemNotificationMenuItem.addEventListener("click", () => {
  void updateSettings(
    {
      lowBatterySystemNotificationEnabled:
        !appSettings.lowBatterySystemNotificationEnabled,
    },
    "已更新低电量通知",
  );
});

lowBatteryThresholdMenuItems.forEach((item) => {
  item.addEventListener("click", () => {
    void updateSettings(
      { lowBatteryThreshold: Number(item.dataset.lowBatteryThreshold) },
      "已更新低电量阈值",
    );
  });
});

startupMenuItem.addEventListener("click", () => {
  void setStartupEnabled(!startupEnabled);
});

showPanelOnStartupMenuItem.addEventListener("click", () => {
  void updateSettings(
    { showPanelOnStartup: !appSettings.showPanelOnStartup },
    "已更新启动时显示面板",
  );
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

copyDiagnosticsMenuItem.addEventListener("click", () => {
  void copyDiagnostics();
});

copyDeviceSummaryMenuItem.addEventListener("click", () => {
  void copyDeviceSummary();
});

exitMenuItem.addEventListener("click", () => {
  setSettingsMenuOpen(false);
  void invoke<void>("exit_app");
});

diagnosticsCloseButton.addEventListener("click", () => {
  setDiagnosticsOpen(false);
});

document.addEventListener("click", () => {
  setSettingsMenuOpen(false);
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    if (diagnosticsPanel.hidden === false) {
      setDiagnosticsOpen(false);
      return;
    }

    if (settingsMenu.hidden === false) {
      if (currentSettingsMenuView() !== "main") {
        backSettingsMenuView();
        return;
      }

      setSettingsMenuOpen(false);
      return;
    }

    void hidePanel();
  }
});

window.addEventListener("focus", () => {
  playPanelEntryAnimation();
});

void listen<RefreshResult>("devices-refreshed", (event) => {
  lastResult = event.payload;
  render(event.payload);
});

void listen<{
  display_name: string;
  battery_percent: number;
}>("low-battery-alert", (event) => {
  footerStatusEl.textContent = `${event.payload.display_name} 电量较低：${event.payload.battery_percent}%`;
});

void initialize();

async function initialize() {
  await loadSettings();
  await loadAboutInfo();
  await refreshStartupState();
  await refreshDevices();
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

async function loadAboutInfo() {
  try {
    appVersion = await invoke<string>("get_app_version");
    aboutVersionEl.textContent = `版本 ${appVersion}`;
  } catch {
    aboutVersionEl.textContent = `版本 ${appVersion}`;
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

  if (lastResult) {
    render(lastResult);
  }
}

function updateSettingsMenuState() {
  refreshIntervalSummaryEl.textContent = `${appSettings.refreshIntervalSeconds} 秒`;
  lowBatterySummaryEl.textContent = appSettings.lowBatteryStatusEnabled
    ? `${appSettings.lowBatteryThreshold}%`
    : "关闭";
  lowBatteryThresholdSummaryEl.textContent = `${appSettings.lowBatteryThreshold}%`;

  lowBatteryStatusMenuItem.setAttribute(
    "aria-checked",
    String(appSettings.lowBatteryStatusEnabled),
  );
  lowBatterySystemNotificationMenuItem.setAttribute(
    "aria-checked",
    String(appSettings.lowBatterySystemNotificationEnabled),
  );
  showPanelOnStartupMenuItem.setAttribute(
    "aria-checked",
    String(appSettings.showPanelOnStartup),
  );

  for (const item of refreshIntervalMenuItems) {
    item.setAttribute(
      "aria-checked",
      String(Number(item.dataset.refreshInterval) === appSettings.refreshIntervalSeconds),
    );
  }

  for (const item of lowBatteryThresholdMenuItems) {
    item.setAttribute(
      "aria-checked",
      String(Number(item.dataset.lowBatteryThreshold) === appSettings.lowBatteryThreshold),
    );
  }

  updateStartupSummary();
}

function setSettingsControlsDisabled(disabled: boolean) {
  for (const item of refreshIntervalMenuItems) {
    item.disabled = disabled;
  }
  for (const item of lowBatteryThresholdMenuItems) {
    item.disabled = disabled;
  }
  lowBatteryStatusMenuItem.disabled = disabled;
  lowBatterySystemNotificationMenuItem.disabled = disabled;
  showPanelOnStartupMenuItem.disabled = disabled;
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

  if (open) {
    resetSettingsMenuView();
  }
}

function openSettingsMenuView(view: SettingsMenuView) {
  settingsMenuViewStack.push(view);
  renderSettingsMenuView(view);
}

function backSettingsMenuView() {
  if (settingsMenuViewStack.length > 1) {
    settingsMenuViewStack.pop();
  }

  renderSettingsMenuView(currentSettingsMenuView());
}

function resetSettingsMenuView() {
  settingsMenuViewStack = ["main"];
  renderSettingsMenuView("main");
}

function currentSettingsMenuView() {
  return settingsMenuViewStack[settingsMenuViewStack.length - 1] ?? "main";
}

function renderSettingsMenuView(view: SettingsMenuView) {
  for (const item of settingsMenuViews) {
    item.hidden = item.dataset.menuView !== view;
  }
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
  updateStartupSummary();
}

function updateStartupSummary() {
  const states = [];
  if (startupEnabled) {
    states.push("自启");
  }
  if (appSettings.showPanelOnStartup) {
    states.push("显示面板");
  }
  startupSummaryEl.textContent = states.length > 0 ? states.join("、") : "关闭";
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

async function copyDiagnostics() {
  try {
    const report = await invoke<string>("get_diagnostics_report");
    await copyTextToClipboard(report);
    footerStatusEl.textContent = "已复制诊断信息";
  } catch {
    footerStatusEl.textContent = "复制诊断信息失败";
  }
}

async function copyDeviceSummary() {
  try {
    await copyTextToClipboard(buildDeviceSummary(lastResult));
    footerStatusEl.textContent = "已复制设备摘要";
  } catch {
    footerStatusEl.textContent = "复制设备摘要失败";
  }
}

async function copyTextToClipboard(text: string) {
  if (!navigator.clipboard?.writeText) {
    throw new Error("Clipboard API is unavailable.");
  }

  await navigator.clipboard.writeText(text);
}

function buildDeviceSummary(result: RefreshResult | null) {
  const lines = [
    `Blue Battery ${appVersion}`,
    `Last updated: ${formatSummaryTime(result?.refreshed_at_ms)}`,
    `Connected BLE: ${result?.connected_le_device_count ?? 0}`,
    `Low battery threshold: ${appSettings.lowBatteryThreshold}%`,
    "Displayable devices:",
  ];

  if (!result || result.devices.length === 0) {
    lines.push("- none");
  } else {
    for (const device of result.devices) {
      lines.push(`- ${device.display_name}: ${device.battery_percent}% (${device.source_kind})`);
    }
  }

  lines.push("Device issues:");
  if (!result || result.issues.length === 0) {
    lines.push("- none");
  } else {
    for (const issue of result.issues) {
      lines.push(`- ${formatDeviceIssue(issue)}`);
    }
  }

  lines.push(`Errors: ${result && result.errors.length > 0 ? result.errors.join(" | ") : "none"}`);
  return lines.join("\n");
}

function formatDeviceIssue(issue: DeviceReadIssue) {
  const labels: Record<DeviceReadIssue["status"], string> = {
    not_connected: "未连接",
    no_standard_battery_service: "无标准 Battery Service",
    unreadable: "不可读",
    read_failed: "读取失败",
  };

  return `${issue.display_name}: ${labels[issue.status]} (${issue.message})`;
}

function formatSummaryTime(timestampMs: number | undefined) {
  if (timestampMs === undefined) {
    return "never";
  }

  return new Date(timestampMs).toLocaleTimeString([], { hour12: false });
}

function setDiagnosticsOpen(open: boolean) {
  diagnosticsPanel.hidden = !open;
}

function playPanelEntryAnimation() {
  shellEl.removeAttribute("data-entering");
  void shellEl.offsetWidth;
  shellEl.dataset.entering = "true";

  window.setTimeout(() => {
    shellEl.removeAttribute("data-entering");
  }, 180);
}

async function hidePanel() {
  try {
    await invoke<void>("hide_panel");
  } catch {
    footerStatusEl.textContent = "关闭面板失败";
  }
}
