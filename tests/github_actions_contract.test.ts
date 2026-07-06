import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const ciWorkflow = readFileSync(
  new URL("../.github/workflows/ci.yml", import.meta.url),
  "utf8",
);
const releaseWorkflow = readFileSync(
  new URL("../.github/workflows/release.yml", import.meta.url),
  "utf8",
);

test("CI workflow validates the Windows app on pushes and pull requests", () => {
  assert.match(ciWorkflow, /pull_request:/);
  assert.match(ciWorkflow, /branches:\s*\n\s+- master/);
  assert.match(ciWorkflow, /runs-on:\s*windows-latest/);
  assert.match(ciWorkflow, /actions\/checkout@v4/);
  assert.match(ciWorkflow, /actions\/setup-node@v4/);
  assert.match(ciWorkflow, /node-version:\s*"22"/);
  assert.match(ciWorkflow, /npm ci/);
  assert.match(ciWorkflow, /npm run test:panel/);
  assert.match(ciWorkflow, /cargo test --manifest-path src-tauri\/Cargo\.toml/);
  assert.match(ciWorkflow, /npm run build/);
  assert.match(ciWorkflow, /npm run tauri -- build --debug/);
});

test("release workflow publishes a tag-matched portable demo zip", () => {
  assert.match(releaseWorkflow, /tags:\s*\n\s+- "v\*"/);
  assert.match(releaseWorkflow, /permissions:\s*\n\s+contents:\s*write/);
  assert.match(releaseWorkflow, /runs-on:\s*windows-latest/);
  assert.match(releaseWorkflow, /Release tag '\$env:GITHUB_REF_NAME' does not match package version/);
  assert.match(releaseWorkflow, /BLUE_BATTERY_ZIP=release\/BlueBattery-demo-v\$version\.zip/);
  assert.match(releaseWorkflow, /npm run test:panel/);
  assert.match(releaseWorkflow, /cargo test --manifest-path src-tauri\/Cargo\.toml/);
  assert.match(releaseWorkflow, /npm run demo:package/);
  assert.match(releaseWorkflow, /CHANGELOG\.md does not contain release notes/);
  assert.match(releaseWorkflow, /actions\/upload-artifact@v4/);
  assert.match(releaseWorkflow, /gh release create/);
  assert.match(releaseWorkflow, /--verify-tag/);
});
