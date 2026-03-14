# Strategy Customisation

Users can create their own strategy instances using the GridStrategy plugin.

Each user strategy consists of:

Strategy record  
StrategyConfig JSON

Example configuration:

{
  "trend": {
    "emaFast": 20,
    "emaSlow": 50,
    "emaTrend": 200
  },
  "grid": {
    "levels": 4,
    "spacing": [0.35,0.7,1.05,1.4]
  },
  "takeProfitPercent": 0.8,
  "hedgePercent": 0.3,
  "maxExposure": 2
}

Users may:

create strategy  
name strategy  
edit parameters  
activate strategy

Multiple strategies may exist but typically only one runs at a time.

The worker loads the active strategy configuration at startup.