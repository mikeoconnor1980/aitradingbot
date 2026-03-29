# UI Design

Main dashboard shows:

chart  
positions (Actions column: Close; SL/TP columns showing trigger prices; "Set SL/TP" button when none set; inline remove per field)  
orders (Actions column: Cancel, Cancel All, Modify per row)  
activity feed (live fill and order update events; 100-event cap; third tab alongside Positions and Orders)  
signals  
bot state

---

# Strategy Configuration Screen

Users can:

create strategy  
rename strategy  
edit parameters  
activate strategy

Configuration is saved as JSON.

---

# Chart

Chart uses TradingView Lightweight Charts.

Displays:

candles  
grid levels  
entry line  
hedge line  
take profit