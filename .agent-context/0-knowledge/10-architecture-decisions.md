# Architecture Decisions

ADR 1 — Backend Language

C# (.NET) chosen for performance and maintainability.

ADR 2 — Frontend

Angular chosen for structured enterprise architecture.

ADR 3 — Database

SQLite chosen for simplicity and single-node deployment.

ADR 4 — Strategy Architecture

Strategies implemented as C# plugins.

Initial plugin:

GridStrategy

Future plugins:

TrendBreakoutStrategy  
MeanReversionStrategy

ADR 5 — Strategy Configuration

Users configure strategies using JSON configuration.

The JSON is stored in the database and interpreted by the strategy engine.