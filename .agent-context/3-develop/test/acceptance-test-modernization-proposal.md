# Acceptance Test Modernization Proposal

**Date**: 2026-04-12
**Status**: Draft — Awaiting Review

---

## Executive Summary

The current acceptance testing pipeline uses a 3-agent Gherkin/SpecFlow workflow (Designer → Implementer → Healer). Industry tooling has evolved significantly in the past 6 months — Playwright now ships built-in AI agents, and the community has shifted from Gherkin feature files to plain-English markdown specs fed directly to AI test generators.

This proposal outlines the case for dropping the Gherkin/SpecFlow translation layer and moving to a streamlined 2-agent pipeline that generates Playwright TypeScript tests directly from structured markdown specifications.

---

## Part 1: Why Gherkin Is Becoming Redundant

### The Original Purpose of Gherkin/BDD

Gherkin was created to solve a specific problem: **bridging the communication gap between business stakeholders and developers**. The Given/When/Then format was designed to be readable by non-technical people and executable by machines.

### Why That Rationale No Longer Holds

| Original Justification | Current Reality |
|------------------------|-----------------|
| Business stakeholders read feature files | In practice, they rarely do. PMs and testers write acceptance criteria in tickets, not `.feature` files |
| Gherkin is the "single source of truth" | The ticket/story is the real source of truth. Feature files are a translation |
| Step definitions enable reuse | AI agents compose test code directly — reuse happens at the page object level |
| Human-readable test reports | Playwright `test.step()` produces equally readable reports without Gherkin |
| Forces structured thinking about behaviour | Structured markdown specs achieve the same outcome with less ceremony |

### The Translation Tax

The Gherkin pipeline introduces a **translation layer** at every stage:

```
Business intent (ticket/story)
  → Gherkin feature file (translation #1)
    → Step definitions with bindings (translation #2)
      → Page object method calls (translation #3)
        → Playwright API calls (actual test)
```

Each translation is a potential source of drift, bugs, and maintenance cost. With AI agents capable of understanding natural language directly, translations #1 and #2 become pure overhead.

### Industry Evidence

#### 1. Playwright's Own Direction

Playwright (Microsoft) now ships **built-in AI agents** for test authoring:

- **Planner Agent** — explores a URL, generates test plans
- **Generator Agent** — takes a plan, generates `.spec.ts` files directly
- **Healer Agent** — fixes failing tests automatically

These agents work with plain TypeScript `.spec.ts` files. Gherkin is not part of the workflow. Install with: `npx playwright init-agents --loop=vscode`

