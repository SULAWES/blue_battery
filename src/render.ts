import {
  describePanelState,
  type DeviceBatteryInfo,
  type PanelState,
  type RefreshResult,
} from "./panel_state.ts";

export type PanelView = {
  summary: string;
  contentHtml: string;
  timestamp: string;
  connectedCount: string;
};

export function buildPanelView(result: RefreshResult): PanelView {
  const state = describePanelState(result, false);

  return {
    summary: state.summary,
    contentHtml:
      state.kind === "devices"
        ? renderDeviceList(result.devices, result.errors)
        : `${renderMessageStateHtml(state)}${renderErrors(result.errors)}`,
    timestamp: formatTime(result.refreshed_at_ms),
    connectedCount: `${result.connected_le_device_count} 个已连接 BLE 设备`,
  };
}

export function buildTransientPanelView(state: PanelState): PanelView {
  return {
    summary: state.summary,
    contentHtml: renderMessageStateHtml(state),
    timestamp: "--",
    connectedCount: "0 个已连接 BLE 设备",
  };
}

export function buildCommandErrorView(
  message: string,
  fallback: RefreshResult | null,
): PanelView {
  return {
    summary: "刷新失败",
    contentHtml: `
    <div class="empty" data-state="error">
      <div class="empty-title">读取失败</div>
      <div class="empty-detail">${escapeHtml(message)}</div>
    </div>
  `,
    timestamp: fallback ? formatTime(fallback.refreshed_at_ms) : "--",
    connectedCount: fallback
      ? `${fallback.connected_le_device_count} 个已连接 BLE 设备`
      : "0 个已连接 BLE 设备",
  };
}

function renderDeviceList(devices: DeviceBatteryInfo[], errors: string[]) {
  return `
      <div class="device-list">
        ${devices.map(renderDevice).join("")}
      </div>
      ${renderErrors(errors)}
    `;
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
