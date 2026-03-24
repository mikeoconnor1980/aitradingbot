---
applyTo: "**/AcceptanceTests/**,**/*.AcceptanceTests/**"
---

# Acceptance Testing Standards

Concise guidelines for writing acceptance tests in DTS applications.

---

## Project Structure
<!-- DOT-CUSTOM-START:acceptance-testing-structure -->
```
AcceptanceTests/
├── Features/           # Gherkin .feature files
├── Pages/              # Page objects (inherit from PageObject.cs)
│   └── Components/     # Reusable UI component objects
├── Steps/              # Step definitions grouped by domain concept
├── Services/           # Supporting services
├── Settings/           # Configuration classes
└── Shared/             # Utilities (comparers, operators)
```
<!-- DOT-CUSTOM-END:acceptance-testing-structure -->

---

## Test Framework
<!-- DOT-CUSTOM-START:acceptance-testing-framework -->

- Use the assertion library specified in the `testing-framework` custom section in `.github/instructions/testing.instructions.md`
<!-- DOT-CUSTOM-END:acceptance-testing-framework -->


## Naming Conventions

### Files

| Type | Pattern | Example |
|------|---------|---------|
| Feature | `{Feature}.feature` | `DelimitedFileImport.feature` |
| Page object | `{Page}Page.cs` | `UploadModalPage.cs` |
| Page component | `{Component}Component.cs` | `SandboxHeaderComponent.cs` |
| Steps | `{DomainConcept}Steps.cs` | `FileUploadSteps.cs`, `NavigationSteps.cs` |

### Step Organization

Steps are grouped by **domain concept**, not by feature file. Each step class owns a single slice of behavior and is reusable across any feature.

| Step class | Responsibility |
|------------|----------------|
| `NavigationSteps.cs` | Authentication and page navigation |
| `FileUploadSteps.cs` | File selection, upload actions, upload button state |
| `DelimiterSteps.cs` | Delimiter selection and custom delimiter input |
| `DataPreviewSteps.cs` | Data preview assertions (columns, headers, rows) |
| `CommonSteps.cs` | Generic UI actions (buttons, notifications, dropdowns) |

**Do NOT use `[Scope(Tag = "...")]`** — it prevents step reuse and is considered a code smell in BDD. If two features need similar wording, make the Gherkin unambiguous instead of scoping steps.

### Methods

| Type | Pattern | Example |
|------|---------|---------|
| Async action | `{Verb}{Target}Async` | `ClickRefreshAsync()` |
| Assertion | `Assert{Condition}Async` | `AssertGridVisibleAsync()` |
| Query | `Get{Property}Async` | `GetRowCountAsync()` |
| Boolean | `Is{Condition}Async` | `IsGridVisibleAsync()` |

---

## Step Definition Pattern

Steps are organized by domain concept, not by feature. Each class is unscoped so any feature can compose steps from multiple classes.

```csharp
[Binding]
public sealed class FileUploadSteps
{
    private readonly TestHarness _testHarness;
    private readonly ScenarioContext _scenarioContext;

    public FileUploadSteps(TestHarness testHarness, ScenarioContext scenarioContext)
    {
        _testHarness = testHarness;
        _scenarioContext = scenarioContext;
    }

    // Fresh page instance each access - avoid stale references
    private UploadModalPage UploadModalPage => new(_testHarness.Page);

    [Given(@"I am on the Upload modal with a ""(.*)"" file selected")]
    public async Task GivenIAmOnTheUploadModalWithAFileSelected(string extension)
    {
        // ...
    }

    [Then(@"the Upload button should be enabled")]
    public async Task ThenTheUploadButtonShouldBeEnabled()
    {
        var isEnabled = await UploadModalPage.IsUploadButtonEnabledAsync();
        isEnabled.Should().BeTrue();
    }
}
```

### Why no `[Scope]`?

- Scoped steps cannot be reused across features — every feature needing "I am authenticated" requires its own copy
- Ambiguity should be resolved by writing precise Gherkin, not by hiding steps behind tags
- Domain-concept grouping scales better as the test suite grows

---

## Page Object Pattern

Inherit from the `PageObject` base class in the `Pages/` folder.

