# Development Plan

Phase 1 — Project setup

Create .NET solution and projects.

Phase 2 — Database

Add EF Core SQLite.

Phase 3 — Strategy Plugin

Create ITradingStrategy interface.

Implement GridStrategy plugin.

Phase 4 — Strategy Config

Store strategy configuration in JSON.

Phase 5 — Worker

Worker loads strategy config and executes plugin.

Phase 6 — API

Expose endpoints:

GET /strategies  
POST /strategies  
PUT /strategies/{id}

Phase 7 — Angular UI

Add strategy builder page.