import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const css = readFileSync(new URL("../src/styles.css", import.meta.url), "utf8");

function block(selector: string) {
  const match = css.match(new RegExp(`${selector.replace(".", "\\.")}\\s*\\{([^}]*)\\}`));

  assert.ok(match, `Missing ${selector} rule.`);
  return match[1];
}

function tokenValue(token: string) {
  const match = css.match(new RegExp(`${token}:\\s*([^;]+);`));

  assert.ok(match, `Missing ${token} token.`);
  return match[1].trim();
}

test("defines Windows panel design tokens", () => {
  for (const token of [
    "--win-bg",
    "--win-surface",
    "--win-card",
    "--win-border",
    "--win-accent",
    "--win-shadow-flyout",
  ]) {
    assert.match(css, new RegExp(`${token}:`));
  }
});

test("uses opaque Windows panel surfaces instead of translucent glass", () => {
  assert.equal(tokenValue("--win-bg"), "#f3f3f3");
  assert.equal(tokenValue("--win-surface"), "#f9f9f9");
  assert.equal(tokenValue("--win-card"), "#ffffff");
  assert.doesNotMatch(block(".shell"), /backdrop-filter/);
});

test("supports Windows light and dark shell appearances", () => {
  assert.match(css, /@media\s*\(prefers-color-scheme:\s*dark\)/);
  assert.match(css, /--win-bg:\s*#202020/);
  assert.match(css, /--win-card:\s*#2b2b2b/);
});

test("uses compact WinUI-like control geometry", () => {
  assert.match(block(".icon-button"), /border-radius:\s*4px/);
  assert.match(block(".device-row"), /border-radius:\s*8px/);
  assert.doesNotMatch(block(".icon-button:active"), /transform:\s*translate/);
});

test("uses Segoe Fluent Icons for in-panel iconography", () => {
  assert.match(block(".fluent-icon"), /font-family:\s*"Segoe Fluent Icons"/);
  assert.match(block(".refresh-glyph"), /font-family:\s*"Segoe Fluent Icons"/);
  assert.match(block(".device-symbol"), /font-family:\s*"Segoe Fluent Icons"/);
});
