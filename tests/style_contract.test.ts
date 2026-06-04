import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const css = readFileSync(new URL("../src/styles.css", import.meta.url), "utf8");

function block(selector: string) {
  const match = css.match(new RegExp(`${selector.replace(".", "\\.")}\\s*\\{([^}]*)\\}`));

  assert.ok(match, `Missing ${selector} rule.`);
  return match[1];
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

test("supports Windows light and dark shell appearances", () => {
  assert.match(css, /@media\s*\(prefers-color-scheme:\s*dark\)/);
  assert.match(css, /\.shell\s*\{[\s\S]*backdrop-filter:\s*blur\(/);
});

test("uses compact WinUI-like control geometry", () => {
  assert.match(block(".icon-button"), /border-radius:\s*4px/);
  assert.match(block(".device-row"), /border-radius:\s*4px/);
});
