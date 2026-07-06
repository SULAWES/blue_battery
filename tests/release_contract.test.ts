import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const packageJson = JSON.parse(
  readFileSync(new URL("../package.json", import.meta.url), "utf8"),
);
const gitignore = readFileSync(new URL("../.gitignore", import.meta.url), "utf8");
const cargoToml = readFileSync(
  new URL("../src-tauri/Cargo.toml", import.meta.url),
  "utf8",
);
const changelog = readFileSync(
  new URL("../CHANGELOG.md", import.meta.url),
  "utf8",
);

test("defines explicit release and demo packaging scripts", () => {
  assert.equal(packageJson.scripts["tauri:build:release"], "tauri build");
  assert.match(packageJson.scripts["demo:package"], /package-demo\.ps1/);
});

test("demo packaging script packages the release executable only", () => {
  const script = readFileSync(
    new URL("../scripts/package-demo.ps1", import.meta.url),
    "utf8",
  );

  assert.match(script, /target[\\/]release[\\/]blue-battery\.exe/);
  assert.doesNotMatch(script, /target[\\/]debug[\\/]blue-battery\.exe/);
  assert.match(script, /BlueBattery-demo-v/);
  assert.match(script, /CHANGELOG\.md/);
});

test("generated demo packages stay out of git", () => {
  assert.match(gitignore, /^release\/$/m);
});

test("renders Fluent tray icons at build time instead of runtime", () => {
  const buildDependencies = cargoToml.match(
    /\[build-dependencies\]([\s\S]*?)(?:\n\[|$)/,
  )?.[1] ?? "";
  const runtimeDependencies = cargoToml.match(
    /\[dependencies\]([\s\S]*?)(?:\n\[|$)/,
  )?.[1] ?? "";

  assert.match(buildDependencies, /^resvg\s*=/m);
  assert.doesNotMatch(runtimeDependencies, /^resvg\s*=/m);
});

test("documents the 0.2.0 public release scope", () => {
  assert.match(changelog, /## 0\.2\.0/);
  assert.match(changelog, /portable zip/i);
  assert.match(changelog, /configurable background refresh/i);
  assert.match(changelog, /low battery threshold/i);
  assert.match(changelog, /structured diagnostics/i);
  assert.match(changelog, /标准 BLE Battery Service/);
  assert.match(changelog, /不支持私有协议/);
  assert.match(changelog, /Windows Toast/);
});
