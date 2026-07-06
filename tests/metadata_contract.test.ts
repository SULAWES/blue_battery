import assert from "node:assert/strict";
import { readFileSync, statSync } from "node:fs";
import test from "node:test";

const packageJson = JSON.parse(
  readFileSync(new URL("../package.json", import.meta.url), "utf8"),
);
const tauriConfig = JSON.parse(
  readFileSync(new URL("../src-tauri/tauri.conf.json", import.meta.url), "utf8"),
);
const cargoToml = readFileSync(
  new URL("../src-tauri/Cargo.toml", import.meta.url),
  "utf8",
);

test("keeps package, Cargo, and Tauri versions in sync", () => {
  const cargoVersion = cargoToml.match(/^version\s*=\s*"([^"]+)"/m)?.[1];

  assert.equal(packageJson.version, "0.2.0");
  assert.equal(cargoVersion, packageJson.version);
  assert.equal(tauriConfig.version, packageJson.version);
});

test("declares the demo application icon in Tauri metadata", () => {
  assert.equal(tauriConfig.productName, "Blue Battery");
  assert.equal(tauriConfig.identifier, "com.sulaw.bluebattery");
  assert.deepEqual(tauriConfig.bundle.icon, ["icons/icon.ico"]);

  const icon = statSync(new URL("../src-tauri/icons/icon.ico", import.meta.url));
  assert.ok(icon.size > 1024, "icon.ico should be a real icon asset");
});

test("configures the tray panel window for Windows native material", () => {
  const mainWindow = tauriConfig.app.windows.find(
    (window: { label?: string }) => window.label === "main",
  );

  assert.ok(mainWindow, "main Tauri window should be declared");
  assert.equal(mainWindow.transparent, true);
  assert.equal(mainWindow.decorations, false);
  assert.deepEqual(mainWindow.windowEffects.effects, ["mica"]);
});
