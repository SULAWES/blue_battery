import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const packageJson = JSON.parse(
  readFileSync(new URL("../package.json", import.meta.url), "utf8"),
);
const gitignore = readFileSync(new URL("../.gitignore", import.meta.url), "utf8");

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
});

test("generated demo packages stay out of git", () => {
  assert.match(gitignore, /^release\/$/m);
});
