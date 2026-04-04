# AI Strategy Review

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-04T08:28:46Z

## User Story

As a trader, I want to run an AI review on my trading strategy JSON from the strategy screen so that I get a structured, critical assessment of my strategy's design, risks, and weaknesses before deploying it.

## Problem Statement

Traders currently have no way to get an objective, automated review of their strategy configuration. They must manually assess their own strategies, which is error-prone and lacks the structured critical analysis an LLM can provide. Key risks such as missing stop losses, unrealistic leverage, or poor market regime fit may go unnoticed until real capital is at risk.

## Requirements

### Functional Requirements

1. An "AI Review" button on the strategy editor/builder page for saved strategies
2. Clicking the button sends the saved strategy JSON to the backend for LLM analysis using a dedicated strategy review prompt
3. A collapsible summary section appears below the JSON editor showing a brief preview of the review
4. A "View Full Review" button opens the complete analysis in a centered modal overlay
5. The review response is rendered as formatted markdown
6. Each strategy revision stores exactly one AI review; re-running on the same revision overwrites the previous review
7. Previous revision reviews are accessible — the user can view the linked review for any past revision
8. The UI shows a non-blocking loading indicator (spinner) while the review is in progress; the user can continue editing
9. A 1-minute cooldown per strategy prevents excessive LLM API usage; the button is disabled with a countdown during the cooldown period
10. A separate configuration section `LlmReview` in `appsettings.json` configures the review LLM (provider, model, API key, timeout) independently from the existing `Llm` section
11. The strategy review system prompt is stored server-side (not sent from the client)
12. An "Apply Suggestions" button is visible in the review UI but rendered as disabled (greyed out) with a tooltip "Coming Soon" — this is a placeholder for a future feature

### Non-Functional Requirements

- LLM response time: review should complete within 30 seconds under normal conditions
- The review LLM configuration must be independently swappable without affecting the strategy interpreter or context provider LLM
- Error handling: graceful failure with user-friendly message if the LLM call fails or times out

## Acceptance Criteria

- [ ] **Given** a saved strategy is open in the strategy editor, **When** the user clicks the "AI Review" button, **Then** the strategy JSON is sent to the backend and an AI review is returned and displayed below the editor as a collapsible markdown summary
- [ ] **Given** a review summary is displayed below the editor, **When** the user clicks "View Full Review", **Then** a centered modal opens showing the full rendered markdown review
- [ ] **Given** the user has just run an AI review, **When** they attempt to run another review within 1 minute for the same strategy, **Then** the button is disabled and shows a countdown timer
- [ ] **Given** a review is in progress, **When** the user observes the UI, **Then** a loading spinner is visible and the form remains interactive
- [ ] **Given** a strategy revision already has a review, **When** the user re-runs the review on the same revision, **Then** the previous review is overwritten with the new one
- [ ] **Given** a strategy has multiple revisions with reviews, **When** the user views a past revision, **Then** the linked review for that revision is displayed
- [ ] **Given** the LLM call fails or times out, **When** the error occurs, **Then** a user-friendly error message is shown and the user can retry
- [ ] **Given** the strategy has not been saved, **When** the user views the editor, **Then** the AI Review button is disabled with a tooltip indicating the strategy must be saved first
- [ ] **Given** the `LlmReview` configuration section is set in `appsettings.json`, **When** the application starts, **Then** the review LLM client uses that configuration independently from the `Llm` section
- [ ] **Given** a review is displayed (summary or modal), **When** the user sees the "Apply Suggestions" button, **Then** it is visually disabled (greyed out) with a "Coming Soon" tooltip and is not clickable

### Release Notes Information

- **Heading**: AI Strategy Review
- **Release note type**: Feature
- **Release Note Summary**: A new "AI Review" button on the strategy editor allows traders to get an automated, structured critical analysis of their trading strategy from an LLM. Reviews cover entry/exit logic quality, risk management, market fit, execution realism, and improvement suggestions. Reviews are persisted per strategy revision.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### Configuration

