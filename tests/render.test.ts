import assert from "node:assert/strict";
import test from "node:test";

import {
  buildCommandErrorView,
  buildPanelView,
  buildTransientPanelView,
} from "../src/render.ts";
import { describePanelState, type RefreshResult } from "../src/panel_state.ts";

function result(partial: Partial<RefreshResult>): RefreshResult {
  return {
    devices: [],
    connected_le_device_count: 0,
    refreshed_at_ms: Date.UTC(2026, 5, 5, 1, 2, 3),
    errors: [],
    ...partial,
  };
}

test("builds a panel view for a displayable device", () => {
  const view = buildPanelView(
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
  );

  assert.match(view.summary, /^上次更新 /);
  assert.doesNotMatch(view.summary, /Keychron Z6 Ultra 8K/);
  assert.match(view.contentHtml, /class="device-list"/);
  assert.match(view.contentHtml, /Keychron Z6 Ultra 8K/);
  assert.doesNotMatch(view.contentHtml, /device-symbol/);
  assert.doesNotMatch(view.contentHtml, /&#xE83F;/);
  assert.equal(view.connectedBadge, "1 BLE");
  assert.equal(view.footerStatus, "就绪");
});

test("builds a transient loading view without a refresh result", () => {
  const view = buildTransientPanelView(describePanelState(null, true));

  assert.equal(view.summary, "正在读取");
  assert.match(view.contentHtml, /正在读取电量/);
  assert.equal(view.connectedBadge, "0 BLE");
  assert.equal(view.footerStatus, "就绪");
});

test("builds a command error view while preserving the last refresh metadata", () => {
  const view = buildCommandErrorView(
    "Bluetooth refresh failed",
    result({
      connected_le_device_count: 1,
      refreshed_at_ms: Date.UTC(2026, 5, 5, 3, 4, 5),
    }),
  );

  assert.match(view.summary, /^上次更新 /);
  assert.match(view.contentHtml, /读取失败/);
  assert.match(view.contentHtml, /Bluetooth refresh failed/);
  assert.equal(view.connectedBadge, "1 BLE");
  assert.equal(view.footerStatus, "就绪");
});