```csharp
public sealed class FeaturePage : PageObject
{
    public FeaturePage(IPage page) : base(page, "feature-path") { }

    // Locators as properties
    private ILocator PageContent => Page.Locator("body");             // Safe parent for grid extensions
    private ILocator Grid => Page.Locator("ag-grid-angular");         // Grid element itself (for visibility checks)
    private ILocator RefreshButton => Page.GetByRole(AriaRole.Button, new() { Name = "Refresh" });

    // Actions
    public async Task ClickRefreshAsync() => await RefreshButton.ClickAsync();

    // Queries
    public async Task<bool> IsGridVisibleAsync() => await Grid.IsVisibleAsync();
    
    // ⚠️ Grid extensions: pass PageContent (body), NOT Grid locator
    public async Task WaitForGridToLoadAsync() => await PageContent.WaitForGridToHaveDataAsync();
}
```

---

## Component Pattern

Components represent **reusable UI elements** that appear across multiple pages (e.g., headers, navigation bars, modals).

### When to Create a Component

Create a component when:
- A UI element appears on multiple pages with identical behavior
- The element has significant interaction logic worth encapsulating
- You want to avoid duplicating locators and methods

**Store components in `Pages/Components/` and name them `{Component}Component.cs`.**

### How Page Objects Use Components

**✅ Preferred Pattern: Encapsulation with Delegation**

```csharp
public sealed class UploadsPage : PageObject
{
    private readonly SandboxHeaderComponent _header;

    public UploadsPage(IPage page) : base(page, "uploads")
    {
        _header = new SandboxHeaderComponent(page);
    }

    // Delegate to the component - page controls its API
    public async Task SelectFirstWorkAreaAsync()
    {
        await _header.SelectFirstWorkAreaAsync();
    }
}
```

**Why this is better:**
- **Encapsulation** - Steps don't need to know about internal components
- **Law of Demeter** - Avoids chains like `page.Header.SelectAsync()`
- **Flexibility** - Add page-specific logic before/after delegation
- **Refactoring** - Change implementation without affecting steps

### When to Expose Components Publicly

**Rarely.** Only expose a component if:
- The component represents a **distinct, independent UI widget** that steps need to interact with directly
- The component has many methods and delegating all of them would be impractical

If you must expose it, use a property:

```csharp
// Only when truly necessary
public SandboxHeaderComponent Header => _header;
```

**Default rule:** Keep components private, expose methods.

---

## FluentAssertions

Always use FluentAssertions:

```csharp
visible.Should().BeTrue();
count.Should().BeGreaterThan(0);
text.Should().Contain("expected");
items.Should().HaveCount(5);
result.Should().NotBeNull();
```

---

## Scenario Context

Share data between steps using `ScenarioContext`. This is especially important for **generated entity names** — steps that create entities should store names in context so subsequent steps can retrieve them without the feature file needing to reference the name.

```csharp
// Store
_scenarioContext.Set(value, "Key");

// Retrieve
var value = _scenarioContext.Get<Type>("Key");
```

### Example: Pipeline Creation Flow

The step that creates a pipeline generates a unique name and stores it. Later steps retrieve it from context.

**Feature file** — no hardcoded names:
```gherkin
Scenario: Running a pipeline with a Filter Data task
    Given I have created a pipeline with a "Filter Data" task
        | Field         | Value   |
        | Filter Column | Product |
    When I run the pipeline
    Then the pipeline run should complete successfully
    When I delete the pipeline run
    And I delete the pipeline
```

**Step definitions** — unique names generated and stored in context:
```csharp
[Given(@"I have created a pipeline with a ""(.*)"" task")]
public async Task GivenIHaveCreatedAPipelineWithATask(string taskType, Table table)
{
    var pipelineName = $"AT_{Guid.NewGuid():N}";
    _scenarioContext.Set(pipelineName, "PipelineName");
    await CreatePipelineWithTaskAsync(pipelineName, taskType, table);
}

[When(@"I run the pipeline")]
public async Task WhenIRunThePipeline()
{
    var pipelineName = _scenarioContext.Get<string>("PipelineName");
    var runName = $"{pipelineName}_Run";
    _scenarioContext.Set(runName, "PipelineRunName");
    await WizardPage.RunPipelineAsync(runName);
}

[When(@"I delete the pipeline")]
public async Task WhenIDeleteThePipeline()
{
    var pipelineName = _scenarioContext.Get<string>("PipelineName");
    await PipelinesPage.DeletePipelineAsync(pipelineName);
}
```

---

## String Escaping in Attributes

```csharp
// ✅ Correct - double quotes in verbatim string
[Then(@"I should see the ""Save"" button")]

// ❌ Wrong - backslash causes CS1056
[Then(@"I should see the \"Save\" button")]
```

---

## Locator Extensions (Preferred)

