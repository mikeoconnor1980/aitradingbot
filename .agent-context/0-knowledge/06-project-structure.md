# Project Structure

Solution:

TradingApp.sln

Projects:

TradingApp.Domain  
TradingApp.Application  
TradingApp.Infrastructure  
TradingApp.Persistence  
TradingApp.Api  
TradingApp.Worker

The Domain, Application, and Persistence layers are tenant-aware.
All data access is scoped by UserId.

---

# Strategy Plugins

Strategies are implemented as plugins.

Example structure:

Application/Strategies/

GridStrategy.cs  
ITradingStrategy.cs

Future strategies may be added here without modifying the worker.

---

# Frontend

Angular UI:

/frontend/trading-ui