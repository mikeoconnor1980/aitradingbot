---
description: Playwright locator discovery standards for acceptance tests
---

# Playwright Locator Discovery Standards

Guidelines for discovering and documenting locators during acceptance test design.

---

<!-- DOT-CUSTOM-START:playwright-locators -->

<!-- DOT-CUSTOM-END:playwright-locators -->

## Playwright MCP Tool Reference

When using Playwright MCP during discovery:

| Action | Tool | Example |
|--------|------|---------|
| Navigate to URL | `mcp_microsoft_pla_browser_navigate` | `url: "https://app.example.com/obligations"` |
| Take snapshot | `mcp_microsoft_pla_browser_snapshot` | Returns accessibility tree with element refs |
| Click element | `mcp_microsoft_pla_browser_click` | `element: "Save button", ref: "A123"` |
| Type text | `mcp_microsoft_pla_browser_type` | `element: "Name input", ref: "B456", text: "Test"` |
| Count elements | `mcp_microsoft_pla_browser_evaluate` | `function: "document.querySelectorAll('h2').length"` |

---

## Locator Priority Order

Use locators in this priority order (most stable first):

| Priority | Type | Example |
|----------|------|---------|
| 1️⃣ | Locator Extensions | `parent.ClickButtonAsync("Submit")` |
| 2️⃣ | Role-based | `Page.GetByRole(AriaRole.Button, new() { Name = "Submit" })` |
| 3️⃣ | Test ID | `Page.GetByTestId("submit-button")` |
| 4️⃣ | Label/Text | `Page.GetByLabel("Email")` |
| 5️⃣ | CSS (last resort) | `Page.Locator(".ag-row")` |

---

## Locator Extensions Reference

The test project has extension methods in `Extensions/Locator/` for common DDS/DDX patterns:

### Grid Operations
- `WaitForGridToFinishLoadingAsync()` - Wait for AG Grid loading overlay to disappear
- `WaitForGridToHaveDataAsync()` - Wait for at least one row
- `GetGridRowsAsync<T>()` - Extract grid data as ViewModels
- `ClickGridRowAsync(viewModel)` - Click a specific row
- `ToggleGridRowSelection(viewModel)` - Toggle row checkbox
- `TriggerGridRowActionAsync(viewModel, GridAction)` - Trigger row action (Edit, Delete, etc.)
- `IsGridRowSelectedAsync(viewModel)` - Check if row is selected
- `ToggleGridSelectAllCheckbox()` - Toggle the header checkbox

### UI Components
- `SelectTabAsync("TabName")` - Select a tab
- `IsTabActiveAsync("TabName")` - Check tab state
- `ClickButtonAsync("ButtonText")` - Click button by text
- `SelectDropdownOptionAsync("Option")` - Select dropdown value
- `ToggleSwitchByLabelAsync("Label")` - Toggle a switch

**Why document these:** Extensions handle row-level grid operations, but assertions on specific cell contents need additional locators documented during discovery.

---

## DDS/DDX Component Patterns

These are **known behaviors** of the DDX/DDS component library that require specific handling. Failure to follow these patterns will cause test failures.

### Checkbox Components

DDS checkboxes render an overlay `<span class="dds-custom-control__icon">` that intercepts pointer events.

| ❌ Fails | ✅ Works |
|----------|----------|
| `checkbox.ClickAsync()` | `checkbox.CheckAsync(new() { Force = true })` |
| `checkbox.UncheckAsync()` | `checkbox.UncheckAsync(new() { Force = true })` |

**Why**: The decorative span covers the actual input element. `Force = true` bypasses the actionability check.

### Filter Bar (ddx-filter-bar)

The DDX filter bar has async rendering and specific button behaviors:

| Behavior | Details |
|----------|----------|
| **Async Rendering** | Filter bar only renders after `buildDynamicFilters()` completes. Wait for the Filter button to be visible before clicking. |
| **Clear All Button** | Applies filters AND closes the panel. Do not wait for panel to remain open after clicking. |
| **Apply Filters Button** | Closes the panel. |
| **Cancel Button** | Closes the panel without applying. |

```csharp
// Wait for filter button before clicking
var filterButton = _page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(@"^Filter.*") });
await filterButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });
await filterButton.ClickAsync();
```

### Dropdown/Select Components

DDX dropdowns use CDK overlay which renders the options panel outside the component DOM tree.

| Behavior | Details |
|----------|----------|
| **Overlay Portal** | Options render in `<body>`, not inside the dropdown element. |
| **Use Extensions** | Always use `SelectDropDownLocatorExtensions` methods. |

### Dialog/Modal Panels

| Behavior | Details |
|----------|----------|
| **Role** | Use `GetByRole(AriaRole.Dialog)` to locate the panel container. |
| **Scope** | All elements inside modals/panels MUST be scoped to the dialog locator. |

