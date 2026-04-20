# Business Model Options

This document records the business-model decision in light of the implemented architecture.

## Decision

**Option C — Split Architecture is the chosen model.**

The implemented system uses a control plane plus client-side execution model rather than server-side private-key custody.

### Implementation Evidence

- `src/TradePilot.Worker/TradePilot.Worker.csproj` builds `TradePilot.ExecutionAgent` with `SelfContained=true` and `RuntimeIdentifier=win-x64` for client deployment.
- `src/TradePilot.Api/Program.cs` explicitly documents that the control plane does not hold private keys and that private keys live only on the execution agent.
- `src/TradePilot.Worker/Services/AgentCheckInService.cs` binds the worker to the API through `Agent.ControlPlaneUrl` and heartbeat polling.
- `src/TradePilot.Api/Controllers/AgentController.cs` plus `src/TradePilot.Application/Agent/Services/AgentCommandStore.cs` implement the control-plane heartbeat and command protocol.
- `deploy/worker/build-installer.ps1` and the Inno Setup assets in `deploy/worker/` package the agent as a Windows Service distribution.

## What Option C Means in This Codebase

The current implementation is a split operational model:

- the Angular UI and ASP.NET Core API act as the control plane
- the execution agent runs on the subscriber machine as `TradePilot.ExecutionAgent`
- wallet addresses are known to the platform
- private keys remain local to the agent
- the API can start, stop, and supervise execution, but it does not sign orders

The current product also has a non-billed entitlement model layered on top of Option C:

- Beginner and Pro tiers exist as application entitlements
- both tiers use a 1-year testing trial
- Profile supports self-service subscribe/cancel actions
- billing and payment collection are still deferred

The implementation is slightly more execution-heavy on the agent than the earliest Option C wording implied. In practice, the live trading session, exchange connectivity, and signing all run on the agent while the control plane handles configuration, orchestration, monitoring, and fleet control.

## Option Summary

| Option | Description | Current Status |
|---|---|---|
| Option A — Self-hosted full bot | User runs the entire product stack locally | Not chosen |
| Option B — Platform-hosted execution | Platform stores user trading credentials and executes centrally | Not chosen |
| Option C — Split architecture | Control plane in the cloud, execution and key custody on the user machine | Chosen and implemented |

## Why Option C Was Chosen

| Driver | Reason |
|---|---|
| Key custody | Avoids server-side storage of customer private keys |
| Security posture | Removes the highest-risk failure mode associated with cloud credential custody |
| Product control | Still allows a managed control plane, UI, auth, monitoring, and fleet operations |
| Operational flexibility | Agents can be updated and supervised without centralising signing authority |
| Hyperliquid fit | Wallet-based signing makes client-side custody a natural boundary |

## Tradeoffs

Option C keeps the strongest benefit of self-custody, but it does add real operational complexity:

- users still need a machine to run the agent
- agent availability matters for live execution
- command delivery and update rollout become product responsibilities
- operator tooling must cover fleet health, kill switch behavior, and version drift

Those tradeoffs are preferable to holding customer private keys in the API platform.

## Entitlement Model Status

| Capability | Status |
|---|---|
| Beginner tier | Implemented |
| Pro tier | Implemented |
| Trial-only access | Implemented |
| Paid billing | Not implemented |
| Stripe checkout | Not implemented |

This means the business model is still pre-commercial even though the product now exposes tier-specific capability differences.

## Future Recommendations

- Define whether future paid tiers will differentiate on control-plane features, not on key custody.
- Add stronger agent authentication and rollout controls before large-scale subscriber onboarding.
- Revisit whether any limited hosted-execution offering is worth considering only after the regulatory and trust tradeoffs are explicitly accepted.
