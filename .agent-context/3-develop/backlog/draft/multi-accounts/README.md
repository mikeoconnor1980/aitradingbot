# Sub-Account Portfolio Management — PBI Breakdown

This epic has been split into 8 PBIs, listed in **implementation order**.
Each PBI builds on the previous ones. The dependency graph is linear with some parallelism possible in the middle tiers.

Hyperliquid sub-accounts require **$100k cumulative volume** on the master account to unlock. PBI-0 provides an automated volume farming feature to reach this threshold safely.

## Implementation Order

```
┌──────────────────────────────────────┐
│ 0. Volume Farming Unlock (standalone)│  ← Can be built independently
└──────────────────────────────────────┘

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
| 0 | [Volume Farming Unlock](pbi-draft-volume-farming-unlock.md) | — | Auto-farm 100k volume on BTC-PERP to unlock sub-accounts |
| 1 | [Sub-Account Domain Model](pbi-draft-sub-account-domain-model.md) | — | Domain entities, DB migration, backward compat |
| 2 | [Sub-Account Registration API](pbi-draft-sub-account-registration-api.md) | 1 | CRUD endpoints, credential validation |
| 3 | [Fund Transfer Engine](pbi-draft-fund-transfer-engine.md) | 1, 2 | Treasury service, idempotent transfers, audit log |
| 4 | [Portfolio Exposure Tracking](pbi-draft-portfolio-exposure-tracking.md) | 1, 2 | Net/gross exposure, aggregated equity |
| 5 | [Strategy-to-SubAccount Binding](pbi-draft-strategy-subaccount-binding.md) | 1, 2 | Strategy isolation, per-account credential routing |
| 6 | [Cross-Account Hedge Orchestration](pbi-draft-hedge-orchestration.md) | 3, 4, 5 | Threshold-based hedging, auto-rebalance |
| 7 | [Portfolio Dashboard UI](pbi-draft-portfolio-dashboard-ui.md) | 2, 3, 4, 6 | Angular dashboard, transfer form, exposure charts |

## Notes

- **PBI-0 is standalone** — can be developed in parallel with PBIs 1-7 (test using an account that already has 100k volume)
- **PBIs 3, 4, 5 can be developed in parallel** once PBI 2 is complete
- Sub-accounts require **$100k cumulative volume** on the master account (runtime prerequisite, not dev dependency)
- **PBI 7 (UI) can be started incrementally** — the sub-account list and portfolio panels can ship as soon as PBIs 2 and 4 are done, with transfer and hedge panels added later
- The original monolithic PBI is retained at [sub_account_portfolio_pbi.md](sub_account_portfolio_pbi.md) for reference
