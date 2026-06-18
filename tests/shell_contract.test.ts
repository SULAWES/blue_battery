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

test("closes the panel with Escape when no inner flyout is open", () => {
  assert.match(source, /hide_panel/);
  assert.match(source, /invoke\s*<\s*void\s*>\("hide_panel"\)/);
  assert.match(source, /event\.key\s*===\s*"Escape"/);
});
