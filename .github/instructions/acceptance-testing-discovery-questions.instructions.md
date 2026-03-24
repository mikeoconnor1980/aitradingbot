# Discovery Questions — Ambiguity Triggers & Deviation Analysis

Standards for identifying ambiguity in journey files and handling deviations between journey specs and live application discovery.

> 📖 **`ask_questions` general usage**: See `.github/instructions/ask-questions.instructions.md`

---

## Ambiguity Triggers (Pre-Discovery)

> ⛔ **MANDATORY**: If ANY of these triggers apply, you MUST use `ask_questions` BEFORE starting discovery.
> Do NOT silently make assumptions — wrong assumptions are expensive to fix after code generation.
> When multiple triggers fire, batch them into `ask_questions` calls of up to 4 questions each.

<!-- DOT-CUSTOM-START:acceptance-testing-discovery-questions -->


<!-- DOT-CUSTOM-END:acceptance-testing-discovery-questions -->

### Trigger Table

Scan the journey file for these triggers:

| # | Trigger | Detection Heuristic | Question to Ask |
|---|---------|---------------------|----------------|
| 1 | **Vague view/assert outcomes** | Outcome says "displays details" or "shows information" without listing specific fields/values to verify | Which specific fields/values should be asserted? |
| 2 | **Unspecified user role** | No `persona.role` defined, or role lacks permission context | Which user role should be used for this journey? |
| 3 | **Multiple navigation paths** | Action could be achieved via sidebar, breadcrumb, URL, or in-page link | Which navigation path should the test follow? |
| 4 | **Ambiguous element references** | Target uses generic terms ("the button", "the section header") without exact label text | What is the exact label/text of the element to interact with? |
| 5 | **Missing edge cases** | All steps cover only the happy path; no error, validation, or empty-state steps exist | Should error/validation/boundary scenarios be included? |
| 6 | **Unclear data dependencies** | Steps reference "first item", "an obligation", or `{today}` without specifying selection criteria or format | What test data prerequisites are assumed? What input formats are expected? |
| 7 | **Read-only verification unclear** | Outcome mentions "read-only" or "disabled" without specifying which elements to check | Which specific elements should be verified as read-only/disabled? |

### Detection Heuristics (Applied During Journey Scan)

The agent MUST scan every step's `outcome` and `target` fields against these patterns:

| Pattern to Detect | Trigger # | Example Match |
|-------------------|-----------|---------------|
| Outcome contains "displays ... details" without field list | 1 | `"Page displays obligation details with action buttons"` |
| Outcome contains "shows additional ... details" | 1 | `"Expandable section shows additional obligation details"` |
| Outcome says "action buttons" without naming them | 1, 4 | `"action buttons for completing and adding comments"` |
| Target says "section header" without exact text | 4 | `"Details expandable section header"` |
| Target references "first" or ordinal position in a list | 6 | `"Grid row (first obligation)"` |
| Value uses placeholder like `{today}` without format | 6 | `"Completed Date" field` with value `{today}` |
| Outcome mentions "disabled or hidden" without specifics | 7 | `"form fields and action buttons are disabled or hidden"` |
| Outcome says "read-only" without listing elements | 7 | `"Page is in read-only mode"` |
| Only happy-path steps exist; no error/validation steps | 5 | No steps for "Save without required fields" |

### How to Ask

- **Batch related triggers** into a single `ask_questions` call (max 4 questions, 2-6 options each)
- **Always propose a `recommended` default** so the user can confirm quickly
- **Include context** from the specific journey step(s) that triggered the question
- **Reference step numbers** so the user can cross-check the journey file
- If more than 4 triggers fire, **prioritise by impact** (data dependencies and vague assertions first, edge cases last) and make a second `ask_questions` call for the remainder

### Example — Batched Ambiguity Questions

The following example shows how to batch 4 questions when multiple triggers fire on the same journey:

