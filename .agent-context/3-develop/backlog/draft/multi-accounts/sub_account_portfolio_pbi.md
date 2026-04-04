# 📌 PBI: Sub-Account Portfolio Management & Fund Transfer Engine

## 🎯 Objective

Enable the trading platform to manage **multiple Hyperliquid
sub-accounts**, including: - automated fund allocation\
- internal transfers between accounts\
- portfolio-level exposure tracking\
- hedge orchestration across accounts

This will allow advanced strategies (e.g. hedging, multi-strategy
execution) while maintaining isolation between strategies.

## 🧠 Background / Context

Hyperliquid only supports a **single net position per asset per
account**, which limits: - hedging strategies\
- grid + hedge combinations\
- multi-strategy execution on the same asset

To overcome this: - Each strategy will run on a **separate
sub-account**\
- A central **Portfolio Engine** will manage exposure and capital
allocation\
- A **Treasury Service** will handle fund transfers between accounts

## 🏗️ High-Level Architecture

UI (Portfolio Dashboard) ↓ Portfolio Engine (Core Logic) ↓
----------------------------------- \| Trading Workers (per account) \|
\| Treasury Service (transfers) \| ----------------------------------- ↓
Hyperliquid API (Main + Sub Accounts)

## 🧩 Key Components

### Portfolio Engine

-   Tracks all sub-accounts\
-   Calculates net exposure\
-   Triggers hedge strategies\
-   Allocates capital

### Treasury Service

-   Transfers funds between accounts\
-   Rebalances capital\
-   Enforces rules

### Trading Workers

-   One per sub-account\
-   Executes strategies\
-   No fund control

### UI Dashboard

-   Aggregated balances\
-   Exposure view\
-   Strategy performance

## 🔧 Functional Requirements

-   Manage sub-accounts\
-   Transfer funds via API\
-   Track balances + exposure\
-   Trigger hedging\
-   Rebalance capital

## 🔐 Non-Functional Requirements

-   Secure API key handling\
-   Separation of concerns\
-   Retry + idempotency\
-   Audit logging

## 🧪 Acceptance Criteria

-   Sub-accounts registered\
-   Transfers work via API\
-   Portfolio aggregates correctly\
-   Hedging offsets exposure\
-   Transfers logged

## 🏁 Outcome

👉 Multi-strategy, portfolio-level trading system
