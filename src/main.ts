import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import "./styles.css";

type DeviceBatteryInfo = {
  device_id: string;
  display_name: string;
  battery_percent: number;
  connection_state: string;
  source_kind: string;
  updated_at_ms: number;
};

type RefreshResult = {
  devices: DeviceBatteryInfo[];
  connected_le_device_count: number;
  refreshed_at_ms: number;
  errors: string[];
};

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

  if (devices.length === 0) {
    summaryEl.textContent = result.connected_le_device_count > 0
      ? "已连接设备未暴露标准电量"
      : "没有已连接 BLE 设备";

    contentEl.innerHTML = `
      <div class="empty">
        <div class="empty-title">暂无可显示电量</div>
        <div class="empty-detail">${escapeHtml(buildEmptyDetail(result))}</div>
      </div>
      ${renderErrors(result.errors)}
    `;
  } else {
    const lowest = Math.min(...devices.map((device) => device.battery_percent));
    const lowestDevice = devices.find((device) => device.battery_percent === lowest);
    summaryEl.textContent = lowestDevice
      ? `${lowestDevice.display_name} ${lowest}%`
      : `${lowest}%`;

    contentEl.innerHTML = `
      <div class="device-list">
        ${devices.map(renderDevice).join("")}
      </div>
      ${renderErrors(result.errors)}
    `;
  }

  timestampEl.textContent = formatTime(result.refreshed_at_ms);
  connectedCountEl.textContent = `${result.connected_le_device_count} 个已连接 BLE 设备`;
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

function buildEmptyDetail(result: RefreshResult) {
  if (result.connected_le_device_count === 0) {
    return "连接支持标准 GATT Battery Service 的 BLE 设备后会自动刷新。";
  }

  return "当前连接设备没有返回标准 Battery Level characteristic。";
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