Use the extension methods in `Extensions/Locator/` for DDS/DDX components.

**⚠️ CRITICAL: ALL extension methods expect a PARENT CONTAINER, not the component itself.**
Use `PageContent` (= `Page.Locator("body")`) as a safe default.

```csharp
// Define PageContent in your Page Object
private ILocator PageContent => Page.Locator("body");

// ✅ CORRECT - PageContent is the parent, extensions find components internally
await PageContent.WaitForGridToFinishLoadingAsync();  // Finds ag-grid-angular
await PageContent.WaitForGridToHaveDataAsync();
await PageContent.IsTabActiveAsync("Obligations");    // Finds dds-tabs
await PageContent.SelectTabAsync("Details");
await PageContent.ClickButtonAsync("Submit");         // Finds .dds-btn

// ❌ WRONG - component locator causes double-chaining
await Page.Locator("ag-grid-angular").WaitForGridToHaveDataAsync();  // FAILS!
await Page.Locator("dds-tabs").IsTabActiveAsync("Obligations");      // FAILS!
```

**Grid operations:**
```csharp
var rows = await PageContent.GetGridRowsAsync<ObligationViewModel>();
await PageContent.ClickGridRowAsync(myViewModel);
await PageContent.ToggleGridRowSelection(myViewModel);
await PageContent.TriggerGridRowActionAsync(myViewModel, GridAction.Edit);
var isSelected = await PageContent.IsGridRowSelectedAsync(myViewModel);
```

**Dropdowns** (exception - these work on the dropdown locator directly):
```csharp
await dropdown.SelectDropdownOptionAsync("Option 1");
await dropdown.SelectDropdownOptionAsync(index: 0);
await dropdown.ToggleDropdownOptionsAsync(["Option 1", "Option 2"]);
var hasValue = await dropdown.HasSelectedDropdownValueAsync("Selected Value");
```

**Why:** These extensions encapsulate DDS component quirks, handle waits automatically, and provide consistent patterns.

---

## ⚠️ CRITICAL: Extension Methods Cause Double-Chaining If Misused

**ALL DDS extension methods locate their target component internally.** Never pass the component locator itself.

| Extension | Internal lookup | ❌ Wrong call | ✅ Correct call |
|-----------|-----------------|---------------|-----------------|
| `WaitForGridToHaveDataAsync()` | `ag-grid-angular` | `Grid.WaitForGridToHaveDataAsync()` | `PageContent.WaitForGridToHaveDataAsync()` |
| `IsTabActiveAsync("X")` | `dds-tabs` | `Page.Locator("dds-tabs").IsTabActiveAsync()` | `PageContent.IsTabActiveAsync()` |
| `ClickButtonAsync("X")` | `.dds-btn` | `Button.ClickButtonAsync()` | `PageContent.ClickButtonAsync()` |

**Error signature:** When you see double selectors in error messages, you've passed the wrong locator:
```
waiting for Locator("ag-grid-angular").Locator("ag-grid-angular") to be visible
waiting for Locator("dds-tabs").Locator("dds-tabs").GetByRole(...) to be visible
```

**Fix:** Replace the component locator with `PageContent` (`Page.Locator("body")`).

---

## AG Grid (via Extensions)

**Prefer extension methods** over raw selectors. Remember: `parent` = container that holds the grid (use `Page.Locator("body")` when unsure).

```csharp
// ✅ Preferred - PageContent is Page.Locator("body"), a safe default
await PageContent.WaitForGridToFinishLoadingAsync();
var data = await PageContent.GetGridRowsAsync<MyViewModel>();
var row = data.First(r => r.Name == "Test");
await PageContent.ClickGridRowAsync(row);

// ✅ Grid row actions (Edit, Delete, View, Copy, Download)
await PageContent.TriggerGridRowActionAsync(row, GridAction.Edit);

// ✅ Row selection
await PageContent.ToggleGridRowSelection(row);
var selected = await PageContent.IsGridRowSelectedAsync(row);
await PageContent.ToggleGridSelectAllCheckbox();

// ❌ WRONG - GridLocator IS the grid, causes double-chaining
await GridLocator.WaitForGridToFinishLoadingAsync();  // FAILS!

// ❌ Avoid - raw selectors
await Page.Locator(".ag-overlay-loading-center").WaitForAsync(...);
var rows = Page.Locator(".ag-row");
```

---

## Anti-Patterns

