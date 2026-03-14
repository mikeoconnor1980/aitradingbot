# LLM Context & Sentiment Architecture

This document describes how an LLM can be integrated into the trading system as a contextual signal provider.

The LLM is used to generate qualitative market context such as sentiment, macro regime classification, and event risk levels.

It should never be responsible for placing trades directly.

Instead, it augments the MarketContext used by the strategy engine.

---

# Purpose

The goal of LLM integration is to provide:

• Market sentiment classification  
• Macro regime detection  
• Event risk identification  
• Human-readable explanations for strategy decisions  

This information can influence strategy behaviour and risk management.

---

# Architecture Position

Pipeline:

MarketData
→ Indicators
→ MarketContextBuilder
→ LlmContextProvider
→ StrategyEngine
→ Signals
→ RiskEngine
→ PositionManager
→ ExecutionEngine

The LLM provides context, not trading instructions.

---

# LLM Context Model

Example data model:

public class LlmContext
{
    public string MarketSentiment { get; set; } = "Neutral";
    public string MacroRegime { get; set; } = "Neutral";
    public string EventRisk { get; set; } = "Low";
    public decimal Confidence { get; set; }
    public string Summary { get; set; } = "";
    public DateTime GeneratedAtUtc { get; set; }
}

---

# Example Output

{
  "marketSentiment": "Bearish",
  "macroRegime": "RiskOff",
  "eventRisk": "High",
  "confidence": 0.81,
  "summary": "Risk sentiment is weak due to macro uncertainty and elevated event risk."
}

---

# How Strategies Use It

Strategies should treat the LLM output as a modifier rather than a signal.

Examples:

If EventRisk == High:
    Disable new entries

If MacroRegime == RiskOff:
    Reduce position size

If MarketSentiment == Bullish:
    Allow full position size

---

# Strategy Modes

LLM context can map to strategy modes:

Aggressive  
Normal  
Defensive  
RiskOff

Example mapping:

Bullish sentiment + low event risk → Aggressive  
Neutral sentiment → Normal  
Bearish sentiment → Defensive  
High event risk → RiskOff  

These modes influence parameters such as:

• position size multiplier  
• grid spacing  
• hedge sensitivity  

---

# Data Sources

Potential sources used by the LLM analysis service:

• crypto news feeds  
• macroeconomic calendars  
• social sentiment summaries  
• curated market commentary  

The system should preprocess inputs before sending them to the LLM.

---

# Update Frequency

LLM analysis should run periodically rather than continuously.

Recommended cadence:

• every 15 minutes  
• hourly  
• on major news events  

The latest result is cached and injected into MarketContext.

---

# Storage

LLM outputs should be stored for audit and analysis.

Example table:

LlmSnapshots

Fields:

Id  
MarketSentiment  
MacroRegime  
EventRisk  
Confidence  
Summary  
GeneratedAtUtc  

---

# UI Usage

The dashboard can display the LLM summary to help explain system behaviour.

Example:

"Market sentiment is currently bearish due to macro uncertainty and elevated event risk."

---

# Safety Guidelines

The LLM should never:

• place trades  
• bypass risk checks  
• override the risk engine  
• generate direct exchange orders  

It should only provide contextual signals that influence strategy behaviour.

---

# Future Enhancements

Possible future improvements:

• sentiment trend detection  
• multi-source sentiment aggregation  
• AI-assisted grid optimisation  
• macro regime forecasting