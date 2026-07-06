import assert from "node:assert/strict";
import test from "node:test";

import {
  describePanelState,
  type RefreshResult,
} from "../src/panel_state.ts";

function result(partial: Partial<RefreshResult>): RefreshResult {
  return {
    devices: [],
    connected_le_device_count: 0,
    refreshed_at_ms: 123,
    errors: [],
    issues: [],
    ...partial,
  };
}

test("reports loading state before the first refresh completes", () => {
  assert.deepEqual(describePanelState(null, true), {
    kind: "loading",
    summary: "正在读取",
    title: "正在读取电量",
    detail: "正在从 Windows 蓝牙接口读取标准电量。",
  });
});

test("reports device state with lowest battery in the summary", () => {
  const state = describePanelState(
    result({
      devices: [
        {
          device_id: "keyboard",
          display_name: "Keychron Z6 Ultra 8K",
          battery_percent: 48,
          connection_state: "已连接",
          source_kind: "GATT BAS",
          updated_at_ms: 123,
        },
      ],
      connected_le_device_count: 1,
    }),
    false,
  );

  assert.equal(state.kind, "devices");
  assert.equal(state.summary, "Keychron Z6 Ultra 8K 48%");
});

test("reports empty state when connected devices expose no standard battery", () => {
  assert.deepEqual(
    describePanelState(
      result({
        connected_le_device_count: 2,
        issues: [
          {
            device_id: "earbuds",
            display_name: "OPPO Enco Free4",
            status: "no_standard_battery_service",
            message: "No standard Battery Service was exposed.",
          },
        ],
      }),
      false,
    ),
    {
      kind: "empty",
      summary: "已连接设备未暴露标准电量",
      title: "暂无可显示电量",
      detail: "OPPO Enco Free4 没有暴露标准 Battery Service。",
    },
  );
});

test("reports error state when no displayable devices are available and reads failed", () => {
  assert.deepEqual(
    describePanelState(
      result({
        connected_le_device_count: 1,
        errors: ["Keychron Z6 Ultra 8K: HRESULT 0x80070490"],
      }),
      false,
    ),
    {
      kind: "error",
      summary: "读取失败，稍后重试",
      title: "读取失败",
      detail: "Windows 蓝牙接口暂时没有返回可用电量，稍后会继续刷新。",
    },
  );
});

test("reports unreadable connected devices separately from missing battery service", () => {
  assert.deepEqual(
    describePanelState(
      result({
        connected_le_device_count: 1,
        issues: [
          {
            device_id: "keyboard",
            display_name: "Keychron Z6 Ultra 8K",
            status: "unreadable",
            message: "Windows found a standard battery path but did not return a readable value.",
          },
        ],
      }),
      false,
    ),
    {
      kind: "error",
      summary: "设备电量不可读",
      title: "设备电量不可读",
      detail: "Keychron Z6 Ultra 8K 已连接，但 Windows 暂时无法读取标准电量。",
    },
  );
});