---

## Default Scoping Patterns

**CRITICAL**: Locators must be scoped to their container to avoid matching duplicate elements elsewhere on the page.

### Required Scoping Rules

| Context | Scope To | Example |
|---------|----------|----------|
| Modal/Dialog content | `GetByRole(AriaRole.Dialog)` | `PanelDialog.GetByRole(AriaRole.Button, new() { Name = "Save" })` |
| Filter panel sections | Dialog + Region | `PanelDialog.GetByRole(AriaRole.Region, new() { Name = "Client" })` |
| Tab panel content | Tab panel locator | `TabPanel.GetByRole(AriaRole.Grid)` |
| Grid cells | Row locator | `Row.Locator(".ag-cell[col-id='name']")` |
| Form fields | Form container | `Form.GetByLabel("Email")` |

### Anti-Pattern: Unscoped Page Locators

```csharp
// ❌ WRONG - searches entire page, may find duplicates
private ILocator SaveButton => _page.GetByRole(AriaRole.Button, new() { Name = "Save" });

// ✅ CORRECT - scoped to the dialog
private ILocator SaveButton => PanelDialog.GetByRole(AriaRole.Button, new() { Name = "Save" });
```

### When to Scope

| Scenario | Scoping Required? |
|----------|-------------------|
| Main page buttons (only one exists) | ❌ No |
| Modal/dialog content | ✅ Yes - scope to dialog |
| Filter panel content | ✅ Yes - scope to dialog |
| Expandable sections | ✅ Yes - scope to region |
| Elements that appear multiple times | ✅ Yes - scope to unique parent |

---

## AG Grid Locator Pitfalls

AG Grid duplicates content in `aria-live` containers for screen readers. This causes text-based locators to match multiple elements.

### Common Duplicated Elements

| Element | Aria Container | Actual Display |
|---------|----------------|----------------|
| Page info | `.ag-aria-description-container` | `.ag-paging-description` |
| Row summary | `.ag-aria-description-container` | `.ag-paging-row-summary-panel` |

### Safe vs Unsafe Locators

| ❌ Avoid (matches aria + display) | ✅ Use Instead (specific class) |
|-----------------------------------|--------------------------------|
| `text=/Page \\d+ of \\d+/` | `.ag-paging-description` |
| `GetByText("1 to 20 of")` | `.ag-paging-row-summary-panel` |
| Generic text patterns in grids | AG Grid CSS classes (`.ag-*`) |

---

## AG Grid CSS Class Reference

| Class | Purpose |
|-------|---------|
| `.ag-paging-description` | "Page X of Y" text |
| `.ag-paging-row-summary-panel` | "1 to 20 of 555" text |
| `.ag-header-cell` | Column headers |
| `.ag-center-cols-container .ag-row` | Data rows (center, scrollable) |
| `.ag-pinned-left-cols-container .ag-row` | Data rows (pinned left - checkboxes) |
| `.ag-overlay-loading-center` | Loading indicator |
| `.ag-overlay-no-rows-center` | Empty state message |
| `.ag-selection-checkbox` | Row selection checkbox |

---

## AG Grid Cell Content Locators

**"Use GridLocatorExtensions" is NOT a complete locator.** Discovery MUST document specific element selectors inside cells:

| Cell Content | Locator Pattern | Notes |
|--------------|-----------------|-------|
| Priority pills | `.ag-cell[col-id='priorityId'] ddx-pill` | Or `app-priority-pill`, `[class*='priority']` |
| Status badges | `.ag-cell[col-id='status'] ddx-badge` | Or component-specific selector |
| Action buttons | `.ag-cell[col-id='actions'] button` | Usually in last column |
| Links | `.ag-cell a` | Scope to specific col-id |
| Checkboxes | `.ag-pinned-left-cols-container .ag-selection-checkbox input` | NOT in center container |

**Key insight:** Extensions handle row-level operations, but assertions on cell contents need specific selectors.

---

## High-Risk Locator Patterns

These patterns commonly return multiple elements - ALWAYS verify uniqueness with a count check:

| Pattern | Risk | Example |
|---------|------|---------|
| `GetByRole(AriaRole.Heading, Level=X)` | Multiple headings at same level | Page title + section titles |
| `GetByRole(AriaRole.Button)` without Name | Multiple buttons on page | Save, Cancel, Close buttons |
| `GetByText("Submit")` | Text appears in multiple contexts | Button label + help text |
| `GetByRole(AriaRole.Row)` | Multiple rows in grid | AG Grid data rows |
| `Locator(".btn")` | Generic CSS class | All styled buttons |

---

## Forbidden Locator Patterns

These patterns are **known to fail** and must NEVER be used in Page Objects:

