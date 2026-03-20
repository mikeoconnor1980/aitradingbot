# PBI Specification: {{FEATURE_NAME}}

**Date:** {{DATE}}
**Author:** {{AUTHOR}}
**Status:** Draft | Ready for Review | Approved

---

## Summary

{{BRIEF_DESCRIPTION}}

### User Story

> As a **{{PERSONA}}**, I want to **{{ACTION}}** so that **{{BENEFIT}}**.

### Business Value

{{BUSINESS_VALUE_DESCRIPTION}}

---

## Requirements

### Functional Requirements

- [ ] {{REQUIREMENT_1}}
- [ ] {{REQUIREMENT_2}}
- [ ] {{REQUIREMENT_3}}

### Non-Functional Requirements

- [ ] {{NFR_1}} (e.g., Performance: Response time < 500ms)
- [ ] {{NFR_2}} (e.g., Security: Requires Manager role)
- [ ] {{NFR_3}} (e.g., Availability: 99.9% uptime)

---

## User Flow

### Happy Path

1. {{STEP_1}}
2. {{STEP_2}}
3. {{STEP_3}}

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| {{ERROR_SCENARIO_1}} | {{ERROR_RESPONSE_1}} |
| {{ERROR_SCENARIO_2}} | {{ERROR_RESPONSE_2}} |

---

## Technical Considerations

### Bounded Contexts

**Context Name:** {{BOUNDED_CONTEXT}}

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| {{HTTP_METHOD}} | `/api/{{ROUTE}}` | {{DESCRIPTION}} |


### Integration Events

| Event | Trigger | Handler Action |
|-------|---------|----------------|
| `{{Entity}}CreatedIntegrationEvent` | After entity creation | {{ACTION}} |
| `{{Entity}}UpdatedIntegrationEvent` | After entity update | {{ACTION}} |

### Jobs (if applicable)

| Job | Schedule | Description |
|-----|----------|-------------|
| `{{JobName}}` | {{CRON_OR_FREQUENCY}} | {{DESCRIPTION}} |

---

## Out of Scope

- {{OUT_OF_SCOPE_ITEM_1}}
- {{OUT_OF_SCOPE_ITEM_2}}

---

## Open Questions

- [ ] {{QUESTION_1}}
- [ ] {{QUESTION_2}}

---

## Acceptance Criteria

- [ ] All unit tests pass with >80% code coverage
- [ ] API endpoints return correct HTTP status codes
- [ ] Entity validation prevents invalid state
- [ ] Integration events are published correctly
- [ ] UI displays data as specified (if applicable)
- [ ] No tech stack compliance violations

---

## Appendix

### References

- [Tech Stack Compliance Guide](../../../.github/skills/tech-stack-compliance/reference/tech-stack-compliance.md)
- [Architecture Standards](../../../.github/skills/architecture/SKILL.md)
- [Copilot Instructions](../../../.github/copilot-instructions.md)

### Related Features

- {{RELATED_FEATURE_1}}
- {{RELATED_FEATURE_2}}
