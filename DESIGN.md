---
name: TradePilot
description: A calm, evidence-led trading workstation for account health, risk and action.
colors:
  canvas: "#061012"
  surface-base: "#0a1719"
  surface-raised: "#102124"
  surface-selected: "#163035"
  text-primary: "#e7f1ef"
  text-secondary: "#b4c8c4"
  text-muted: "#829b97"
  accent: "#79cfc3"
  positive: "#54c8a7"
  negative: "#e58a9c"
  warning: "#d8b56d"
  information: "#8fc7d8"
rounded:
  sm: "4px"
  md: "8px"
  lg: "12px"
  pill: "999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
  2xl: "32px"
  3xl: "48px"
typography:
  title:
    fontFamily: "Roboto, Helvetica Neue, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 650
    lineHeight: 1.2
  body:
    fontFamily: "Roboto, Helvetica Neue, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "Roboto, Helvetica Neue, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 650
    lineHeight: 1.3
components:
  surface:
    backgroundColor: "{colors.surface-base}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.lg}"
  nav-selected:
    backgroundColor: "{colors.surface-selected}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.md}"
---

# Design System: TradePilot

## Overview

**Creative North Star: "The Night Watch"**

TradePilot is a restrained, high-signal workstation for monitoring live account and market state. Its visual authority comes from composure: a quiet dark-teal canvas, precise tonal layering, explicit freshness, legible risk language and financial numerals that remain stable while data changes.

The system is refined rather than replaced. Brand appears through disciplined teal accents and surface temperature, not glow or novelty. Hierarchy answers account health and required attention before secondary detail.

**Key Characteristics:** calm, evidence-led, compact when healthy, explicit when degraded, structurally responsive.

## Colors

The palette uses cool dark-teal surfaces with a single muted cyan accent and deliberately softened semantic states.

- **Night Canvas** (`#061012`): application background.
- **Work Surface** (`#0a1719`): default shell and content surface.
- **Raised Surface** (`#102124`): controls and genuinely raised regions.
- **Selected Surface** (`#163035`): active navigation and selected controls.
- **Primary Text** (`#e7f1ef`): headings and essential values.
- **Secondary Text** (`#b4c8c4`): explanatory copy.
- **Muted Text** (`#829b97`): timestamps and quiet metadata; not for essential instructions.
- **Pilot Teal** (`#79cfc3`): primary actions, current selection and focus.
- **Positive / Negative / Warning / Information**: state only, always paired with text or iconography.

**The Evidence Rule.** Colour may reinforce a state but never carry its meaning alone.

## Typography

**Display and Body Font:** Roboto with Helvetica Neue and system sans fallbacks.

**Data Font:** the same family with `font-variant-numeric: tabular-nums`; identifiers may use the platform monospace stack selectively.

Roboto remains for this milestone because no approved, bundled IBM Plex WOFF2 assets exist in the repository and runtime font-CDN loading is prohibited. IBM Plex can be reconsidered with licensed, performance-tested local subsets.

- **Page title:** 24px, weight 650, 1.2 line-height.
- **Section title:** 16–18px, weight 650.
- **Body:** 14–16px, 1.5 line-height.
- **Functional label:** minimum 12px, weight 650; uppercase only for short data labels.
- **Primary financial value:** 24–32px depending on viewport, tabular numerals.

## Layout

Desktop uses a fixed side navigation and a content region capped at 1280px. Page gutters follow the spacing scale and reduce structurally at 768px. The Overview leads with a compact page header, primary account values, a risk strip, actionable alerts, market context and operational tabs.

Mobile keeps Overview, Markets, Trade and More persistent. Equity, P&L and the highest-severity risk remain immediate; secondary metrics disclose progressively. Market context becomes compact rather than disappearing. Pages must not introduce horizontal document scrolling.

## Elevation & Depth

Depth is tonal. Base, raised, overlay and selected surfaces use small luminance shifts and borders. Shadows are reserved for overlays that actually float above the document; decorative blur and halos are not part of the system.

## Shapes

The radius vocabulary is 4px for compact details, 8px for controls, 12px for primary surfaces and pill only for badges/chips. Dividers, spacing and typography should replace unnecessary nested cards.

## Components

### Navigation

Navigation is grouped by user intent. Active destinations use the selected surface and a slim inset accent. Locked destinations retain their label and use a concise lock/Pro marker. Healthy connection state is quiet; degraded state gains language and semantic emphasis.

### Account Metrics

Equity and unrealised P&L form the primary account block. Supporting risk metrics are smaller and aligned in a strip. Values use tabular numerals and include their quote currency where applicable.

### Freshness and Status

Freshness is explicit and readable. Stale data remains fully opaque; a labelled status identifies affected data. Risk and connection states combine icon, text and colour.

### Buttons and Focus

Controls follow Angular Material affordances. Focus uses a visible 2px Pilot Teal outline with offset. Motion is 150–250ms, communicates state and is disabled or reduced under `prefers-reduced-motion`.

## Do's and Don'ts

### Do:

- **Do** lead with account health, risk and required attention.
- **Do** keep healthy system state compact.
- **Do** use semantic tokens and tabular numerals on changed surfaces.
- **Do** preserve loading, partial, empty, error and stale data readability.

### Don't:

- **Don't** use neon, glowing borders, decorative gradients or glass as a visual shortcut.
- **Don't** fade an entire surface to communicate stale data.
- **Don't** create same-weight card grids that hide priority.
- **Don't** present AI interpretation without provenance and freshness.
- **Don't** alter business logic or execution semantics through visual work.