**Source**: [Playwright docs — Test Agents](https://playwright.dev/docs/test-agents)

#### 2. "From Acceptance Criteria to Playwright Tests with MCP" (Jan 2026)

Rich (Software Architect, UK fintech) demonstrated a complete workflow:
- Plain-English markdown files in a `prompts/` folder
- AI agent reads the markdown, uses Playwright MCP to explore the live app
- Generates and runs Playwright TypeScript tests directly
- No Gherkin, no step definitions, no SpecFlow

Key quote: *"We already have plain-English specifications. With minor adjustments, those acceptance criteria can become explicit UX interaction specs."*

**Source**: https://dev.to/yerac/from-acceptance-criteria-to-playwright-tests-with-mcp-4ka6

#### 3. "Letting Playwright MCP Explore your site and Write your Tests" (Jun 2025)

Debbie O'Brien (Playwright team, Microsoft) published the canonical example:
- A `.prompt.md` file with structured test intent
- Playwright MCP navigates the app, discovers elements, generates tests
- Tests are committed directly — no intermediate Gherkin layer
- 264 reactions — the most popular Playwright testing article of 2025

**Source**: https://dev.to/debs_obrien/letting-playwright-mcp-explore-your-site-and-write-your-tests-mf1

#### 4. "Why AI Can't Write Good Playwright Tests (And How To Fix It)" (Dec 2025)

Johnny's deep-dive on progressive DOM disclosure for AI test authoring:
- Introduces Verdex MCP server for structural exploration
- Entire approach is AI → Playwright directly
- Gherkin is never mentioned in a 31-minute read on AI-assisted test generation
- Shows that the selector quality problem is solved at the tool level, not the specification format level

**Source**: https://dev.to/johnonline35/why-ai-cant-write-good-playwright-tests-and-how-to-fix-it-knn

#### 5. "Fixing Failing Tests Automatically with Playwright's New Healer Agent" (Nov 2025)

Debbie O'Brien demonstrates the Healer Agent:
- 9 failing tests fixed automatically while she was away
- Agent reads test logs, inspects DOM snapshots, updates selectors
- Marks unfixable tests with `test.fixme` and explains why
- Result: 104 passing, 3 intentionally skipped with clear explanations

**Source**: https://dev.to/debs_obrien/fixing-failing-tests-automatically-with-playwrights-new-healer-agent-13ck

#### 6. ThoughtWorks Technology Radar Vol 33 (Nov 2025)

Highlights "spec-driven development" with AI but explicitly warns against *"reverting to traditional software-engineering antipatterns — most notably, a bias toward heavy up-front specification and big-bang releases."*

BDD/Gherkin as a formal specification layer falls squarely into the "heavy up-front specification" category they warn about.

**Source**: https://www.thoughtworks.com/radar

#### 7. Cucumber Project Status

- SmartBear dropped Cucumber → returned to community ownership (Dec 2024)
- No new BDD methodology content published since 2022
- Their "2025 year in review" (Apr 2026) is about project survival, not ecosystem growth

**Source**: https://cucumber.io/blog/

### What Gherkin Still Does Well

To be fair, Gherkin has legitimate remaining use cases:

- **Regulated industries** where auditable, business-signed specifications are legally required
- **Large teams with dedicated QA** who don't code but need to read/write test specs
- **Existing test suites** where the investment has already been made and tests are stable

None of these apply to this project (solo developer, personal trading bot, greenfield tests).

---

## Part 2: Current Pipeline Analysis

### Current 3-Agent Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  CURRENT: 3-Agent Gherkin Pipeline                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Input: Journey YAML file                                           │
│                                                                     │
│  Agent 1: Designer (4-Test: 1 Designer)                             │
│  ├── Navigates live app with playwright-cli                         │
│  ├── Discovers locators, takes snapshots                            │
│  ├── Creates discovery notes (.md)                                  │
│  └── Generates Gherkin .feature files                               │
│       ↓                                                             │
│  Agent 2: Implementer (4-Test: 2 Implementer)                      │
│  ├── Reads discovery notes + .feature files                         │
│  ├── Creates Page Objects (C#)                                      │
│  ├── Creates Component classes (C#)                                 │
│  ├── Creates SpecFlow Step Definitions (C#)                         │
│  └── Builds and verifies compilation                                │
│       ↓                                                             │
│  Agent 3: Healer (4-Test: 3 Healer)                                │
│  ├── Executes tests with dotnet test                                │
│  ├── Diagnoses failures (locator, timing, assertion, app error)     │
│  ├── Fixes issues using playwright-cli for live verification        │
│  └── Escalates after 3 failed attempts                              │
│                                                                     │
│  Output: C# SpecFlow tests (.feature + .cs step defs + page objs)  │
│                                                                     │
│  Artifacts per feature:                                             │
│  - discovery notes (.md)                                            │
│  - .feature file (Gherkin)                                          │
│  - Page Object classes (.cs)                                        │
│  - Component classes (.cs)                                          │
│  - Step Definition classes (.cs)                                    │
│  - Changes tracking file (.md)                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### Problems with Current Approach

| Problem | Impact |
|---------|--------|
| **Language mismatch** | C# tests for an Angular (TypeScript) frontend — two languages in the test chain |
| **Translation overhead** | Gherkin → Step Definitions is pure boilerplate that AI makes unnecessary |
| **3-agent handoff** | Each agent transition requires session context files, increasing failure points |
| **SpecFlow dependency** | Additional NuGet package, binding registration, attribute ceremony |
| **Rigid Gherkin syntax** | `[Scope(Tag)]` restrictions, step reuse constraints, regex bindings |
| **Discovery notes duplication** | Locator info captured in discovery notes, then manually re-encoded in Page Objects |
| **Heavy artifact count** | 6+ files per feature vs 2-3 with direct generation |

### What's Working Well (Keep These)

| Strength | Why It Works |
|----------|-------------|
| **Playwright CLI for discovery** | Live app exploration produces verified locators — this is ahead of the curve |
| **Page Object pattern** | Encapsulation of page structure is valuable regardless of test format |
| **Healer agent concept** | Self-healing with escalation after 3 attempts is a solid pattern |
| **Discovery notes** | Documenting locators and page structure before test generation improves quality |
| **Hard stop rules** | Preventing the agent from improvising when tools fail is essential |
| **Journey YAML input** | Structured input format for test specifications |

---

## Part 3: Proposed Modernized Pipeline

### New 2-Agent Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  PROPOSED: 2-Agent Direct Pipeline                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Input: Journey YAML or structured markdown spec                    │
│                                                                     │
│  Agent 1: Designer (4-Test: 1 Designer) — REVISED                   │
│  ├── Navigates live app with Playwright MCP / playwright-cli        │
│  ├── Discovers locators, takes snapshots                            │
│  ├── Creates discovery notes (.md) — same as today                  │
│  └── Generates structured test specs (.md) — NOT Gherkin            │
│       ↓                                                             │
│  Agent 2: Generator + Healer (4-Test: 2 Generator) — MERGED        │
│  ├── Reads discovery notes + test specs                             │
│  ├── Creates Page Objects (TypeScript)                              │
│  ├── Generates Playwright .spec.ts files directly                   │
│  ├── Uses test.step() for structured, readable test output          │
│  ├── Executes tests with npx playwright test                        │
│  ├── Diagnoses and heals failures (same escalation rules)           │
│  └── Iterates until passing or escalates                            │
│                                                                     │
│  Output: Playwright TypeScript tests (.spec.ts + page objects)      │
│                                                                     │
│  Artifacts per feature:                                             │
│  - discovery notes (.md)                                            │
│  - test spec (.md)                                                  │
│  - Page Object classes (.ts)                                        │
│  - Playwright test files (.spec.ts)                                 │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Changes

| Aspect | Current | Proposed |
|--------|---------|----------|
| **# of agents** | 3 (Designer → Implementer → Healer) | 2 (Designer → Generator/Healer) |
| **Spec format** | Gherkin `.feature` files | Structured markdown test specs |
| **Test language** | C# with SpecFlow | TypeScript with Playwright Test |
| **Page objects** | C# classes | TypeScript classes |
| **Test runner** | `dotnet test` (SpecFlow + NUnit) | `npx playwright test` |
| **Step definitions** | SpecFlow `[Given]`/`[When]`/`[Then]` bindings | Direct Playwright API calls in `.spec.ts` |
| **Test structure** | Gherkin scenarios → step defs → page objects | `test.step()` blocks → page objects |
| **Reporting** | SpecFlow HTML reports | Playwright HTML reporter + Trace Viewer |
| **Handoff files** | Discovery notes → Gherkin → Step defs → Page objects | Discovery notes → Specs → Tests + Page objects |
| **Browser discovery** | playwright-cli (custom) | Playwright MCP or playwright-cli |
| **Self-healing** | Separate Healer agent | Built into Generator agent |

### Structured Test Spec Format (Replaces Gherkin)

Instead of:

```gherkin
Feature: Risk Management Dashboard

Scenario: User views position risk summary
  Given I am logged in as a trader
  When I navigate to the risk management dashboard
  Then I should see the position risk summary panel
  And the total exposure should be displayed
  And the margin utilisation percentage should be visible
```

We write:

```markdown
# Risk Management Dashboard

## Before Each Test
- Navigate to the application
- Log in as a trader (use test credentials from environment)
- Navigate to /risk-management

## 1. Position Risk Summary
- The position risk summary panel is visible
- Total exposure value is displayed and not empty
- Margin utilisation percentage is visible and shows a valid percentage (0-100%)
- The panel updates when positions change

## 2. Risk Limit Alerts
- When total exposure exceeds 80% of the configured limit
- A warning alert is displayed in the risk panel
- The alert text includes the current exposure amount
- The alert has a "warning" severity indicator
```

**Advantages of this format:**
- Same readability as Gherkin (arguably more natural)
- No syntax constraints (no regex step matching needed)
- AI can interpret intent directly without parsing Gherkin grammar
- Can include notes, context, edge cases inline
- Git-diffable, PR-reviewable, stakeholder-readable
- Maps 1:1 to `test.describe()` + `test.step()` blocks

### Generated Test Output Example

The agent would generate:

```typescript
// tests/e2e/risk-management/risk-dashboard.spec.ts
import { test, expect } from '@playwright/test';
import { RiskDashboardPage } from '../pages/risk-dashboard.page';

test.describe('Risk Management Dashboard', () => {
  let riskPage: RiskDashboardPage;

  test.beforeEach(async ({ page }) => {
    riskPage = new RiskDashboardPage(page);
    await riskPage.login();
    await riskPage.navigateTo();
  });

  test('Position Risk Summary', async ({ page }) => {
    await test.step('Risk summary panel is visible', async () => {
      await expect(riskPage.summaryPanel).toBeVisible();
    });

    await test.step('Total exposure is displayed', async () => {
      const exposure = await riskPage.getTotalExposure();
      expect(exposure).not.toBe('');
    });

    await test.step('Margin utilisation shows valid percentage', async () => {
      const margin = await riskPage.getMarginUtilisation();
      const value = parseFloat(margin.replace('%', ''));
      expect(value).toBeGreaterThanOrEqual(0);
      expect(value).toBeLessThanOrEqual(100);
    });
  });
});
```

```typescript
// tests/e2e/pages/risk-dashboard.page.ts
import { Page, Locator } from '@playwright/test';

export class RiskDashboardPage {
  readonly page: Page;
  readonly summaryPanel: Locator;
  readonly totalExposure: Locator;
  readonly marginUtilisation: Locator;

  constructor(page: Page) {
    this.page = page;
    this.summaryPanel = page.getByTestId('risk-summary-panel');
    this.totalExposure = page.getByTestId('total-exposure');
    this.marginUtilisation = page.getByTestId('margin-utilisation');
  }

  async login() { /* ... */ }
  async navigateTo() { await this.page.goto('/risk-management'); }
  async getTotalExposure() { return this.totalExposure.textContent(); }
  async getMarginUtilisation() { return this.marginUtilisation.textContent(); }
}
```

**Note**: `test.step()` produces structured, readable test reports identical to Gherkin scenario output — but without the translation overhead.

---

## Part 4: Migration Strategy

### Phase 1: Set Up Playwright Test Infrastructure (TypeScript)

- [ ] Add `playwright.config.ts` to `frontend/trading-ui/` (or root-level `tests/e2e/`)
- [ ] Configure Playwright Test runner alongside existing Angular test setup
- [ ] Create `tests/e2e/pages/` directory for TypeScript page objects
- [ ] Create `tests/e2e/specs/` directory for `.spec.ts` test files
- [ ] Create `prompts/` or `test-specs/` directory for structured markdown specs
- [ ] Add Playwright MCP to `.vscode/mcp.json` if not already present

### Phase 2: Revise Agent Definitions

- [ ] Revise `4-test-1-acceptance-test-designer.agent.md` — change output from Gherkin to structured markdown
- [ ] Create `4-test-2-acceptance-test-generator.agent.md` — merged Implementer + Healer that generates TypeScript
- [ ] Archive old `4-test-2-acceptance-test-implementer.agent.md` and `4-test-3-acceptance-test-healer.agent.md`
- [ ] Update `.github/instructions/acceptance-testing.instructions.md` for new structure
- [ ] Update or replace `.github/instructions/gherkin.instructions.md` with `test-specs.instructions.md`

### Phase 3: Write First Tests

- [ ] Pick one feature (e.g., Risk Management Dashboard — since the UI plan is in progress)
- [ ] Run the revised Designer agent to produce discovery notes + markdown spec
- [ ] Run the Generator agent to produce `.spec.ts` + page objects
- [ ] Validate the full cycle works end-to-end

### Phase 4: Evaluate and Iterate

- [ ] Compare effort/quality with the old Gherkin pipeline
- [ ] Adjust agent instructions based on what works
- [ ] Document patterns in instruction files

---

## Part 5: What We Keep, What We Change, What We Drop

| Element | Decision | Rationale |
|---------|----------|-----------|
| Playwright CLI for discovery | **Keep** | Verified locator discovery is essential |
| Discovery notes (.md) | **Keep** | Documenting page structure before generation improves quality |
| Journey YAML input | **Keep** | Structured input for test design |
| Page Object pattern | **Keep** (change to TypeScript) | Encapsulation is valuable regardless of language |
| Hard stop / escalation rules | **Keep** | Preventing agent hallucination is critical |
| Changes tracking files | **Keep** | Audit trail for what changed |
| Gherkin `.feature` files | **Drop** | Replaced by structured markdown specs |
| SpecFlow step definitions | **Drop** | Pure translation overhead with AI agents |
| SpecFlow NuGet dependency | **Drop** | No longer needed |
| C# test project | **Drop** | TypeScript aligns with Angular frontend |
| Separate Healer agent | **Merge** into Generator | Same agent generates and heals — less handoff friction |
| `[Binding]` / `[Given]` / `[When]` / `[Then]` | **Drop** | Replaced by `test.step()` |
| `[Scope(Tag)]` restrictions | **Drop** | Non-issue with direct test files |

---

## Part 6: Risk Assessment

| Risk | Mitigation |
|------|------------|
| TypeScript page objects less familiar than C# | Page Object pattern is identical; only syntax differs |
| Playwright MCP locator quality is inconsistent | Keep playwright-cli as fallback; use Verdex MCP for complex pages |
| AI generates brittle selectors (`.nth()`) | Agent instructions enforce role-first, container-scoped selectors (same as current) |
| Loss of Gherkin's structured scenario format | `test.step()` + structured markdown specs provide equivalent structure |
| Merging Healer into Generator makes one large agent | Clear phase separation within the agent definition mitigates this |
| Existing SpecFlow investment lost | No existing acceptance tests in this project — greenfield, zero sunk cost |

---

## Decision Required

**Option A: Proceed with modernization** — Create revised agent definitions, set up TypeScript Playwright infrastructure, and test with the Risk Management Dashboard feature.

**Option B: Incremental approach** — Keep existing agent structure but swap Gherkin output for markdown specs as a first step. Migrate to TypeScript tests later.

**Option C: Keep current approach** — Continue with Gherkin/SpecFlow. The pipeline works; Gherkin isn't broken, it's just more ceremony than needed.

**Recommendation**: Option A. This is a greenfield project with no existing acceptance tests. There's zero migration cost and the simplified pipeline will pay off immediately.
