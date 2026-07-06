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
    "--win-panel-solid",
    "--win-panel-material",
    "--win-bg",
    "--win-surface",
    "--win-card",
    "--win-stroke",
    "--win-stroke-highlight",
    "--win-hover",
    "--win-focus-ring",
    "--win-accent",
    "--win-shadow-flyout",
  ]) {
    assert.match(css, new RegExp(`${token}:`));
  }
});

test("uses layered native material surfaces with readable fallback", () => {
  assert.equal(tokenValue("--win-panel-solid"), "#f3f3f3");
  assert.equal(tokenValue("--win-panel-material"), "rgba(243, 243, 243, 0.92)");
  assert.equal(tokenValue("--win-surface"), "rgba(249, 249, 249, 0.88)");
  assert.equal(tokenValue("--win-card"), "rgba(255, 255, 255, 0.96)");
  assert.match(block(".shell"), /background:\s*var\(--win-panel-material\)/);
  assert.doesNotMatch(block(".device-row"), /background:\s*transparent/);
  assert.doesNotMatch(block(".shell"), /backdrop-filter/);
});

test("supports Windows light and dark shell appearances", () => {
  assert.match(css, /@media\s*\(prefers-color-scheme:\s*dark\)/);
  assert.match(css, /--win-panel-solid:\s*#202020/);
  assert.match(css, /--win-panel-material:\s*rgba\(32,\s*32,\s*32,\s*0\.9\)/);
  assert.match(css, /--win-card:\s*rgba\(43,\s*43,\s*43,\s*0\.94\)/);
});

test("prevents root-level scrollbars during panel entry motion", () => {
  assert.match(block("html"), /overflow:\s*hidden/);
  assert.match(block("body"), /overflow:\s*hidden/);
  assert.match(block("#app"), /overflow:\s*hidden/);
  assert.match(block(".shell"), /height:\s*100vh/);
  assert.doesNotMatch(block(".shell"), /min-height:\s*100vh/);
  assert.match(block(".content"), /overflow-x:\s*hidden/);
  assert.match(block(".content"), /overflow-y:\s*auto/);
});

test("uses a one pixel stroke and soft Fluent elevation", () => {
  assert.match(block(".shell"), /border:\s*1px solid var\(--win-stroke\)/);
  assert.match(block(".shell"), /box-shadow:\s*var\(--win-shadow-flyout\)/);
  assert.match(tokenValue("--win-shadow-flyout"), /0 18px 38px/);
  assert.match(tokenValue("--win-stroke-highlight"), /rgba\(255,\s*255,\s*255/);
  assert.doesNotMatch(tokenValue("--win-shadow-flyout"), /0 0 2px/);
});

test("uses compact WinUI-like control geometry", () => {
  assert.match(block(".settings-button"), /border-radius:\s*4px/);
  assert.match(block(".device-row"), /border-radius:\s*8px/);
  assert.doesNotMatch(block(".settings-button:active"), /transform:\s*translate/);
});

test("uses Segoe Fluent Icons for in-panel iconography", () => {
  assert.match(block(".fluent-icon"), /font-family:\s*"Segoe Fluent Icons"/);
  assert.equal(tokenValue("--win-icon"), "#3b3a39");
  assert.match(block(".settings-button"), /color:\s*var\(--win-icon\)/);
  assert.match(block(".settings-glyph"), /color:\s*var\(--win-icon\)/);
  assert.match(block(".settings-glyph"), /font-size:\s*16px/);
  assert.match(block(".menu-refresh-glyph"), /font-size:\s*14px/);
  assert.match(block(".menu-interval-glyph"), /font-size:\s*14px/);
});

test("places status and settings affordance in the footer", () => {
  assert.match(block(".footer"), /height:\s*36px/);
  assert.match(block(".settings-button"), /width:\s*28px/);
  assert.match(block(".settings-button"), /border-radius:\s*4px/);
  assert.match(block(".settings-menu"), /position:\s*absolute/);
});

test("uses a compact topbar BLE badge", () => {
  assert.match(block(".topbar-count"), /font-size:\s*12px/);
  assert.match(block(".topbar-count"), /justify-self:\s*end/);
});

test("uses a compact diagnostics flyout inside the panel", () => {
  assert.match(block(".diagnostics-panel"), /position:\s*absolute/);
  assert.match(block(".diagnostics-panel"), /bottom:\s*42px/);
  assert.match(block(".diagnostics-report"), /font-family:\s*Consolas/);
});

test("uses readable single-line settings menu items", () => {
  assert.match(block(".settings-menu"), /min-width:\s*220px/);
  assert.match(block(".menu-item"), /justify-content:\s*flex-start/);
  assert.match(block(".menu-item"), /text-align:\s*left/);
  assert.match(block(".menu-text"), /justify-self:\s*start/);
  assert.match(block(".menu-text"), /white-space:\s*nowrap/);
});

test("keeps function icons neutral and uses checkmarks for selected menu states", () => {
  for (const selector of [".menu-low-battery-glyph", ".menu-startup-glyph", ".menu-panel-glyph"]) {
    assert.match(block(selector), /color:\s*var\(--win-icon\)/);
  }

  assert.doesNotMatch(css, /\.menu-item\[aria-checked="true"\]\s+\.menu-low-battery-glyph/);
  assert.doesNotMatch(css, /\.menu-item\[aria-checked="true"\]\s+\.menu-startup-glyph/);
  assert.doesNotMatch(css, /\.menu-item\[aria-checked="true"\]\s+\.menu-panel-glyph/);
  assert.match(block(".menu-check"), /color:\s*var\(--win-accent\)/);
});

test("styles switched submenu views with back, chevron, check, and metadata affordances", () => {
  assert.match(block(".settings-menu-view"), /display:\s*grid/);
  assert.match(block(".settings-menu-header"), /height:\s*32px/);
  assert.match(block(".menu-back"), /grid-template-columns:\s*16px minmax\(0,\s*1fr\)/);
  assert.match(block(".menu-chevron"), /justify-self:\s*end/);
  assert.match(block(".menu-check"), /font-size:\s*14px/);
  assert.match(block(".menu-value"), /color:\s*var\(--win-text-tertiary\)/);
  assert.match(
    css,
    /\.menu-item\[aria-checked="false"\]\s+\.menu-check\s*\{[^}]*opacity:\s*0/s,
  );
});

test("adds a restrained entry motion with reduced-motion fallback", () => {
  assert.match(
    css,
    /\.shell\[data-entering="true"\]\s*\{[^}]*transform-origin:\s*bottom right[^}]*animation:\s*panel-enter/s,
  );
  assert.match(css, /@keyframes\s+panel-enter/);
  assert.match(css, /@media\s*\(prefers-reduced-motion:\s*reduce\)/);
  assert.match(
    css,
    /@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{[\s\S]*\.shell\[data-entering="true"\]\s*\{[^}]*animation:\s*none/s,
  );
});

test("keeps native control focus states visible without blue icon noise", () => {
  assert.match(css, /:focus-visible/);
  assert.match(block(".settings-button:focus-visible"), /outline:\s*2px solid var\(--win-focus-ring\)/);
  assert.match(block(".menu-item:focus-visible"), /background:\s*var\(--win-hover\)/);
});
