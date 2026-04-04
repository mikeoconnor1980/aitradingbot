# Sub-Account Portfolio Management — PBI Breakdown

This epic has been split into 7 independent PBIs, listed in **implementation order**.
Each PBI builds on the previous ones. The dependency graph is linear with some parallelism possible in the middle tiers.

## Implementation Order

```
┌─────────────────────────────────────┐
│ 1. Sub-Account Domain Model         │  ← Foundation: entities, migrations
└──────────────────┬──────────────────┘
                   │
┌──────────────────▼──────────────────┐
│ 2. Sub-Account Registration API     │  ← CRUD for sub-accounts
└──────────────────┬──────────────────┘
                   │
        ┌──────────┼──────────┐
        ▼          ▼          ▼
┌───────────┐ ┌─────────┐ ┌──────────────┐
│ 3. Fund   │ │ 4. Port-│ │ 5. Strategy- │  ← Can be built in parallel
│ Transfer  │ │ folio   │ │ to-Account   │
│ Engine    │ │ Exposure│ │ Binding      │
└─────┬─────┘ └────┬────┘ └──────┬───────┘
      │             │             │
      └─────────────┼─────────────┘
                    ▼
┌──────────────────────────────────────┐
│ 6. Cross-Account Hedge Orchestration │  ← Requires 3, 4, 5
└──────────────────┬───────────────────┘
                   │
┌──────────────────▼──────────────────┐
│ 7. Portfolio Dashboard UI            │  ← Consumes all backend PBIs
└─────────────────────────────────────┘
```

## PBI Summary

| # | PBI | Depends On | File |
|---|-----|-----------|------|
| 1 | [Sub-Account Domain Model](pbi-draft-sub-account-domain-model.md) | — | Domain entities, DB migration, backward compat |
| 2 | [Sub-Account Registration API](pbi-draft-sub-account-registration-api.md) | 1 | CRUD endpoints, credential validation |
| 3 | [Fund Transfer Engine](pbi-draft-fund-transfer-engine.md) | 1, 2 | Treasury service, idempotent transfers, audit log |
| 4 | [Portfolio Exposure Tracking](pbi-draft-portfolio-exposure-tracking.md) | 1, 2 | Net/gross exposure, aggregated equity |
| 5 | [Strategy-to-SubAccount Binding](pbi-draft-strategy-subaccount-binding.md) | 1, 2 | Strategy isolation, per-account credential routing |
| 6 | [Cross-Account Hedge Orchestration](pbi-draft-hedge-orchestration.md) | 3, 4, 5 | Threshold-based hedging, auto-rebalance |
| 7 | [Portfolio Dashboard UI](pbi-draft-portfolio-dashboard-ui.md) | 2, 3, 4, 6 | Angular dashboard, transfer form, exposure charts |

## Notes

- **PBIs 3, 4, 5 can be developed in parallel** once PBI 2 is complete
- **PBI 7 (UI) can be started incrementally** — the sub-account list and portfolio panels can ship as soon as PBIs 2 and 4 are done, with transfer and hedge panels added later
- The original monolithic PBI is retained at [sub_account_portfolio_pbi.md](sub_account_portfolio_pbi.md) for reference
