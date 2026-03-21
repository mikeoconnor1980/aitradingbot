---
name: html-wireframes
description: "Generate standalone HTML wireframe files for PBI mockups and user journeys. Produces self-contained .html files viewable in any browser, shareable with stakeholders, and importable into DTS.Design. Replaces Excalidraw for wireframe generation — no subagent delegation needed."
---

# HTML Wireframes Skill

## Overview

Generate self-contained HTML wireframe files that:
- **Open in any browser** — no tooling required to view
- **Are shareable** — email, embed in PRDs, paste in chat
- **Are importable** — DTS.Design will parse these into canvas objects (M3+)
- **Are fast to generate** — direct string output, no subagent delegation, no JSON bloat

## When to Use

- User asks for a wireframe, mockup, or screen sketch
- PBI review needs a visual to clarify layout
- `pbi-to-journey` skill needs step mockups
- Any agent needs to produce a visual artifact during design work

## When NOT to Use

- Architecture diagrams or flowcharts → use Excalidraw or Mermaid
- Sequence diagrams → use Mermaid
- High-fidelity design work → use DTS.Design itself

## Output Location

| Artifact | Path |
|----------|------|
| Screen mockup | `.agent-context/1-discover/wireframes/mockup_[name].html` |
| Journey step mockup | `.agent-context/1-discover/wireframes/mockup_[journey]_step_[N].html` |
| Journey overview | `.agent-context/1-discover/wireframes/journey_[name].html` |

## HTML Template

Every wireframe is a **single self-contained HTML file** with inline CSS. No external dependencies.

