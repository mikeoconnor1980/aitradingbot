---
applyTo: "**/*.feature"
---

# Gherkin & BDD Standards

Best practices for writing Gherkin feature files in DTS acceptance tests.

---

<!-- DOT-CUSTOM-START:gherkin -->

<!-- DOT-CUSTOM-END:gherkin -->

## Gherkin Style: Declarative over Imperative

### ❌ Imperative (How - Avoid)
```gherkin
Scenario: User logs in
    Given I am on the login page
    When I enter "user@example.com" in the email field
    And I click the "Login" button
    Then I should see "Dashboard" in the header
```

### ✅ Declarative (What - Preferred)
```gherkin
Scenario: User logs in successfully
    Given I am a registered user
    When I log in with valid credentials
    Then I should see the Dashboard
```

---

## Feature File Structure

```gherkin
@FeatureTag
Feature: Feature Name

    As a {role},
    I want to {goal},
    So that {benefit}.

Background:
    Given {common preconditions}

@Smoke
Scenario: Critical path scenario
    Given {precondition}
    When {action}
    Then {expected outcome}

Scenario Outline: Parameterized scenario
    Given I have <input>
    When I perform action
    Then I should see <output>

    Examples:
        | input | output |
        | A     | X      |
        | B     | Y      |
```

---

## Scenario Limits

**Keep features focused:**
- **Maximum 3-6 scenarios per feature** - if more are needed, split into separate features
- **1 Smoke test per feature** - the critical happy path only
- **Avoid long `And` chains** - more than 2-3 `And` steps usually means the scenario is doing too much

```gherkin
# ❌ Too many steps - hide complexity
Scenario: Complex workflow
    Given I am authenticated
    And I have permissions
    And I have a client
    And the client has entities
    And the entities have periods
    When I navigate to the page
    And I click the filter
    And I select a date
    And I click apply
    Then I should see results
    And the count should be correct
    And the sort should be right

# ✅ Better - split into focused scenarios
Scenario: Viewing filtered obligations
    Given I am on the Obligations page with test data
    When I apply the date filter
    Then I should see filtered results
```

---

## Tagging Strategy

| Tag | Purpose |
|-----|---------|
| `@{FeatureName}` | Scope steps to feature (required) |
| `@Smoke` | Critical path - run every deployment |
| `@Regression` | Full suite - run nightly |
| `@WIP` | Work in progress - exclude from CI |
| `@Ignore` | Temporarily disabled (add reason comment) |

---

## Step Writing Guidelines

### Given (Preconditions) - Describe State
```gherkin
# ✅ Good
Given I am an authenticated user with access to Work Areas
Given I am on the Dashboard page

# ❌ Bad - describes actions
Given I click the login button
```

### When (Actions) - Single Action
```gherkin
# ✅ Good
When I navigate to the Compliance Setup page
When I click on the "Client" column header

# ❌ Bad - multiple actions
When I click login and enter credentials and submit
```

### Then (Assertions) - Verifiable Outcome
```gherkin
# ✅ Good
Then I should see the Compliance Setup grid
Then the grid should contain at least one row

# ❌ Bad - vague
Then it works correctly
```

---

## Data Tables

```gherkin
Scenario: Grid displays expected columns
    Then the grid should display the following columns
        | Column Name  |
        | Client       |
        | Jurisdiction |
        | Entity       |
```

**Note:** Step definitions must use exact column header names (e.g., `row["Column Name"]`).

---

## Background Steps

```gherkin
Background:
    Given I am an authenticated user

Scenario: View grid
    When I navigate to the page
    Then I should see the grid
```

**Keep backgrounds minimal** - they run before EVERY scenario.

---

## Naming Conventions

| Type | Convention |
|------|------------|
| Feature file | `ComplianceSetup.feature` or `compliance-setup.feature` |
| Scenario | Start with verb, be specific: `Viewing the grid`, `Sorting by Client` |

---

## Anti-Patterns