| ❌ Avoid | ✅ Use Instead |
|----------|----------------|
| `Task.Delay(1000)` | `await element.WaitForAsync()` |
| `Thread.Sleep()` | Playwright wait methods |
| Static page objects | Fresh instance per access |
| Magic strings in steps | Named locators in page objects |
| `element.IsVisibleAsync().Result` | `await element.IsVisibleAsync()` |
| Asserting animations/transitions | Assert stable state (URL, text, row count) |
| Hardcoded timeouts | Playwright auto-wait with `Expect()` |
| Inventing new step patterns | Reuse existing steps (search first) |
| `[Scope(Tag = "...")]` on steps | Unscoped steps grouped by domain concept |
| Feature-scoped step classes | Domain-concept step classes |
| Mutable fields for inter-step data | `ScenarioContext.Set/Get` |
| Hardcoded entity names in features | Generate unique names with GUID in steps, store in `ScenarioContext` |
| `Grid.WaitForGridToHaveDataAsync()` | `PageContent.WaitForGridToHaveDataAsync()` |
| `Page.Locator("dds-tabs").IsTabActiveAsync()` | `PageContent.IsTabActiveAsync()` |
| `ComponentLocator.ExtensionMethod()` | `PageContent.ExtensionMethod()` |

---

## Step Reuse Policy

**Before writing new step definitions:**

1. **Read existing Steps/*.cs files** - grep `[Given|When|Then]` to find existing patterns
2. **Reuse exact wording** - don't invent synonyms for existing steps
3. **Mark new steps explicitly** - if unavoidable, comment `// NEW STEP`

**Rule:** If a needed step doesn't exist:
- Reuse the closest existing step, OR
- Emit `// TODO: NEW STEP REQUIRED - {proposed pattern}`

**Why:** Copilot invents steps that *sound right* but don't match existing bindings, causing runtime "no matching step" errors.

```csharp
// ❌ Copilot invents: "When I click the first row"
// ✅ Actual step is:   "When I click on row 0"
// Check step-dictionary.md BEFORE writing!
```

---

## Pre-Generation Checklist

Before writing step definitions:

1. **Search existing steps** - grep `[Given|When|Then]` in Steps/ folder
2. **Verify method signatures** - grep Page Object for actual methods
3. **Check for duplicates** - ensure the step wording doesn't already exist in another step class
4. **No invented steps** - reuse existing patterns or mark as TODO

---

## Test Data Strategy

Tests should be **idempotent** and **environment-agnostic**.

### Principles

| Principle | Description |
|-----------|-------------|
| **Use existing data** | Query for data that meets test criteria |
| **Create then cleanup** | If creating data, clean up after |
| **Isolated contexts** | Each test should not affect others |
| **No hardcoded IDs** | Use search/filter to find suitable data |
| **Unique entity names** | Generate names with GUID to prevent parallel test collisions |

### Unique Entity Names

When tests create entities (pipelines, uploads, etc.), **always generate unique names** with a GUID suffix and store them in `ScenarioContext`. Feature files should **never contain hardcoded entity names** — the step definition owns the naming.

```csharp
// ✅ Unique name generated in step, stored in context
var pipelineName = $"AT_{Guid.NewGuid():N}";
_scenarioContext.Set(pipelineName, "PipelineName");

// ✅ Derived names built from the stored base name
var runName = $"{pipelineName}_Run";
_scenarioContext.Set(runName, "PipelineRunName");

// ❌ Hardcoded names - collide in parallel runs
var pipelineName = "AT_Filter";
var runName = "AT_Filter_Run";
```

**Feature file pattern** — entity names are implicit, not passed as parameters:
```gherkin
# ✅ Correct - no hardcoded names, context-driven
Given I have created a pipeline with a "Filter Data" task
When I run the pipeline
When I delete the pipeline

# ❌ Wrong - hardcoded names that collide in parallel
Given I have created a pipeline "AT_Filter" with a "Filter Data" task
When I run the pipeline with name "AT_Filter_Run"
When I delete the pipeline "AT_Filter"
```

### Data Query Pattern

```csharp
// ❌ Hardcoded - breaks across environments
var client = await GetClientByNameAsync("Client ABC");

// ✅ Query for any suitable data
var client = await FindFirstAccessibleClientAsync();
```

### Test Data Tags

For tests requiring specific data conditions, use tags:

```gherkin
@RequiresMultipleClients
Scenario: User can switch between clients
    Given I have access to multiple clients
    ...

@RequiresEmptyWorkArea
Scenario: Empty state message is displayed
    Given I have no Work Area access
    ...
```

**Why:** Tags allow filtering tests based on data availability and document preconditions.
