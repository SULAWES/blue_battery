export type DeviceBatteryInfo = {
  device_id: string;
  display_name: string;
  battery_percent: number;
  connection_state: string;
  source_kind: string;
  updated_at_ms: number;
};

export type DeviceReadIssue = {
  device_id: string;
  display_name: string;
  status:
    | "not_connected"
    | "no_standard_battery_service"
    | "unreadable"
    | "read_failed";
  message: string;
};

export type RefreshResult = {
  devices: DeviceBatteryInfo[];
  connected_le_device_count: number;
  refreshed_at_ms: number;
  errors: string[];
  issues: DeviceReadIssue[];
};

type MessagePanelState = {
  kind: "loading" | "empty" | "error";
  summary: string;
  title: string;
  detail: string;
};

type DevicesPanelState = {
  kind: "devices";
  summary: string;
};

export type PanelState = MessagePanelState | DevicesPanelState;

export function describePanelState(
  result: RefreshResult | null,
  refreshing: boolean,
): PanelState {
  if (!result) {
    return {
      kind: "loading",
      summary: refreshing ? "正在读取" : "等待刷新",
      title: "正在读取电量",
      detail: "正在从 Windows 蓝牙接口读取标准电量。",
    };
  }

  if (result.devices.length > 0) {
    const lowest = Math.min(...result.devices.map((device) => device.battery_percent));
    const lowestDevice = result.devices.find((device) => device.battery_percent === lowest);

    return {
      kind: "devices",
      summary: lowestDevice ? `${lowestDevice.display_name} ${lowest}%` : `${lowest}%`,
    };
  }

  if (result.errors.length > 0) {
    return {
      kind: "error",
      summary: "读取失败，稍后重试",
      title: "读取失败",
      detail: "Windows 蓝牙接口暂时没有返回可用电量，稍后会继续刷新。",
    };
  }

  const readIssue = result.issues.find(
    (issue) => issue.status === "unreadable" || issue.status === "read_failed",
  );
  if (readIssue) {
    return {
      kind: "error",
      summary: readIssue.status === "read_failed" ? "读取失败，稍后重试" : "设备电量不可读",
      title: readIssue.status === "read_failed" ? "读取失败" : "设备电量不可读",
      detail:
        readIssue.status === "read_failed"
          ? `${readIssue.display_name} 已连接，但读取标准电量失败。`
          : `${readIssue.display_name} 已连接，但 Windows 暂时无法读取标准电量。`,
    };
  }

  if (result.connected_le_device_count === 0) {
    return {
      kind: "empty",
      summary: "没有已连接 BLE 设备",
      title: "暂无可显示电量",
      detail: "连接支持标准 GATT Battery Service 的 BLE 设备后会自动刷新。",
    };
  }

  const noBatteryIssue = result.issues.find(
    (issue) => issue.status === "no_standard_battery_service",
  );
  if (noBatteryIssue) {
    return {
      kind: "empty",
      summary: "已连接设备未暴露标准电量",
      title: "暂无可显示电量",
      detail: `${noBatteryIssue.display_name} 没有暴露标准 Battery Service。`,
    };
  }

  return {
    kind: "empty",
    summary: "已连接设备未暴露标准电量",
    title: "暂无可显示电量",
    detail: "当前连接设备没有返回标准 Battery Level characteristic。",
  };
}
