import {
  describePanelState,
  type DeviceBatteryInfo,
  type PanelState,
  type RefreshResult,
} from "./panel_state.ts";

export type PanelView = {
  summary: string;
  contentHtml: string;
  connectedBadge: string;
  footerStatus: string;
};

export type PanelRenderSettings = {
  lowBatteryStatusEnabled?: boolean;
  lowBatteryThreshold?: number;
};

const DEFAULT_LOW_BATTERY_THRESHOLD = 20;

export function buildPanelView(
  result: RefreshResult,
  settings: PanelRenderSettings = {},
): PanelView {
  const state = describePanelState(result, false);

  return {
    summary: `上次更新 ${formatTime(result.refreshed_at_ms)}`,
    contentHtml:
      state.kind === "devices"
        ? renderDeviceList(result.devices, result.errors, settings)
        : `${renderMessageStateHtml(state)}${renderErrors(result.errors)}`,
    connectedBadge: formatConnectedBadge(result.connected_le_device_count),
    footerStatus: formatFooterStatus(result.devices, settings),
  };
}

export function buildTransientPanelView(state: PanelState): PanelView {
  return {
    summary: state.summary,
    contentHtml: renderMessageStateHtml(state),
    connectedBadge: "0 BLE",
    footerStatus: "就绪",
  };
}

export function buildCommandErrorView(
  message: string,
  fallback: RefreshResult | null,
): PanelView {
  return {
    summary: fallback ? `上次更新 ${formatTime(fallback.refreshed_at_ms)}` : "刷新失败",
    contentHtml: `
    <div class="empty" data-state="error">
      <div class="empty-title">读取失败</div>
      <div class="empty-detail">${escapeHtml(message)}</div>
    </div>
  `,
    connectedBadge: fallback
      ? formatConnectedBadge(fallback.connected_le_device_count)
      : "0 BLE",
    footerStatus: "就绪",
  };
}

function renderDeviceList(
  devices: DeviceBatteryInfo[],
  errors: string[],
  settings: PanelRenderSettings,
) {
  return `
      <div class="device-list">
        ${devices.map((device) => renderDevice(device, settings)).join("")}
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

function renderDevice(device: DeviceBatteryInfo, settings: PanelRenderSettings) {
  const percent = Math.max(0, Math.min(100, device.battery_percent));
  const lowThreshold = settings.lowBatteryThreshold ?? DEFAULT_LOW_BATTERY_THRESHOLD;
  const level = percent <= lowThreshold ? "low" : percent <= 35 ? "mid" : "high";

  return `
    <article class="device-row">
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

function formatConnectedBadge(count: number) {
  return `${Math.max(0, count)} BLE`;
}

function formatFooterStatus(devices: DeviceBatteryInfo[], settings: PanelRenderSettings) {
  if (settings.lowBatteryStatusEnabled === false) {
    return "就绪";
  }

  const lowThreshold = settings.lowBatteryThreshold ?? DEFAULT_LOW_BATTERY_THRESHOLD;
  const lowDevice = devices
    .filter((device) => device.battery_percent <= lowThreshold)
    .sort((left, right) => left.battery_percent - right.battery_percent)[0];

  return lowDevice
    ? `低电量：${lowDevice.display_name} ${lowDevice.battery_percent}%`
    : "就绪";
}

function escapeHtml(value: string) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
