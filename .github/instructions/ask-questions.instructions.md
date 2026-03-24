# `ask_questions` Tool Usage Guidelines

Best practices for using the `ask_questions` tool in DTS agent workflows.

---

## When to Use `ask_questions`

Use the `ask_questions` tool when:
- Acceptance criteria are vague or contradictory
- Navigation path is unclear
- Business rules are ambiguous
- Edge cases aren't addressed
- Implementation approach has multiple valid options

**DO NOT use `ask_questions` when:**
- The answer is determinable from code or context
- You can make a reasonable decision yourself
- Just reporting status or problems
- Confirming something obvious

---

## Best Practices

| Rule | Description |
|------|-------------|
| **Batch questions** | Combine related questions into a single call (max 4 questions) |
| **Provide context** | Explain what is being decided and why |
| **Propose defaults** | Mark the `recommended` option to suggest YOUR preferred approach |
| **Keep options exclusive** | Use `multiSelect: true` only when choices are additive |
| **Max 6 options** | Keep option lists focused; "Other" is added automatically |
| **Short headers** | Max 12 characters for the header/identifier |

---

## Example

```json
{
  "questions": [
    {
      "header": "Entry Point",
      "question": "The story mentions 'Obligations page' but there are multiple entry points. Which should be the test starting point?",
      "options": [
        { "label": "Main navigation menu", "description": "User clicks Obligations in nav", "recommended": true },
        { "label": "Dashboard widget", "description": "User clicks from dashboard" },
        { "label": "Direct URL", "description": "User navigates directly to /obligations" }
      ]
    },
    {
      "header": "Edge Cases",
      "question": "Should we test the scenario where the user has no obligations?",
      "options": [
        { "label": "Yes", "description": "Include empty state scenario", "recommended": true },
        { "label": "No", "description": "Assume user always has obligations" }
      ]
    }
  ]
}
```

---

## Question Categories

| Category | Example Question | When to Ask |
|----------|------------------|-------------|
| **Entry Point** | "Which navigation path should be the primary test path?" | Multiple ways to reach feature |
| **Scope** | "Should we test all status transitions or just the happy path?" | Story scope is ambiguous |
| **User Roles** | "Does this apply to all user roles or specific ones?" | Role-based behavior unclear |
| **Edge Cases** | "Should empty states be covered?" | Edge cases not specified |
| **UI Behavior** | "Is this a modal dialog or inline expansion?" | Component type unclear |
| **Missing Coverage** | "The story only verifies navigation, not content. Should I add verification?" | Discovery reveals more than story specifies |

---

## ⚠️ Ask, Don't Embellish

If discovery reveals UI elements not covered by the story acceptance criteria:

| ❌ Don't | ✅ Do |
|----------|--------|
| Silently add extra Then assertions | Ask user if coverage should be expanded |
| Assume all discovered elements need testing | Follow story scope exactly |
| Add "helpful" extra checks | Let user decide what matters |
