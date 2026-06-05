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
