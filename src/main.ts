import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import {
  describePanelState,
  type DeviceBatteryInfo,
  type PanelState,
  type RefreshResult,
} from "./panel_state.ts";
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
    renderMessageState(describePanelState(null, true));
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
  const devices = result.devices;
  const state = describePanelState(result, false);
  summaryEl.textContent = state.summary;

  if (state.kind === "devices") {
    contentEl.innerHTML = `
      <div class="device-list">
        ${devices.map(renderDevice).join("")}
      </div>
      ${renderErrors(result.errors)}
    `;
  } else {
    contentEl.innerHTML = `
      ${renderMessageStateHtml(state)}
      ${renderErrors(result.errors)}
    `;
  }

  timestampEl.textContent = formatTime(result.refreshed_at_ms);
  connectedCountEl.textContent = `${result.connected_le_device_count} 个已连接 BLE 设备`;
}

function renderMessageState(state: PanelState) {
  summaryEl.textContent = state.summary;
  contentEl.innerHTML = renderMessageStateHtml(state);
  timestampEl.textContent = "--";
  connectedCountEl.textContent = "0 个已连接 BLE 设备";
}

function renderMessageStateHtml(state: PanelState) {
  if (state.kind === "devices") {
    return "";
  }

  return `
    <div class="empty" data-state="${state.kind}">
      <div class="empty-title">${escapeHtml(state.title)}</div>
      <div class="empty-detail">${escapeHtml(state.detail)}</div>
    </div>
  `;
}

function renderDevice(device: DeviceBatteryInfo) {
  const percent = Math.max(0, Math.min(100, device.battery_percent));
  const level = percent <= 20 ? "low" : percent <= 50 ? "mid" : "high";

  return `
    <article class="device-row">
      <div class="device-icon" data-level="${level}" aria-hidden="true">
        <div class="device-icon-fill" style="height: ${percent}%"></div>
      </div>
      <div class="device-main">
        <div class="device-name">${escapeHtml(device.display_name)}</div>
        <div class="device-meta">${escapeHtml(device.connection_state)} · ${escapeHtml(device.source_kind)}</div>
      </div>
      <div class="battery-stack">
        <div class="battery-number">${percent}%</div>
        <div class="battery-bar" aria-hidden="true">
          <div class="battery-bar-fill" data-level="${level}" style="width: ${percent}%"></div>
        </div>
      </div>
    </article>
  `;
}

function renderError(message: string) {
  const fallback = lastResult;
  summaryEl.textContent = "刷新失败";
  contentEl.innerHTML = `
    <div class="empty">
      <div class="empty-title">读取失败</div>
      <div class="empty-detail">${escapeHtml(message)}</div>
    </div>
  `;

  timestampEl.textContent = fallback ? formatTime(fallback.refreshed_at_ms) : "--";
  connectedCountEl.textContent = fallback
    ? `${fallback.connected_le_device_count} 个已连接 BLE 设备`
    : "0 个已连接 BLE 设备";
}

function renderErrors(errors: string[]) {
  if (errors.length === 0) {
    return "";
  }

  return `
    <div class="warning-list">
      ${errors.map((error) => `<div>${escapeHtml(error)}</div>`).join("")}
    </div>
  `;
}

function formatTime(ms: number) {
  return new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(new Date(ms));
}

function escapeHtml(value: string) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
