# DCA Next Steps

Date: 2026-04-18

## Current State

- DCA strategy configuration is available in the strategy builder.
- DCA evaluation and scheduling are wired into the backend and backtesting path.
- Live DCA starts are now blocked intentionally because spot order execution is not implemented yet.
- Hyperliquid testnet WebSocket connectivity is currently unreliable from this environment and returned `502 Bad Gateway` during verification.

## Immediate Next Things To Do

- Implement true Hyperliquid spot execution support for DCA buys.
- Add a capability check in the UI so unsupported live DCA strategies are clearly marked before the user clicks start.
- Surface the API rejection reason in the app so users see why live DCA cannot start yet.
- Restart local API and worker services and verify the new fail-fast behavior end to end.
- Retest the Hyperliquid testnet WebSocket endpoint to determine whether the `502` issue is temporary or persistent.

## Short-Term Engineering Follow-Up

- Decouple fixed-schedule DCA evaluation from the live trade WebSocket so hourly and daily schedules can still progress during stream outages.
- Introduce a polling or candle-snapshot fallback for confirmed 1h closes used by DCA scheduling.
- Add worker-side telemetry for rejected live strategy starts so control-plane diagnostics are clearer.
- Add integration coverage for the full DCA start flow, including unsupported-live rejection handling.
- Add an explicit product warning in the DCA config card that backtesting is supported but live spot execution is pending.

## Product Decisions To Confirm

- Decide whether DCA should remain backtest-only until full spot support is complete.
- Decide whether testnet DCA validation should use simulated execution while spot support is being built.
- Decide whether the first live DCA release is single-asset only or includes the planned multi-asset portfolio flow.
- Decide whether profit-taking rules are part of the first live DCA release or a later phase.

## Recommended Delivery Order

1. Finish live spot execution support.
2. Add UI messaging and unsupported-mode handling.
3. Add schedule fallback that does not depend on the trade WebSocket.
4. Re-verify against testnet and then run a controlled paper-style validation flow.
5. Expand from single-asset DCA to multi-asset portfolio DCA.

## Acceptance Checks For The Next Round

- Starting a DCA strategy from the UI gives a clear result instead of silent worker-side failure.
- A supported live DCA strategy can place a real or simulated spot buy on schedule.
- Hourly DCA evaluation still works when the trade WebSocket is down.
- Worker health reporting distinguishes exchange outage, unsupported mode, and internal execution errors.
- Backtest and live scheduling rules stay aligned for DCA intervals and gates.