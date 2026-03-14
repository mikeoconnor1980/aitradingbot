# AI Grid Trading System

## Overview

This project is an experimental algorithmic trading system for cryptocurrency markets.
It combines deterministic trading strategies, modular architecture, AI-assisted market context,
and a reproducible backtesting framework.

The initial focus is on BTC perpetual markets using the Hyperliquid exchange,
but the architecture is designed to support additional strategies and exchanges in the future.

Unlike many retail trading bots, the primary focus of this system is:
- deterministic strategy execution
- strong architectural separation
- reproducible backtesting
- safe automation

---

# Core Strategy Concept

The initial strategy implemented is a multi-timeframe pullback grid strategy.

Strategy structure:

4H trend filter  
↓  
1H directional bias  
↓  
15m pullback grid entries  
↓  
short hedge on confirmed breakdown

This approach attempts to trade mean-reversion inside an existing trend while protecting downside risk.

---

# Key Design Principles

## Deterministic Candle-Based Execution

Strategies execute only when candles have fully closed.

This avoids common trading bot problems such as:
- trading on partially formed candles
- inconsistent strategy signals
- backtests behaving differently from live trading

Execution is triggered by a dedicated CandleClock scheduling system.

---

## Modular Trading Pipeline

Trading logic is separated into clear layers.

StrategyEngine  
↓  
Signal generation  
↓  
RiskEngine  
↓  
PositionManager  
↓  
ExecutionEngine

This separation improves maintainability and allows risk rules to be applied consistently across strategies.

---

## Strategy Configuration System

Strategies are defined using configuration rather than hardcoded logic.

Parameters can include:

- grid levels
- grid spacing
- take profit thresholds
- hedge activation conditions
- position sizing rules

Strategies are stored as JSON and can be versioned and backtested.

---

## Grid Lifecycle State Model

Grid strategies require careful management of multiple entries, fills and exits.

The system introduces a GridState model and lifecycle state machine.

Example lifecycle:

Idle  
↓  
GridPending  
↓  
GridActive  
↓  
TakeProfitPending  
↓  
Closed

This dramatically reduces conditional logic within strategy implementations.

---

# AI-Assisted Market Context

The system optionally integrates LLM-generated sentiment and macro context.

AI is used to provide contextual signals such as:

MarketSentiment: Bullish | Neutral | Bearish  
MacroRegime: RiskOn | Neutral | RiskOff  
EventRisk: Low | Medium | High

The LLM does not generate trades directly.

Instead it influences risk behaviour such as position sizing or entry restrictions.

---

# Backtesting Engine

The system includes a historical replay engine that allows strategies to run on historical market data.

Backtesting pipeline:

Historical data  
↓  
ReplayEngine  
↓  
CandleClock  
↓  
StrategyScheduler  
↓  
StrategyEngine  
↓  
SimulatedExecutionEngine

Because the same pipeline is used for live and historical execution, backtests are more reliable.

---

# Market Data Storage

Historical candles are stored locally using SQLite.

Benefits include:

- fast multi-year backtests
- reproducible datasets
- reduced exchange API usage

Candles are indexed by:

symbol  
timeframe  
timestamp

Backtests load entire ranges into memory for replay.

---

# Scheduling Architecture

A dedicated CandleClock system detects when candles close and triggers strategy evaluation.

Market Data  
↓  
CandleClock  
↓  
StrategyScheduler  
↓  
Strategy Pipeline

This model ensures strategies run exactly once per candle.

---

# Technology Stack

Backend

.NET / C#

Frontend

Angular

Data storage

SQLite

Exchange integration

Hyperliquid API

Optional AI integration

OpenAI or Anthropic

---

# What Makes This Different

Many retail trading bots suffer from:

- tightly coupled strategy and execution logic
- poor backtesting capability
- strategies embedded directly in code
- trading on partially formed candles
- complex grid management

This project addresses these issues by providing:

- deterministic candle-based execution
- modular trading architecture
- configurable strategy definitions
- explicit grid lifecycle management
- unified backtest and live execution pipeline
- optional AI-assisted market context

---

# Project Status

The project is currently experimental and under active development.

Goals include:

- validating the grid strategy framework
- building robust backtesting infrastructure
- exploring AI-assisted trading context

Future work may include:

- additional strategies
- multi-exchange support
- automated parameter optimisation
- portfolio-level risk management

---

# Disclaimer

This project is for research and experimentation only.

Algorithmic trading involves significant financial risk and no guarantee of profitability.