- New `LlmReview` configuration section in `appsettings.json` with the same shape as `LlmOptions` (Provider, BaseUrl, ModelName, ApiKey, TimeoutSeconds)
- Registered as a separate named/typed options instance so the review LLM client is independently configured
- Currently uses the same Gemini provider/model but is separately configurable for future upgrade to a more powerful model

### LLM Integration

- Reuse the existing `OpenAiCompatibleLlmClient` infrastructure from `TradingApp.AI`
- Register a second `ILlmClient` instance (keyed/named) for review specifically
- System prompt stored server-side as a constant or resource — not configurable by the client
- User message is the serialized strategy JSON

### API Endpoints

- `POST /api/strategies/{id}/review` — triggers AI review for a saved strategy; returns the review markdown
- `GET /api/strategies/{id}/reviews` — retrieves stored review for the current or specified revision

### Persistence

- Store review result linked to strategy revision (review text, timestamp, model used)

### Strategy Review Prompt

The following system prompt is stored server-side and used for all strategy reviews. The user message is the serialized strategy JSON.

```text
You are an expert trading strategy reviewer.
You are NOT executing trades, NOT validating schema, and NOT guaranteeing profitability.
Your role is to critically review a trading strategy defined in JSON and provide a structured, objective assessment of its design, risks, and potential weaknesses.

IMPORTANT RULES:
- The JSON is already structurally valid and executable.
- Do NOT validate schema or syntax.
- Do NOT assume missing fields exist.
- Only analyse what is explicitly present.
- If something is missing, call it out as missing — do not infer values.
- Do NOT claim the strategy is profitable or safe.
- Avoid absolute statements like "this will work".
- Be critical, realistic, and practical.
- Distinguish clearly between facts and inferences.

---

REVIEW THE STRATEGY ACROSS THESE DIMENSIONS:

1. Strategy Summary
   - What type of strategy is this? (trend-following, mean reversion, breakout, grid, etc.)
   - Describe how it works in plain English

2. Entry Logic Quality
   - Are the entry signals clear and logical?
   - Any risk of noise / false signals?
   - Any obvious weaknesses?

3. Exit Logic Completeness
   - Are take profit and stop loss defined?
   - Is exit logic realistic and balanced?
   - Any missing exit conditions?

4. Risk Management
   - Position sizing approach
   - Use of leverage
   - Stop loss presence and quality
   - Exposure concentration risks
   - Missing safeguards (daily loss caps, max trades, etc.)

5. Strategy Weaknesses
   - Where is this likely to fail?
   - Market conditions where performance may degrade

6. Market Regime Fit
   - Trending / ranging / volatile / low liquidity suitability

7. Complexity & Overfitting Risk
   - Is the strategy overly complex?
   - Too many conditions?
   - Risk of curve fitting?

8. Execution Realism
   - Would this work in real markets?
   - Consider slippage, latency, spread, liquidity

9. Missing Elements
   - What important components are absent?

10. Improvement Suggestions
    - Practical, actionable suggestions only

---

OUTPUT FORMAT:
- Return your review as markdown using the numbered section headings above.
- Use bullet points within each section.
- Keep the total review under 1500 words.
- End with a brief one-paragraph overall assessment.

ADDITIONAL INSTRUCTIONS:
- Keep explanations concise but meaningful
- Use bullet-style phrasing inside arrays
- Be honest and critical, not polite
- If something is good, say why — but always look for weaknesses
- If key risk controls are missing, highlight them strongly
```

## Out of Scope

- Applying AI suggestions automatically to modify the strategy JSON
- Comparison view between revision reviews
- Scoring or rating system derived from review dimensions
- Streaming/SSE response from the LLM (entire response returned at once)
- Client-side prompt customization — the review prompt is fixed server-side