| ❌ Avoid | ✅ Use Instead |
|----------|----------------|
| `When I click element with id "btn-submit"` | `When I submit the form` |
| `Then the API returns 200` | `Then the operation completes successfully` |
| `Then it should work` | `Then I should see the confirmation message` |
| 15+ steps in one scenario | Break into multiple scenarios |
| Adding assertions beyond story scope | Implement exactly what the story specifies |

---

## ⛔ CRITICAL: Story Fidelity Rule

**Scenarios MUST match the story acceptance criteria - no more, no less.**

### The Rule

> Discovery notes show what CAN be verified. The story shows what SHOULD be verified.
> These are NOT the same thing. Always default to the story.

### Example: Over-Engineering (Don't Do This)

**Story says:**
```markdown
- WHEN I click the "Add Item" button
- THEN I should be navigated to the Add Obligation form
```

**Discovery notes show:** Form has 10 required fields (Client, Project, Work area, etc.)

**❌ Wrong - Agent embellishes:**
```gherkin
Then I should be navigated to the Add Obligation form
And I should see the required form fields  ← NOT IN STORY
```

**✅ Correct - Agent follows story:**
```gherkin
Then I should be navigated to the Add Obligation form
```

### When Coverage Seems Incomplete

If you believe the story is missing important coverage:

1. **DO NOT** silently add extra assertions
2. **DO** use `ask_questions` to propose additions explicitly
3. **Let the user decide** whether to expand scope

**Example question:**
> "The story only verifies navigation to the Add form, not the form fields themselves.
> Should I add a scenario to verify required fields are displayed, or keep it simple?"

### Why This Matters

| Problem | Consequence |
|---------|-------------|
| Extra assertions break when UI changes | Tests fail for reasons unrelated to the story |
| Tests become coupled to implementation details | Harder to maintain, more brittle |
| Scope creep compounds across features | Test suite becomes slow and flaky |

---

## Step Text Must Match Exactly

The step `When I click "Save"` is NOT the same as `When I click the "Save" button`:

```gherkin
When I click "Save"              # Pattern: I click "(.*)"
When I click the "Save" button   # Pattern: I click the "(.*)" button
```

These require **separate** step definitions.

---

## Pre-Generation Gate: Discovery Notes Validation

Before generating ANY feature files, validate the discovery notes. Do NOT proceed if any of these checks fail.

| # | Check | How to Verify | Action if Failed |
|---|-------|---------------|------------------|
| 1 | **Gherkin standards file read** | Confirm you read gherkin.instructions.md | STOP - Read the file first |
| 2 | **Every locator table has a Count column** | Scan all locator tables in discovery notes | STOP - Re-do discovery with count checks |
| 3 | **No locators have Count > 1** | Check Count column for any value > 1 | STOP - Find alternative locators |
| 4 | **No forbidden patterns used** | Check for `Filter(HasNot)`, unscoped `Level=2` headings, etc. | STOP - Replace with working patterns |
| 5 | **All high-risk locators explicitly verified** | H2 headings, generic buttons, textboxes | STOP - Verify with count check |
| 6 | **Multi-page workflows traced** | Each navigation action has destination page documented | STOP - Re-run discovery with workflow tracing |
| 7 | **Cell content locators documented** | AG Grid cells show specific element selectors, not just "Use extensions" | STOP - Add specific locators |

---

## Scenario-to-Locator Coverage

Before writing scenarios, verify discovery notes cover all UI interactions.

For each planned scenario, list the locators it will need:

```markdown
| Scenario | Required Locators | In Discovery? |
|----------|-------------------|---------------|
| Viewing obligation details | Grid row click, Details page H1, Details URL pattern | ✅ / ❌ |
| Adding a new obligation | Add button, Form/modal locator, Form fields | ✅ / ❌ |
| Selecting via checkbox | Checkbox (pinned container), Bulk action button | ✅ / ❌ |
```

**If any scenario shows ❌:**
1. Return to discovery phase
2. Navigate to the missing UI via Playwright
3. Document the missing locators
4. Then return to feature generation
