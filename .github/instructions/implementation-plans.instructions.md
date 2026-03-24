---
applyTo: "**/*-plan.instructions.md"
---

# Implementation Plans

## Plan Structure

Implementation Plans have the following structure:

```markdown
---
applyTo: ".agent-context/3-develop/build/changes/{{date}}-{{task_description}}-changes.md"
currentAgent: "Implementation Planner"
agentStartedAt: "2026-01-01T10:00:00Z"
status: "planned"
lastUpdated: "2026-01-01T10:01:15Z"
---

<!-- markdownlint-disable-file -->

{{plan}}

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-01-01T10:00:00Z | 2026-01-01T10:01:15Z |
```

## Status Workflow

Plans are built to work in an agentic workflow for building a single PBI. Frontmatter is used to define the current Agent and status in the workflow.

| Agent | Start Status | End Status | Description |
|-------|-------------|------------|-------------|
| Implementation Planner | — | `planned` | Creates the implementation plan |
| Plan Reviewer | `plan-in-review` | `plan-reviewed` | Reviews plan against project standards and design |
| Plan Implementer | `in-progress` | `implemented` | Implements the plan phases |
| Implementation Reviewer | `in-review` | `complete` | Reviews implementation and finalises outputs |

> **Note**: The Implementation Planner agent creates the plan file, so it only sets an end status. All other agents update an existing plan file with both start and end statuses.

### Status Updates

The `status`, `currentAgent`, `agentStartedAt` and `lastUpdated` fields MUST be updated on the current plan frontmatter EVERY time an Agent STARTS and FINISHES their custom workflow. Use the **utc-datetime** skill to obtain timestamps (ISO 8601 format: `yyyy-MM-ddTHH:mm:ssZ`).

#### When an Agent STARTS

1. Set `currentAgent` to the agent's name
2. Set `status` to the appropriate status value
3. Set `agentStartedAt` to the current UTC datetime
4. Set `lastUpdated` to the current UTC datetime
5. **Append a row** to the `## Agent Log` table at the bottom of the plan file (not required by Implementation Planner since it creates the initial row):

```markdown
| {{agent_name}} | {{started_status}} | {{agentStartedAt_value}} | - |
```

#### When an Agent FINISHES

1. Set `currentAgent` to `"None"`
2. Set `status` to the completed status value
3. Set `lastUpdated` to the current UTC datetime
4. **Update row** in the `## Agent Log` table at the bottom of the plan file:

```markdown
| {{agent_name}} | {{completed_status}} | {{agentStartedAt_value}} | {{lastUpdated_value}} |
```

> The Agent Log provides a full audit trail of which agents worked on the plan and how long each took. It MUST NOT be modified or removed — only appended to.
