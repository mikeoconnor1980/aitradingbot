# TradePilot VS Code Review Prompt

Use this prompt in VS Code / Copilot Chat / your preferred coding assistant to review your **existing strategy engine** against the **new extracted packs** and drive an implementation comparison.

---

## Prompt

You are helping me review and potentially evolve the TradePilot strategy engine in a **C# / .NET** codebase.

I already have a **working strategy engine**, but I have also extracted a new proposed design pack that includes:

- a shared strategy schema approach
- YAML strategy instances
- strategy groups
- FluentValidation validators
- a YamlDotNet loader and mapping layer
- a derived signal engine with registry/interfaces
- starter derived signal implementations

Your job is to help me perform a **structured engineering review** of my current implementation versus this proposed design, with a focus on:

- maintainability
- readability
- extensibility
- runtime safety
- validation quality
- separation of concerns
- suitability for future strategy types like:
  - signal
  - DCA
  - grid

Do **not** assume the new pack is automatically better.  
Review both critically.

---

## Objectives

Please do the following in order:

### 1. Inspect the current engine
Review the existing codebase and identify:

- how strategies are currently represented
- how strategies are loaded/configured
- how validation is currently handled
- how conditions are evaluated
- how indicators/signals are represented
- whether there is already a concept similar to:
  - derived signals
  - strategy registry
  - strategy subtype discrimination
  - runtime compilation
  - state per strategy type

Produce a short architecture summary of the current implementation.

---

### 2. Inspect the new extracted pack
Review the extracted files and summarise the proposed architecture, especially:

- strategy schema structure
- YAML authoring approach
- C# contract model
- validation layer
- loader/mapping layer
- derived signal registry and implementations
- separation between:
  - authoring
  - validation
  - mapping
  - runtime execution

---

### 3. Compare the two designs
Create a comparison with sections for:

- readability
- maintainability
- testability
- risk of config/runtime bugs
- ease of adding new strategy types
- ease of adding new derived signals
- backtesting suitability
- live trading suitability
- likely refactor cost
- likely hidden complexity

Be specific and grounded in the actual code, not generic opinions.

---

### 4. Identify what is genuinely better in the new design
Highlight only the parts of the new design that are materially better than the current implementation.

Examples could include:
- better schema discrimination
- cleaner separation of DTO vs runtime model
- stronger validation boundaries
- better derived signal abstraction
- clearer subtype support for signal/DCA/grid

Also identify any parts that are worse, unnecessary, premature, or overly abstract.

---

### 5. Recommend a migration strategy
Do **not** recommend a full rewrite unless clearly justified.

Instead, propose an incremental path with categories:

- **Adopt now**
- **Adopt later**
- **Keep current approach**
- **Needs more design before adoption**

Aim for a realistic sequence of changes that minimises disruption.

---

### 6. Produce a concrete implementation plan
Create a phased plan such as:

#### Phase 1
Small safe improvements with high ROI

#### Phase 2
Validation/loading refactor

#### Phase 3
Derived signal abstraction

#### Phase 4
Support for DCA/grid expansion

Each phase should include:
- code areas to touch
- expected benefits
- risk level
- suggested tests

---

### 7. Generate code changes when useful
Where appropriate, propose or implement targeted improvements in-place.

Prefer:
- minimal safe refactors
- adapters/wrappers
- additive changes
- extraction of interfaces
- improved validators
- clearer contracts

Avoid unnecessary renaming churn unless it improves clarity significantly.

---

## Review standards

When reviewing, pay particular attention to these design qualities:

### Separation of concerns
Can I clearly distinguish:
- config authoring
- parsing
- validation
- mapping
- runtime execution
- signal derivation
- state management

### Strategy model quality
Can the engine support multiple strategy families cleanly:
- directional signal strategies
- DCA strategies
- grid strategies

### Derived signal model
Are concepts like these implemented in a reusable, central way:
- candle_pattern
- liquidity_sweep
- structure_shift
- range_state
- regime_state

Or are they duplicated and scattered?

### Safety
Can invalid strategy definitions be rejected before runtime?

### Maintainability
Would a new developer understand the boundaries quickly?

### Extensibility
How hard is it to add:
- a new strategy subtype
- a new derived signal
- a new condition operator
- a new execution mode

---

## Important constraints

- The codebase is **C#**, not Python.
- Treat YAML as an authoring/config format, not a runtime dependency.
- Prefer practical architecture over overengineering.
- Be honest if the current implementation is already better in some areas.
- Use the extracted pack as a reference design, not unquestioned truth.
- Optimise for long-term maintainability and clarity.
- Consider live trading implications, not just code elegance.

---

## Preferred outputs

Please provide results in this structure:

### A. Current engine summary
### B. New pack summary
### C. Comparison table
### D. Recommended adoption decisions
### E. Phased implementation plan
### F. Optional code changes / patches

If possible, point to specific files/classes/methods when making claims.

---

## Optional follow-up tasks

After the review, help me with one or more of these:

1. Extract shared interfaces from the current engine
2. Add subtype-aware strategy validation
3. Introduce a derived signal registry
4. Refactor YAML loading into DTO -> validation -> mapping -> runtime model
5. Introduce DCA and grid strategy foundations without breaking signal strategies
6. Add tests for strategy config loading and validation

Start with the review first.