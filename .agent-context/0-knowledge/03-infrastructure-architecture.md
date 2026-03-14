# Infrastructure Architecture

The system runs on a VPS.

Main components:

Angular UI  
C# API  
C# Worker  
SQLite database

---

# Execution Flow

Browser  
↓  
Angular UI  
↓  
API  
↓  
Trading Worker  
↓  
Hyperliquid

---

# Data Storage

SQLite database location:

/data/sqlite/tradingapp.db

Other folders:

/data/logs  
/data/backups  
/data/snapshots

---

# Deployment

Docker containers:

api  
worker  
ui

Managed with docker compose.