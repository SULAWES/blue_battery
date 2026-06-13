import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const readme = readFileSync(new URL("../README.md", import.meta.url), "utf8");

test("README explains the demo scope and Bluetooth battery boundary", () => {
  assert.match(readme, /当前连接/);
  assert.match(readme, /Windows 能读取/);
  assert.match(readme, /标准 BLE Battery Service/);
  assert.match(readme, /不支持私有协议/);
});

test("README tells demo users how to run and package the app", () => {
  assert.match(readme, /npm run tauri -- build/);
  assert.match(readme, /npm run demo:package/);
  assert.match(readme, /target\/release\/blue-battery\.exe/);
});

test("README documents tray controls and troubleshooting basics", () => {
  assert.match(readme, /托盘/);
  assert.match(readme, /刷新/);
  assert.match(readme, /诊断信息/);
  assert.match(readme, /不显示设备/);
});
