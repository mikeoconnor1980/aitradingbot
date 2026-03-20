---
name: pbi-interview
description: Interview the user to gather comprehensive requirements for a new feature or refine an existing PBI. Generates a detailed specification document after validating against the tech stack.
---

# PBI Interview Skill

## Purpose

This skill conducts an interactive interview with the user to gather comprehensive requirements for a new pbi. It ensures all proposed technical solutions comply with the approved tech stack before generating a detailed specification document.

## When to Use

- When the user says "interview me" about a pbi
- When the user asks to "plan a feature", "plan a pbi", "plan a user story", "refine an existing pbi" or "gather requirements"
- When a request is ambiguous and needs a spec before implementation
- When planning a new bounded context, entity, integration, or API endpoint
- Before any significant development effort to ensure alignment with architecture

## Prerequisites

This skill depends on:
- **[agent knowledge](../../../.agent-context/0-knowledge/)**: Existing knowledge and architecture principles
- **[project standards](../../instructions/)**: Project standards for coding, testing, and documentation

## Interview Process

> **CRITICAL**: You MUST use the `askQuestions` tool for ALL interview questions. Never ask questions in plain text messages—always invoke the tool.

### Phase 1: Context Gathering

Ask ONE question at a time using the `askQuestions` tool, wait for the answer, then adapt your next question. Cover these areas:
1. **User Story**
   - Who is the primary user/persona?
   - What problem does this feature solve?
   - What is the business value?

2. **Happy Path**
   - Step-by-step user flow from start to finish
   - What data is required as input?
   - What is the expected output/result?

3. **Edge Cases & Error States**
   - What happens when things go wrong?
   - Validation requirements
   - Offline/degraded behavior (if applicable)
   - Permission/authorization requirements

4. **Technical Considerations**
   - Does this require database changes?
   - New API endpoints needed?
   - Integration points and integration with external services?
   - Event-driven requirements?
   - Data storage and privacy
   - Security
   - Performance/ scalability needs?

> Always use the `askQuestions` tool with appropriate options when gathering responses. Provide 2-4 answer options where applicable to streamline the interview.

### Phase 2: Tech Stack Validation

Before finalizing the spec, validate all proposed technical choices against [agent knowledge](../../../.agent-context/0-knowledge/) and [project standards](../../instructions/):

1. **Architecture Compliance**
   - Confirm bounded context organization

2. **Technology Compliance**
   - Validate proposed packages/libraries against approved list
   - Check frontend technology choices (if applicable)

3. **Testing Compliance**
   - Confirm MSTest will be used (not xUnit or NUnit)
   - Verify Builder pattern is planned for test data
   - Check acceptance test approach if E2E testing is needed

If any proposed solution violates the tech stack:
- Clearly explain the violation to the user
- Suggest the compliant alternative from the approved stack
- Update the spec to use compliant technologies

### Phase 3: Specification Generation

Once the interview is complete and tech stack is validated, generate the specification document using the [template](template.md).

## Interview Guidelines

### Do's

- **ALWAYS use the `askQuestions` tool**—never ask questions in plain text
- Ask ONE focused question at a time
- Wait for the user's response before asking the next question
- Adapt questions based on previous answers
- Offer options (A/B choices) when appropriate
- Probe for specifics when answers are vague
- Summarize understanding before moving to a new topic area

### Don'ts

- Don't dump a list of questions at once
- Don't assume technical solutions—ask the user first
- Don't proceed with non-compliant technologies
- Don't skip the tech stack validation phase
- Don't generate the spec until you have sufficient clarity

## Sample Interview Flow

```
Agent: "What problem does this feature solve, and who is the primary user?"
User: "We need a way for managers to approve expense reports."

Agent: "Got it. When a manager opens an expense report for approval, 
        what information do they need to see to make a decision?"
User: "The line items, total amount, and the submitter's name."

Agent: "What should happen after the manager approves or rejects? 
        Any notifications or follow-up actions?"
User: "Email the submitter and update the report status."

Agent: "That notification requirement means we'll need an integration event. 
        Per our tech stack, this would use DTS.Extensions.EventBus with 
        Azure Service Bus. Does that align with your expectations?"
User: "Yes."

... (continue until spec is complete)
```

## Output

The skill produces a specification document to respond. 


The output contains:

1. Feature summary and user story
2. Detailed requirements with acceptance criteria (checkboxes)
4. API signatures (endpoints, request/response types) (if applicable)
6. Integration events (if applicable)
7. Tech stack compliance confirmation
8. Out of scope items

## Execution Steps

1. **Initiate Interview**: Begin with context-gathering questions
2. **Iterate**: Continue until all areas are covered
3. **Validate Tech Stack**: Cross-reference with [agent knowledge](../../../.agent-context/0-knowledge/) and [project standards](../../instructions/)
4. **Generate Spec**: Create the specification document from [template](template.md)
5. **Confirm with User**: Ask user to review and approve the spec