| ❌ Forbidden Pattern | Why It Fails | ✅ Use Instead |
|---------------------|--------------|----------------|
| `GetByRole(AriaRole.Textbox).Filter(new() { HasNot = ... })` | `Filter` with `HasNot` checks *descendants*, not the element's own attributes | `Page.Locator("textarea")` or `GetByRole(AriaRole.Textbox, new() { Name = "FieldLabel" })` |
| `GetByRole(AriaRole.Heading, new() { Level = 2 })` (unscoped) | Pages typically have multiple H2s | `Page.Locator(".page-header h2")` or scope to parent container |
| `GetByText("...").First` for non-repeating elements | If you need `.First`, the locator isn't unique | Make the locator more specific |
| `GetByPlaceholder("...")` for date fields | Multiple date inputs share the same placeholder | Use `GetByLabel("Field Name")` |
| Raw AG Grid selectors (`.ag-row`, `.ag-cell`) | Complex DOM, virtualization issues | Use `GridLocatorExtensions` methods |
| Raw DDX dropdown selectors | CDK overlay complexity | Use `SelectDropDownLocatorExtensions` methods |

**When you discover a locator matching a forbidden pattern:**
1. ❌ Do NOT mark it as verified
2. ⚠️ Mark it as "Needs Alternative"  
3. 📝 Document the element and suggest a working alternative
4. 🔄 Find a unique locator before proceeding

---

## Behavioral Snapshots During Discovery

**CRITICAL**: A single snapshot is insufficient. State-changing actions require before/after snapshots to understand UI behavior.

### When to Capture Multiple Snapshots

| Action | Snapshots Required |
|--------|--------------------|
| Opening a modal/panel | `page-before.yml` + `modal-open.yml` |
| Clicking a button that may close UI | `before-click.yml` + `after-click.yml` |
| Expanding/collapsing sections | `collapsed.yml` + `expanded.yml` |
| Form submission | `form-filled.yml` + `after-submit.yml` |
| Filter applied | `filters-open.yml` + `grid-after-filter.yml` |

### Discovery Snapshot Workflow

```markdown
1. Navigate to page
2. Capture: `{feature}-initial.yml`
3. Click button to open panel/modal
4. Capture: `{feature}-panel-open.yml`
5. Interact with panel (e.g., select filters)
6. Capture: `{feature}-panel-selections.yml`
7. Click action button (Apply/Clear/Cancel)
8. Capture: `{feature}-after-action.yml`
9. Compare: Did UI change? Did panel close? Did page reload?
```

### What to Document From Behavioral Snapshots

| Observation | Document As |
|-------------|-------------|
| Panel closed after button click | "Button X applies changes AND closes panel" |
| New elements appeared | "After clicking X, the Y element becomes visible" |
| Page URL changed | "Clicking X navigates to /new-path" |
| Loading indicator appeared | "Operation triggers loading state" |
| Section collapsed/expanded | "Section toggles on header click" |

### Behavioral Documentation Template

```markdown
### State Transitions

| Trigger | Before State | After State | Notes |
|---------|--------------|-------------|-------|
| Click "Clear All" | Panel open, filters selected | Panel closed, grid refreshed | Applies AND closes |
| Click section header | Section collapsed | Section expanded | Toggle behavior |
| Click "Apply Filters" | Panel open | Panel closed, grid filtered | Closes automatically |
```

---

## Workflow Tracing Requirements

Discovery MUST follow user workflows to their completion, not just inventory the starting page.

### Required Actions by Element Type

| Action Type | You MUST | Example |
|-------------|----------|---------|
| Row click | Navigate to destination page, discover its elements | Grid row → Details page |
| Add/Create button | Click it, discover the form/modal/page that opens | "Add item" → Modal or new page |
| Checkbox selection | Click it, discover what UI appears (bulk action buttons) | Checkbox → "Update (1)" button |
| Tab click | Click it, discover the tab panel contents | Tab → New grid or form |

### Workflow Tracing Checklist

For EACH clickable element that changes the page:
- [ ] Click the element via Playwright
- [ ] Take snapshot of the resulting UI
- [ ] Document locators for the NEW page/modal/panel
- [ ] Note the URL change (if any)
- [ ] Navigate back and continue discovery

### Common Failures from Skipping This

| What Was Missed | Symptom at Runtime |
|-----------------|-------------------|
| Details page has multiple H1s | `strict mode violation: locator resolved to 3 elements` |
| Form/modal locator unknown | `Expected form visible, but found False` |
| Bulk action button locator unknown | `Button not found` |

---

## Discovery Notes Template

Save discovery notes to `.agent-context/3-develop/test/acceptance-tests/discovery/{feature-name}-discovery.md`.

Use the canonical template at `.github/prompts/discovery-notes-template.md`.

