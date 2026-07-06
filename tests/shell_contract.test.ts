import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const source = readFileSync(new URL("../src/main.ts", import.meta.url), "utf8");

test("moves refresh from the topbar into the footer settings menu", () => {
  assert.doesNotMatch(source, /<button id="refresh"/);
  assert.match(source, /id="settings"/);
  assert.match(source, /id="settings-menu"/);
  assert.match(source, /id="menu-refresh"/);
});

test("renders a compact BLE count in the title bar", () => {
  assert.match(source, /id="connected-badge"/);
  assert.match(source, /class="topbar-count"/);
});

test("exposes diagnostics from the settings menu", () => {
  assert.match(source, /id="menu-diagnostics"/);
  assert.match(source, /id="diagnostics-panel"/);
  assert.match(source, /get_diagnostics_report/);
});

test("exposes startup toggle from the settings menu", () => {
  assert.match(source, /id="menu-startup"/);
  assert.match(source, /role="menuitemcheckbox"/);
  assert.match(source, /get_startup_enabled/);
  assert.match(source, /set_startup_enabled/);
});

test("exposes startup registry cleanup from the settings menu", () => {
  assert.match(source, /id="menu-clear-startup"/);
  assert.match(source, /clear_startup_entry/);
  assert.match(source, /清理开机自启动项/);
});

test("organizes settings into main and secondary menu views", () => {
  assert.match(source, /id="settings-menu-main"/);
  assert.match(source, /id="settings-menu-refresh"/);
  assert.match(source, /id="settings-menu-low-battery"/);
  assert.match(source, /id="settings-menu-threshold"/);
  assert.match(source, /id="settings-menu-startup"/);
  assert.match(source, /id="settings-menu-diagnostics"/);
  assert.match(source, /id="settings-menu-about"/);
  assert.match(source, /data-menu-view/);
  assert.match(source, /settingsMenuViewStack/);
});

test("uses explicit submenu choices for refresh interval and low battery threshold", () => {
  for (const seconds of [120, 60, 30]) {
    assert.match(source, new RegExp(`id="menu-refresh-interval-${seconds}"`));
  }

  for (const threshold of [10, 15, 20, 25]) {
    assert.match(source, new RegExp(`id="menu-low-battery-threshold-${threshold}"`));
  }

  assert.doesNotMatch(source, /nextNumberOption/);
});

test("uses checkmarks for checkbox and radio menu states", () => {
  assert.match(
    source,
    /id="menu-low-battery-status"[\s\S]*?<span class="fluent-icon menu-check"/,
  );
  assert.match(
    source,
    /id="menu-startup"[\s\S]*?<span class="fluent-icon menu-check"/,
  );
  assert.match(
    source,
    /id="menu-show-panel-on-startup"[\s\S]*?<span class="fluent-icon menu-check"/,
  );
});

test("exposes a quiet low battery notification toggle without forcing Windows toasts", () => {
  const notificationButton =
    source.match(
      /<button id="menu-low-battery-system-notification"[\s\S]*?<\/button>/,
    )?.[0] ?? "";

  assert.match(source, /id="menu-low-battery-system-notification"/);
  assert.match(source, /lowBatterySystemNotificationEnabled/);
  assert.doesNotMatch(notificationButton, /disabled/);
  assert.match(source, /已更新低电量通知/);
});

test("exposes startup preference, diagnostics copy, about, reset, and exit actions", () => {
  assert.match(source, /id="menu-show-panel-on-startup"/);
  assert.match(source, /showPanelOnStartup/);
  assert.match(source, /id="menu-copy-diagnostics"/);
  assert.match(source, /id="menu-copy-device-summary"/);
  assert.match(source, /copyTextToClipboard/);
  assert.match(source, /id="menu-about-version"/);
  assert.match(source, /get_app_version/);
  assert.match(source, /id="menu-reset-settings"/);
  assert.match(source, /id="menu-exit"/);
  assert.match(source, /exit_app/);
});

test("loads and updates persisted user settings", () => {
  assert.match(source, /get_settings/);
  assert.match(source, /update_settings/);
  assert.match(source, /reset_settings/);
  assert.match(source, /refreshIntervalSeconds/);
  assert.match(source, /lowBatteryThreshold/);
  assert.doesNotMatch(source, /60_000/);
});

test("does not run a second frontend auto refresh loop", () => {
  assert.doesNotMatch(source, /setInterval/);
  assert.doesNotMatch(source, /autoRefreshTimer/);
});

test("closes the panel with Escape when no inner flyout is open", () => {
  assert.match(source, /hide_panel/);
  assert.match(source, /invoke\s*<\s*void\s*>\("hide_panel"\)/);
  assert.match(source, /event\.key\s*===\s*"Escape"/);
  assert.match(source, /backSettingsMenuView\(\)/);
});