```json
{
  "questions": [
    {
      "header": "View Details",
      "question": "Steps 6-7 say 'displays obligation details' and 'shows additional details' but don't list specific fields. Should the test assert specific field labels/values, or just verify the sections are visible?",
      "options": [
        { "label": "Verify sections visible only", "description": "Assert the Details and Completing sections render without checking individual fields", "recommended": true },
        { "label": "Verify specific fields", "description": "Assert exact field labels — I'll list which fields during discovery" },
        { "label": "Both", "description": "Assert sections visible AND verify key field labels" }
      ]
    },
    {
      "header": "Read-Only",
      "question": "Step 15 says 'form fields and action buttons are disabled or hidden' after completing. Which elements should the test specifically verify as read-only?",
      "options": [
        { "label": "Status dropdown + Save button", "description": "Verify these two key elements are disabled/hidden", "recommended": true },
        { "label": "All form fields", "description": "Verify every editable field on the page is now disabled" },
        { "label": "Discover during exploration", "description": "I'll identify all affected elements during discovery and confirm with you" }
      ]
    },
    {
      "header": "Data & Format",
      "question": "Step 5 clicks 'Grid row (first obligation)' and Step 13 fills '{today}' into Completed Date. Two questions: (a) Should the test always use the first grid row, or target a specific obligation? (b) What date format does the picker expect?",
      "options": [
        { "label": "First row + discover format", "description": "Use whatever is first in the grid; discover date format during exploration", "recommended": true },
        { "label": "Named obligation", "description": "Target a specific obligation by name — please specify which one" },
        { "label": "Any with Open status", "description": "Find any row with Open status and use that" }
      ]
    },
    {
      "header": "Edge Cases",
      "question": "The journey covers only the happy path (complete an obligation successfully). Should I add scenarios for validation errors or alternative flows?",
      "options": [
        { "label": "Happy path only", "description": "Match journey scope exactly — no extra scenarios", "recommended": true },
        { "label": "Add validation", "description": "Add scenario: Save without Completed Date shows error" },
        { "label": "Add status transitions", "description": "Add scenarios for Open→In Progress and other transitions" },
        { "label": "All of the above", "description": "Happy path + validation + all status transitions" }
      ]
    }
  ]
}
```

> **Note**: The example above addresses triggers 1 (vague assertions), 4 (ambiguous elements), 6 (data dependencies), 5 (missing edge cases), and 7 (read-only verification) in a single batched call of 4 questions.

---

## Deviation Analysis (Post-Discovery)

> ⛔ **MANDATORY**: This analysis runs after discovery and BEFORE feature generation.
> Skipping this risks generating features that silently deviate from the journey spec.

### Deviation Detection

After completing discovery, the agent MUST systematically compare:

| Aspect | Journey Says | Discovery Found | Deviation? |
|--------|-------------|-----------------|------------|
| **Grid columns** | Lists specific columns | Only some visible (virtualization, conditional display) | Check counts |
| **Form fields** | Lists specific fields | Fields present/absent/renamed | Check labels |
| **Navigation path** | Specific click sequence | Different UI elements or paths | Check elements |
| **Available actions** | Specific buttons/options | Different or additional buttons | Check names |
| **Dropdown options** | Specific values | Different or additional values | Check options |
| **Page structure** | Specific layout | Different component structure | Check hierarchy |

### ⛔ MANDATORY: Deviation Report & Questions

If **ANY** deviations are found, the agent MUST:

1. **List all deviations** clearly in a summary table
2. **Use `ask_questions`** to get the user's decision on how to handle them
3. **Ask whether the journey file should be updated** to match reality

**The agent MUST NOT silently adjust the feature file to match what it discovered without asking.**

### Deviation Report Template

```markdown
## ⚠️ Deviation Report: Journey vs Discovery

| # | Aspect | Journey Specifies | Discovery Found | Deviation |
|---|--------|-------------------|-----------------|----------|
| 1 | Grid columns | 11 columns listed | 4 always-visible, 7 conditionally hidden or virtualized | Column count mismatch |
| 2 | Status options | "Open, Completed" | "Open, In Progress, Completed, Not Applicable" | Additional options |
| 3 | Navigation | "Click obligation name" | Name is a link in AG Grid row | Matches (no deviation) |
```

### Required Deviation Questions

```json
{
  "questions": [
    {
      "header": "Deviations",
      "question": "Discovery found deviations from the journey spec (see report above). How should the feature file handle these?",
      "options": [
        { "label": "Match journey exactly", "description": "Feature file follows journey spec, ignore extra discoveries" },
        { "label": "Match discovered reality", "description": "Feature file reflects what was actually found in the app", "recommended": true },
        { "label": "Let me review each", "description": "I'll decide on each deviation individually" }
      ]
    },
    {
      "header": "Update YAML",
      "question": "Should the journey YAML file be updated to reflect what was discovered in the live application?",
      "options": [
        { "label": "Yes, update the journey", "description": "Amend the YAML to match discovered reality", "recommended": true },
        { "label": "No, keep journey as-is", "description": "Journey stays unchanged; deviations noted in discovery only" }
      ]
    }
  ]
}
```

### Journey File Update (If Approved)

If the user approves updating the journey file:

1. **Read the current journey YAML** from the path stored during init
2. **Apply only the approved deviations** — do NOT rewrite the entire file
3. **Preserve the original structure** — only change specific values (e.g., column lists, option values)
4. **Add a comment** noting the update was based on discovery:
   ```yaml
   # Updated by Acceptance Test Design agent based on live app discovery (YYYY-MM-DD)
   ```
5. **Show the diff** to the user before saving

### No Deviations Found

If discovery matches the journey exactly:
- Log: "No deviations found between journey spec and discovered application state"
- **AUTO-CONTINUE** to `generate-features` phase