**CRITICAL: Discovery notes contain ONLY observed facts from Playwright exploration.**

Do NOT include in discovery notes:
- ❌ Method signatures or C# code
- ❌ Page Object class designs
- ❌ Implementation planning
- ❌ Step Definition suggestions

These belong in the **implementation agent**, not the design agent.

---

## Discovery Completeness Standards

Discovery notes MUST include locators for **ALL pages and UI states** in the workflow, not just the starting page.

### Required Sections by Workflow Type

| Workflow | Required Discovery Sections |
|----------|----------------------------|
| Grid → Details | Grid page locators + Details page locators |
| Add/Create button | Main page + Form/Modal locators |
| Row selection | Checkbox container + Bulk action buttons |
| Tab navigation | Tab bar + Each tab panel contents |
| Filter/Search | Filter controls + Grid state changes |

### Multi-Page Discovery Template

For each page visited during workflow tracing:

```markdown
## Page: {Page Name}

### Navigation
- **URL Pattern**: `{url-pattern}` (note GUIDs: `/obligations/{id}`)
- **How to reach**: {Previous page} → {Click action}

### Locators Discovered
| Element | Locator | Count | Verified |
|---------|---------|-------|----------|
| ... | ... | 1 | ✅ |
```

### Common Discovery Gaps (Causes Test Failures)

| What Was Missed | Symptom at Runtime |
|-----------------|-------------------|
| Details page has 3 H1s | `strict mode violation: resolved to 3 elements` |
| Form/modal locator not documented | `Expected form visible, but found False` |
| Checkbox in pinned container | `Timeout waiting for .ag-checkbox-input` |
| Bulk action button after selection | `Button not found` |

---

## Locator Uniqueness Verification

**MANDATORY**: During discovery, verify every locator resolves to exactly 1 element.

### Verification Method

Use browser evaluate during discovery:
```javascript
document.querySelectorAll('your-selector').length
```

### Playwright MCP Limitation

The `browser_evaluate` tool cannot serialize complex JavaScript functions.

```javascript
// ❌ Fails - "Passed function is not well-serializable!"
Array.from(document.querySelectorAll('button')).filter(b => b.textContent.includes('Refresh')).length

// ✅ Works - simple selector count
document.querySelectorAll('h1').length
document.querySelectorAll('[role="tab"]').length
document.querySelectorAll('button[aria-label="Refresh"]').length
```

### Count Interpretation

| Count | Action |
|-------|--------|
| 0 | ❌ Locator invalid - element not found |
| 1 | ✅ Safe to use |
| >1 | ⚠️ Make more specific (see below) |

### Making Locators More Specific

When count > 1:

1. **Use CSS class** instead of text: `.ag-paging-description` vs `text=Page`
2. **Add parent scope**: `Locator(".ag-paging-panel").Locator("text=Page")`
3. **Use role + name**: `GetByRole(AriaRole.Button, new() { Name = "Save" })`
4. **Last resort - `.First`**: Only if semantically correct (e.g., first row)

---

## Handling Strict Mode Violations

When Playwright throws `strict mode violation: resolved to N elements`:

### Diagnosis Steps

1. **Identify the duplicate source** - Often accessibility/aria containers
2. **Check AG Grid** - Pagination and summary text is always duplicated
3. **Check DDX/DDS** - Some components render hidden duplicates

### Resolution Strategies

```
// ❌ Fails - matches aria-live + actual display
text=/Page \\d+ of \\d+/

// ✅ Fix 1: Use specific CSS class
.ag-paging-description

// ✅ Fix 2: Scope to parent container
.ag-paging-panel >> text=/Page \d+ of \d+/

// ✅ Fix 3: Use .First (only if semantically correct)
text=/Page \\d+ of \\d+/ >> nth=0
```

---

## Discovery Output Checklist

Before completing discovery notes, verify:

- [ ] Every page visited in workflow has its own section
- [ ] All locators have Count = 1 (verified via browser_evaluate)
- [ ] AG Grid cell content locators documented (not just "use extensions")
- [ ] Checkbox locators use `.ag-pinned-left-cols-container` if pinned
- [ ] Multi-H1 pages have specific heading locators (not generic `h1`)
- [ ] Forms/modals have documented locators
- [ ] URL patterns documented (especially GUID patterns)
- [ ] **Async loading patterns identified** (filter bars, grids, dynamic content)
- [ ] **State transitions documented** (what UI changes occur when buttons clicked)
- [ ] **Multiple behavioral snapshots captured** (before/after for modals, panels, forms)
- [ ] **Scoping requirements noted** (elements that must be scoped to dialog/region/container)
- [ ] **DDS/DDX component quirks documented** (Force=true for checkboxes, panel closing behavior)