### Base Template

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{{TITLE}} — Wireframe</title>
  <link href="https://fonts.googleapis.com/css2?family=Open+Sans:wght@300;400;600;700&display=swap" rel="stylesheet">
  <style>
    /* ============================================================
       DDX/DDS Design System — Wireframe Stylesheet
       Extracted from the live DDX Front-End Showcase
       https://dtsresources-dev.tax.deloitte.co.uk/extensions/front-end/
       ============================================================ */

    /* === DDX Colour Palette (SCSS variable names in comments) === */
    :root {
      /* Primary */
      --ddx-black: #000000;              /* $black */
      --ddx-white: #ffffff;              /* $white */
      --ddx-red: #da291c;               /* $red */
      --ddx-orange: #ed8b00;            /* $orange */
      --ddx-yellow: #ffcd00;            /* $yellow */
      /* Brand */
      --ddx-deloitte-green: #86bc25;    /* $deloitte-green */
      /* Primary Green */
      --ddx-primary-green-1: #43b02a;   /* $primary-green-1 */
      --ddx-primary-green-2: #26890d;   /* $primary-green-2 — main action colour */
      --ddx-primary-green-3: #046a38;   /* $primary-green-3 */
      /* Primary Teal & Blue */
      --ddx-primary-teal: #0d8390;      /* $primary-teal */
      --ddx-primary-blue: #007cb0;      /* $primary-blue */
      /* Grays */
      --ddx-gray-1: #d0d0ce;            /* $gray-1 — borders, dividers */
      --ddx-gray-2: #bbbcbc;            /* $gray-2 */
      --ddx-gray-3: #a7a8aa;            /* $gray-3 */
      --ddx-gray-4: #97999b;            /* $gray-4 */
      --ddx-gray-5: #75787b;            /* $gray-5 */
      --ddx-gray-6: #63666a;            /* $gray-6 */
      --ddx-gray-7: #53565a;            /* $gray-7 — secondary button bg */
      --ddx-gray-8: #27282a;            /* $gray-8 — dark surfaces, page-header */
      /* Background Grays */
      --ddx-bg-gray-1: #fafafa;         /* $background-gray-1 */
      --ddx-bg-gray-2: #f5f5f5;         /* $background-gray-2 */
      --ddx-bg-gray-3: #ebebeb;         /* $background-gray-3 */
      /* Semantic tints (badge / banner backgrounds) */
      --ddx-success-bg: #e1ebde;        /* green badge bg */
      --ddx-info-bg: #d9ebf3;           /* blue badge bg */
      --ddx-warning-bg: #f6ecdd;        /* orange badge bg */
      --ddx-error-bg: #f3e1e0;          /* red badge bg */
      --ddx-banner-success-bg: #f4f9f3; /* banner success bg */
    }

    /* === Reset === */
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

    /* === Typography (Open Sans — DDX standard) === */
    body {
      font-family: 'Open Sans', sans-serif;
      font-size: 14px;
      line-height: 1.5;
      background: var(--ddx-bg-gray-2);
      color: var(--ddx-black);
      padding: 24px;
    }

    /* === Wireframe Chrome === */
    .wf-page {
      max-width: 1200px;
      margin: 0 auto;
      background: var(--ddx-white);
      border: 1px solid var(--ddx-gray-1);
      overflow: hidden;
    }
    .wf-meta {
      background: var(--ddx-bg-gray-3);
      padding: 8px 16px;
      font-size: 11px;
      color: var(--ddx-gray-6);
      border-bottom: 1px solid var(--ddx-gray-1);
      display: flex;
      justify-content: space-between;
    }

    /* === App Shell — DDS Header === */
    .wf-header {
      background: var(--ddx-black);
      color: var(--ddx-white);
      padding: 0 24px;
      height: 48px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 14px;
    }
    .wf-header .logo {
      font-weight: 700;
      font-size: 18px;
      letter-spacing: -0.5px;
    }
    .wf-header .logo::after {
      content: '.';
      color: var(--ddx-deloitte-green);
    }
    .wf-header .app-name {
      color: var(--ddx-white);
      font-size: 14px;
      font-weight: 400;
      margin-left: 12px;
      padding-left: 12px;
      border-left: 1px solid var(--ddx-gray-7);
    }
    .wf-header .nav { display: flex; gap: 16px; align-items: center; }
    .wf-header .nav a { color: var(--ddx-white); text-decoration: none; font-size: 13px; }
    .wf-header .avatar {
      width: 32px; height: 32px; border-radius: 50%;
      background: var(--ddx-gray-7); color: var(--ddx-white);
      display: flex; align-items: center; justify-content: center;
      font-size: 12px; font-weight: 600;
    }

    /* === DDX Side Navigation === */
    .wf-sidebar {
      width: 300px;
      min-width: 300px;
      background: var(--ddx-black);
      color: var(--ddx-white);
      font-size: 14px;
      padding: 0;
    }
    .wf-sidebar .nav-item {
      padding: 10px 16px;
      border-left: 4px solid var(--ddx-black);
      cursor: pointer;
      color: var(--ddx-white);
      font-weight: 400;
    }
    .wf-sidebar .nav-item:hover {
      background: var(--ddx-gray-8);
    }
    .wf-sidebar .nav-item.active {
      background: var(--ddx-primary-green-2);
      border-left-color: var(--ddx-primary-green-2);
      font-weight: 700;
      color: var(--ddx-white);
    }
    .wf-sidebar .nav-group {
      background: var(--ddx-gray-8);
    }
    .wf-sidebar .nav-group .nav-item {
      padding-left: 28px;
      border-left: 4px solid var(--ddx-gray-7);
    }
    .wf-sidebar .nav-group .nav-item.active {
      background: var(--ddx-gray-7);
      border-left-color: var(--ddx-primary-green-2);
      font-weight: 700;
    }

    .wf-body { display: flex; min-height: 500px; }
    .wf-content { flex: 1; padding: 24px; }

    /* === DDX Page Header (dark banner at top of content) === */
    .wf-page-header {
      background: var(--ddx-gray-8);
      color: var(--ddx-white);
      padding: 24px 40px;
    }
    .wf-page-header .wf-breadcrumb { color: var(--ddx-gray-3); margin-bottom: 8px; }
    .wf-page-header .wf-breadcrumb a { color: var(--ddx-primary-green-2); text-decoration: none; }
    .wf-page-header .wf-page-title {
      color: var(--ddx-white);
      margin-bottom: 0;
    }

    /* === UI Elements === */
    .wf-card {
      border: 1px solid var(--ddx-gray-1);
      padding: 16px;
      margin-bottom: 16px;
      background: var(--ddx-white);
    }
    .wf-card-title {
      font-size: 16px;
      font-weight: 600;
      margin-bottom: 12px;
      padding-bottom: 8px;
      border-bottom: 1px solid var(--ddx-bg-gray-3);
    }

    /* === DDS Form Inputs === */
    .wf-input {
      display: block;
      width: 100%;
      padding: 5px 12px;
      outline: 0.8px solid var(--ddx-gray-1);
      border: none;
      font-size: 14px;
      font-family: 'Open Sans', sans-serif;
      background: var(--ddx-white);
      height: 32px;
      margin-bottom: 16px;
    }
    .wf-input:focus { outline-color: var(--ddx-primary-green-2); outline-width: 2px; }
    .wf-label {
      display: block;
      font-size: 12px;
      font-weight: 400;
      color: var(--ddx-black);
      margin-bottom: 4px;
    }
    .wf-textarea {
      display: block;
      width: 100%;
      min-height: 80px;
      padding: 5px 12px;
      outline: 0.8px solid var(--ddx-gray-1);
      border: none;
      font-size: 14px;
      font-family: 'Open Sans', sans-serif;
      background: var(--ddx-white);
      margin-bottom: 16px;
      resize: vertical;
    }
    .wf-select {
      display: block;
      width: 100%;
      padding: 5px 12px;
      outline: 0.8px solid var(--ddx-gray-1);
      border: none;
      font-size: 14px;
      font-family: 'Open Sans', sans-serif;
      background: var(--ddx-white);
      height: 32px;
      margin-bottom: 16px;
      appearance: auto;
    }

    /* === DDS Buttons (square corners — zero border-radius) === */
    .wf-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 5px 12px;
      height: 32px;
      font-size: 14px;
      font-weight: 400;
      font-family: 'Open Sans', sans-serif;
      border: none;
      cursor: pointer;
      margin-right: 8px;
      color: var(--ddx-white);
    }
    .wf-btn-primary { background: var(--ddx-primary-green-2); }
    .wf-btn-secondary { background: var(--ddx-gray-7); }
    .wf-btn-danger { background: var(--ddx-red); }
    .wf-btn-disabled { background: var(--ddx-gray-1); color: var(--ddx-gray-4); cursor: not-allowed; }

    /* === DDX AG Grid / Data Table === */
    .wf-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 14px;
    }
    .wf-table th {
      background: var(--ddx-white);
      text-align: left;
      padding: 0 16px;
      height: 44px;
      border-bottom: 0.8px solid var(--ddx-gray-1);
      font-weight: 700;
      font-size: 13px;
      color: var(--ddx-gray-7);
    }
    .wf-table td {
      padding: 0 16px;
      height: 49px;
      border-bottom: 0.8px solid var(--ddx-gray-1);
      color: var(--ddx-black);
      line-height: 49px;
    }
    .wf-table tr:hover td {
      background: var(--ddx-bg-gray-1);
    }

    /* === DDX Banners (full-width notification bars) === */
    .wf-banner {
      padding: 12px 16px;
      font-size: 14px;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .wf-banner-success {
      background: var(--ddx-banner-success-bg);
      color: var(--ddx-primary-green-2);
      border-bottom: 0.8px solid var(--ddx-primary-green-2);
    }
    .wf-banner-warning {
      background: var(--ddx-red);
      color: var(--ddx-white);
      border-bottom: 0.8px solid var(--ddx-red);
    }

    /* === DDX Badges (rounded or squared, various colours) === */
    .wf-badge {
      display: inline-block;
      padding: 5px 12px;
      font-size: 14px;
      font-weight: 600;
    }
    .wf-badge-rounded { border-radius: 4px; }
    .wf-badge-round { border-radius: 16000px; }
    .wf-badge-squared { border-radius: 0; }
    .wf-badge-success {
      background: var(--ddx-success-bg); color: var(--ddx-primary-green-2);
      border: 0.8px solid var(--ddx-primary-green-2);
    }
    .wf-badge-info {
      background: var(--ddx-info-bg); color: var(--ddx-primary-blue);
      border: 0.8px solid var(--ddx-primary-blue);
    }
    .wf-badge-warning {
      background: var(--ddx-warning-bg); color: #896129;
      border: 0.8px solid var(--ddx-warning-bg);
    }
    .wf-badge-error {
      background: var(--ddx-error-bg); color: var(--ddx-red);
      border: 0.8px solid var(--ddx-error-bg);
    }
    .wf-badge-neutral {
      background: var(--ddx-bg-gray-2); color: var(--ddx-gray-6);
      border: 0.8px solid var(--ddx-bg-gray-2);
    }
    .wf-badge-emphasis {
      background: var(--ddx-bg-gray-3); color: var(--ddx-black);
      border: 0.8px solid var(--ddx-black);
    }

    .wf-empty {
      text-align: center;
      padding: 48px 24px;
      color: var(--ddx-gray-4);
    }
    .wf-empty .icon { font-size: 48px; margin-bottom: 12px; }

    .wf-placeholder {
      background: var(--ddx-bg-gray-2);
      border: 2px dashed var(--ddx-gray-1);
      padding: 24px;
      text-align: center;
      color: var(--ddx-gray-4);
      font-size: 13px;
    }

    /* === DDX Breadcrumbs === */
    .wf-breadcrumb {
      font-size: 12px;
      color: var(--ddx-gray-5);
      margin-bottom: 16px;
    }
    .wf-breadcrumb a,
    .wf-breadcrumb span { color: var(--ddx-primary-green-2); text-decoration: none; }
    .wf-breadcrumb .separator { color: var(--ddx-gray-5); margin: 0 6px; }

    /* === DDX Headings === */
    .wf-page-title {
      font-size: 32px;
      font-weight: 600;
      letter-spacing: -0.5px;
      line-height: 40px;
      margin-bottom: 20px;
    }
    h2.wf-section-title {
      font-size: 24px;
      font-weight: 700;
      letter-spacing: -0.3px;
      line-height: 28px;
      margin-bottom: 16px;
    }

    /* === DDX Tabs (green underline indicator) === */
    .wf-tabs {
      display: flex;
      gap: 0;
      border-bottom: 1px solid var(--ddx-gray-1);
      margin-bottom: 16px;
    }
    .wf-tab {
      padding: 8px 12px;
      font-size: 14px;
      cursor: pointer;
      position: relative;
      color: var(--ddx-black);
    }
    .wf-tab.active::after {
      content: '';
      position: absolute;
      bottom: -1px;
      left: 0; right: 0;
      height: 2px;
      background: var(--ddx-primary-green-2);
    }

    .wf-grid { display: grid; gap: 16px; }
    .wf-grid-2 { grid-template-columns: 1fr 1fr; }
    .wf-grid-3 { grid-template-columns: 1fr 1fr 1fr; }

    /* === DDX Modal (square corners, dark overlay) === */
    .wf-modal-overlay {
      position: fixed; inset: 0;
      background: rgba(0,0,0,0.32);
      display: flex; align-items: center; justify-content: center;
    }
    .wf-modal {
      background: var(--ddx-white);
      padding: 14px;
      width: 400px;
      max-width: 90vw;
    }
    .wf-modal-title {
      font-size: 18px;
      font-weight: 700;
      margin-bottom: 16px;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .wf-modal-body { margin-bottom: 16px; font-size: 14px; }
    .wf-modal-footer { display: flex; justify-content: flex-end; gap: 8px; }

    /* === Status Indicators (coloured shapes) === */
    .wf-status {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 13px;
    }
    .wf-status-dot {
      width: 10px; height: 10px;
      border-radius: 50%;
      display: inline-block;
    }
    .wf-status-dot.success { background: var(--ddx-primary-green-2); }
    .wf-status-dot.warning { background: var(--ddx-orange); }
    .wf-status-dot.error { background: var(--ddx-red); }
    .wf-status-dot.info { background: var(--ddx-primary-blue); }
    .wf-status-dot.neutral { background: var(--ddx-gray-4); }

    /* === Annotations === */
    .wf-annotation {
      position: relative;
      display: inline-block;
    }
    .wf-annotation::after {
      content: attr(data-note);
      position: absolute;
      top: -6px;
      right: -12px;
      background: var(--ddx-orange);
      color: var(--ddx-white);
      font-size: 10px;
      padding: 2px 6px;
      border-radius: 10px;
      white-space: nowrap;
      pointer-events: none;
    }

    /* === DTS.Design Import Hints === */
    /* data-wf-type: component type for canvas import */
    /* data-wf-props: JSON properties for canvas import */

    /* === Sidebar Toggle (CSS-only) === */
    .nav-toggle-input { display: none; }
    .nav-toggle-label { display: none; cursor: pointer; }

    /* === Responsive Mobile === */
    @media (max-width: 768px) {
      body { padding: 8px; }
      .nav-toggle-label {
        display: flex; align-items: center; justify-content: center;
        width: 32px; height: 32px; font-size: 18px; color: var(--ddx-white);
      }
      .wf-body { position: relative; }
      .wf-sidebar {
        display: none; position: absolute; top: 0; left: 0; bottom: 0; z-index: 100;
      }
      #nav-toggle:checked ~ .wf-page .wf-sidebar { display: block; }
      .wf-header { padding: 0 12px; }
      .wf-header .app-name { display: none; }
      .wf-header .nav { gap: 8px; }
      .wf-header .nav a { font-size: 11px; }
      .wf-page-header { padding: 16px; }
      .wf-page-title { font-size: 22px; line-height: 28px; }
      .wf-content { padding: 12px; }
      .wf-grid-2, .wf-grid-3, .wf-grid-4 { grid-template-columns: 1fr; }
      .wf-grid[style] { grid-template-columns: 1fr !important; }
      .wf-table { display: block; overflow-x: auto; }
      .wf-card { padding: 12px; }
      .wf-banner { flex-direction: column; gap: 8px; align-items: flex-start; }
      .wf-tabs { flex-wrap: wrap; }
    }
  </style>
</head>
<body>
  <input type="checkbox" id="nav-toggle" class="nav-toggle-input">
  <div class="wf-meta">
    <span>{{PBI_REF}} — {{SCREEN_NAME}}</span>
    <span>DTS.Design Wireframe</span>
  </div>
  <div class="wf-page">
    {{CONTENT}}
  </div>
</body>
</html>
```

## DDX/DDS Design Reference

Quick reference for the design tokens and patterns used in the wireframe stylesheet above. All values were extracted from the live DDX Front-End Showcase.

### Colour Palette

| Variable | Hex | Usage |
|----------|-----|-------|
| `$black` | `#000000` | Header bg, sidebar bg, text |
| `$white` | `#ffffff` | Content bg, header text |
| `$red` | `#da291c` | Error, danger, warning banner bg |
| `$orange` | `#ed8b00` | Warning badge text, annotations |
| `$yellow` | `#ffcd00` | Limited use |
| `$deloitte-green` | `#86bc25` | Brand accent (logo dot) |
| `$primary-green-2` | `#26890d` | **Primary action colour** — buttons, active tabs, active nav, links |
| `$gray-1` | `#d0d0ce` | Borders, dividers, input outlines |
| `$gray-7` | `#53565a` | Secondary button bg, active child nav bg |
| `$gray-8` | `#27282a` | Dark surfaces, page-header bg, child nav container |
| `$background-gray-2` | `#f5f5f5` | Page background, neutral badge bg |

### Typography

| Element | Font | Weight | Size | Line‑height | Spacing |
|---------|------|--------|------|-------------|---------|
| Body | Open Sans | 400 (Regular) | 14px | 20px | 0 |
| H1 | Open Sans | 600 (Semibold) | 32px | 40px | -0.5 |
| H2 | Open Sans | 700 (Bold) | 24px | 28px | -0.3 |
| H4 | Open Sans | 700 (Bold) | 20px | 30px | -0.2 |
| H6 | Open Sans | 700 (Bold) | 16px | 24px | 0 |
| Small / Label | Open Sans | 400 | 12px | 16px | 0 |
| Table header | Open Sans | 700 | 13px | — | 0 |

### Key DDX/DDS Component Patterns

| Component | Key Visual Characteristics |
|-----------|---------------------------|
| **Header** | Black bg, 48px tall, Deloitte logo with green dot, white text |
| **Side Navigation** | Black bg, 300px wide, 4px left border accent, green = active parent, gray-7 = active child |
| **Page Header** | Dark gray-8 bg, white text, breadcrumbs + H1 title section |
| **Buttons** | **Square corners (0 border-radius)**, 32px height, 5px 12px padding, green primary / gray-7 secondary |
| **Inputs** | **Square corners**, 32px height, 0.8px gray-1 outline (not border), white bg |
| **Tabs** | No bg change; active = 2px green bottom indicator (`::after` pseudo-element) |
| **AG Grid / Table** | White header, 0.8px gray-1 row borders, 13px bold gray-7 header text, 49px row height |
| **Badges** | 5px 12px padding, 600 weight, rounded (4px) / round (pill) / squared (0) variants per semantic colour |
| **Banners** | Full-width, success = light green bg + green text, warning = red bg + white text |
| **Modals** | **Square corners**, white bg, 14px padding, 400px width, rgba(0,0,0,0.32) overlay |
| **Breadcrumbs** | 12px text, green links, `/` separators |

### Critical Style Rules

1. **Zero border-radius everywhere** — Buttons, inputs, modals, tables, cards all use square corners. This is the most distinctive DDX trait vs generic Material/Bootstrap designs.
2. **Green = `#26890d`** — Not the Deloitte brand green (`#86bc25`). The interactive/action green is `$primary-green-2`.
3. **Open Sans** — The only font. Never use system fonts or other web fonts in wireframes.
4. **Black surfaces** — Header and sidebar are pure `#000000`, not dark gray.
5. **Outline not border** — DDS inputs use `outline` for their visible border, not `border`.
6. **Mobile responsive** — Every wireframe must include the `@media (max-width: 768px)` block and the CSS-only sidebar toggle from the base template. On mobile: sidebar is hidden behind a hamburger toggle (`<input type="checkbox" id="nav-toggle">` + `<label>` in header), grids stack to single column (including inline `style` grids via `!important`), tables scroll horizontally, header app-name is hidden, and padding is reduced.

## Generation Rules

### 1. Self-Contained Only
Every `.html` file must work by opening it in a browser with zero dependencies. No external CSS frameworks, no JavaScript. The **only** external resource is the Google Fonts link for Open Sans (degrades gracefully if offline).

### 2. Realistic Content
Use realistic placeholder content, not "Lorem ipsum". If the wireframe is for a project list, show 3-4 example projects with real-sounding names.

### 3. Import Hints
Add `data-wf-type` and `data-wf-props` attributes to key elements so DTS.Design can parse them into canvas objects later:

```html
<button class="wf-btn wf-btn-primary"
        data-wf-type="ddx-button"
        data-wf-props='{"variant": "primary", "label": "Save Project"}'>
  Save Project
</button>
```

Standard `data-wf-type` values:
| Value | Maps To |
|-------|---------|
| `ddx-button` | DDX Button component |
| `ddx-input` | DDX Text Input |
| `ddx-textarea` | DDX Textarea |
| `ddx-select` | DDX Select/Dropdown |
| `ddx-table` | DDX Data Table |
| `ddx-card` | DDX Card |
| `ddx-alert` | DDX Alert/Banner |
| `ddx-modal` | DDX Modal Dialog |
| `ddx-tabs` | DDX Tab Group |
| `ddx-badge` | DDX Badge/Status |
| `ddx-sidebar` | DDX Side Navigation |
| `ddx-header` | DDX App Header |
| `frame` | DTS.Design Frame container |
| `text` | Text element |
| `placeholder` | Placeholder/image area |

### 4. No JavaScript
Wireframes are static. No interactivity, no event handlers, no scripts. They represent a single screen state.

### 5. One State Per File
If a screen has multiple states (loading, error, empty, populated), generate a separate `.html` file for each state of interest.

## Composing a Wireframe

### Step 1: Determine Screen Layout

Choose layout from these patterns:

| Pattern | When |
|---------|------|
| Sidebar + Content | List/detail views, navigation-heavy screens |
| Full-width Content | Forms, dashboards, landing pages |
| Content + Right Panel | Canvas with properties panel (DTS.Design itself) |
| Modal over Page | Confirmation dialogs, create/edit forms |

### Step 2: Build the Shell

Start with the standard app shell (header, optional sidebar), adapting navigation to match the feature context.

### Step 3: Fill Content Area

Use the CSS classes from the template to compose the content. Build from the PBI's acceptance criteria — each criterion should be visible in the wireframe.

### Step 4: Add Import Hints

Tag interactive elements with `data-wf-type` and `data-wf-props` for future DTS.Design import.

### Step 5: Add Metadata Bar

The `.wf-meta` bar at the top shows PBI reference and screen name — this stays outside the "device frame" and serves as documentation.

## Journey Overview Wireframe

When generating a journey overview (multi-step), produce a single `.html` file with a visual flow:

```html
<div class="wf-content" style="padding: 32px;">
  <h1 class="wf-page-title">User Journey: {{JOURNEY_NAME}}</h1>
  <p style="color: var(--ddx-gray-6); margin-bottom: 24px;">Persona: {{PERSONA}} — {{GOAL}}</p>

  <div style="display: flex; flex-wrap: wrap; gap: 16px; align-items: flex-start;">
    <!-- Repeat for each step -->
    <div class="wf-card" style="width: 220px; text-align: center;">
      <div style="font-size: 28px; color: var(--ddx-primary-green-2); font-weight: 700;">1</div>
      <div style="font-weight: 600; margin: 8px 0;">{{STEP_NAME}}</div>
      <div style="font-size: 12px; color: var(--ddx-gray-5);">{{STEP_DESCRIPTION}}</div>
      <div style="margin-top: 8px;">
        <a href="mockup_{{journey}}_step_1.html" style="font-size: 12px; color: var(--ddx-primary-green-2);">View mockup →</a>
      </div>
    </div>
    <!-- Arrow between steps -->
    <div style="display: flex; align-items: center; font-size: 24px; color: var(--ddx-gray-1);">→</div>
    <!-- Next step... -->
  </div>
</div>
```

## Examples

### Simple Form Screen

```html
<div class="wf-header">
  <div style="display: flex; align-items: center;">
    <label for="nav-toggle" class="nav-toggle-label">&#9776;</label>
    <span class="logo">Deloitte</span>
    <span class="app-name">DTS.Design</span>
  </div>
  <div class="nav"><a>Projects</a> <a>Components</a></div>
  <div class="avatar">PC</div>
</div>
<div class="wf-body">
  <div class="wf-sidebar">
    <div class="nav-item active">New Project</div>
    <div class="nav-item">My Projects</div>
    <div class="nav-item">Shared</div>
  </div>
  <div style="flex: 1;">
    <div class="wf-page-header">
      <div class="wf-breadcrumb">Projects <span class="separator">/</span> <a>New Project</a></div>
      <h1 class="wf-page-title" style="margin-bottom: 0;">Create New Project</h1>
    </div>
    <div class="wf-content">
      <div class="wf-card" data-wf-type="frame">
        <label class="wf-label">Project Name</label>
        <input class="wf-input" data-wf-type="ddx-input" placeholder="Enter project name" />
        <label class="wf-label">Description</label>
        <textarea class="wf-textarea" data-wf-type="ddx-textarea" placeholder="What is this project for?"></textarea>
        <div style="margin-top: 16px;">
          <button class="wf-btn wf-btn-primary" data-wf-type="ddx-button">Create Project</button>
          <button class="wf-btn wf-btn-secondary" data-wf-type="ddx-button">Cancel</button>
        </div>
      </div>
    </div>
  </div>
</div>
```

### Data Table Screen

```html
<!-- This example shows the content area only — wrap in wf-header + wf-body + wf-sidebar shell -->
<div class="wf-page-header">
  <div class="wf-breadcrumb">Projects <span class="separator">/</span> <a>My Projects</a></div>
  <div style="display: flex; justify-content: space-between; align-items: center;">
    <h1 class="wf-page-title" style="margin-bottom: 0;">My Projects</h1>
    <button class="wf-btn wf-btn-primary" data-wf-type="ddx-button">+ New Project</button>
  </div>
</div>
<div class="wf-content">
  <div class="wf-card" data-wf-type="ddx-table">
    <table class="wf-table">
      <thead>
        <tr><th>Name</th><th>Screens</th><th>Last Modified</th><th>Status</th><th></th></tr>
      </thead>
      <tbody>
        <tr>
          <td>Onboarding Flow</td><td>8</td><td>Mar 10, 2026</td>
          <td><span class="wf-badge wf-badge-rounded wf-badge-success">Active</span></td>
          <td><button class="wf-btn wf-btn-secondary" style="padding: 4px 12px; font-size: 12px;">Open</button></td>
        </tr>
        <tr>
          <td>Dashboard Redesign</td><td>5</td><td>Mar 8, 2026</td>
          <td><span class="wf-badge wf-badge-rounded wf-badge-warning">Draft</span></td>
          <td><button class="wf-btn wf-btn-secondary" style="padding: 4px 12px; font-size: 12px;">Open</button></td>
        </tr>
      </tbody>
    </table>
  </div>
</div>
```

## Speed Comparison

| Aspect | Excalidraw (old) | HTML Wireframes (new) |
|--------|------------------|----------------------|
| Token cost per wireframe | 4k-22k (JSON) | 500-1500 (HTML) |
| Subagent delegation | Required | Not needed |
| Round-trips | 2+ per mockup | 1 (direct create) |
| Viewable standalone | VS Code extension only | Any browser |
| Shareable | Export PNG first | Send .html file |
| DTS.Design import | Not planned | `data-wf-type` hints |

## Quality Checklist

Before delivering a wireframe:

- [ ] Opens in browser with no errors
- [ ] Content is realistic (not lorem ipsum)
- [ ] PBI reference shown in meta bar
- [ ] Interactive elements have `data-wf-type` attributes
- [ ] Layout matches the described screen purpose
- [ ] Single state per file (no JavaScript state switching)
- [ ] Font is Open Sans (Google Fonts link present in `<head>`)
- [ ] All corners are square — zero `border-radius` on buttons, inputs, modals, cards
- [ ] Primary action colour is `#26890d` (DDX green), never blue
- [ ] Header and sidebar are black (`#000`), not dark-gray or navy
- [ ] Page header uses dark `#27282a` banner with white text and breadcrumbs
- [ ] Inputs use `outline` (not `border`) with 0.8px `#d0d0ce`
