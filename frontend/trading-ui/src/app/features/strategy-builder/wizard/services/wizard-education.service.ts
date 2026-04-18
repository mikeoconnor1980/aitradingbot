import { Injectable } from "@angular/core";

export interface WizardStepEducation {
  title: string;
  question: string;
  description: string;
  tips: string[];
}

export interface TemplateEducation {
  id: string;
  label: string;
  icon: string;
  description: string;
  bestFor: string;
  available: boolean;
}

@Injectable({ providedIn: "root" })
export class WizardEducationService {
  public readonly templates: TemplateEducation[] = [
    {
      id: "grid",
      label: "Grid Strategy",
      icon: "grid_on",
      description: "Places a ladder of buy orders at regular intervals below the current price. Profits from range-bound markets by catching dips and selling on bounces.",
      bestFor: "Sideways / range-bound markets",
      available: true
    },
    {
      id: "dca",
      label: "DCA",
      icon: "stacked_line_chart",
      description: "Scheduled accumulation — buy at regular intervals with optional price gates and scaling bands. Set a budget cap and let it run.",
      bestFor: "Long-term accumulation / dollar-cost averaging",
      available: true
    },
    {
      id: "signal",
      label: "Signal",
      icon: "tune",
      description: "Build your own entry logic by combining indicators like RSI, MACD, and EMA crossovers. Full control over when to enter and exit.",
      bestFor: "Experienced traders with a specific edge",
      available: true
    },
  ];

  public getStepEducation(stepIndex: number): WizardStepEducation {
    return this._steps[stepIndex] ?? this._steps[0];
  }

  private readonly _steps: WizardStepEducation[] = [
    {
      title: "Get Started",
      question: "How do you want to create your strategy?",
      description: "Pick a pre-built strategy from the library, or choose a strategy type to build your own from scratch.",
      tips: []
    },
    {
      title: "Market & Timeframe",
      question: "What are you trading and on what timeframe?",
      description: "Select the market pair and candle timeframe. The timeframe determines how often your strategy evaluates conditions.",
      tips: [
        "Shorter timeframes (5m, 15m) generate more signals but are noisier — expect more false entries.",
        "Longer timeframes (4h, 1d) give fewer but higher-conviction signals.",
        "BTC-USD is the most liquid market on Hyperliquid with the tightest spreads."
      ]
    },
    {
      title: "Entry Logic",
      question: "How should the strategy enter trades?",
      description: "Configure when the strategy opens positions. Grid strategies use price levels; signal strategies use indicator conditions.",
      tips: [
        "For grids: more levels with tighter spacing = more trades but smaller position per level.",
        "For signals: combining multiple conditions (AND logic) reduces false entries but may miss opportunities.",
        "The breakdown threshold determines when a grid is abandoned if price moves too far against you."
      ]
    },
    {
      title: "Exit Rules",
      question: "When should the strategy close trades?",
      description: "Define how profits are captured and losses are limited. Good exit rules are as important as good entries.",
      tips: [
        "A 2:1 reward-to-risk ratio means your winners need to be twice the size of your losers.",
        "ATR trailing stops adapt to market volatility — wider in volatile markets, tighter in calm ones.",
        "Always enable a stop loss. Without one, a single adverse move can wipe out many small wins."
      ]
    },
    {
      title: "Risk Management",
      question: "How much capital should each trade risk?",
      description: "Position sizing and risk limits determine how much you can lose on any single trade. This is the most important section.",
      tips: [
        "Professional traders typically risk 1-2% of their account per trade.",
        "Leverage amplifies both gains AND losses. Start with 1x until you're profitable.",
        "Max open trades limits your total exposure. More concurrent trades = higher total risk.",
        "Cooldown prevents re-entering immediately after a stop loss — gives the market time to settle."
      ]
    },
    {
      title: "Trend Filter",
      question: "Want to filter trades by the overall trend direction?",
      description: "A trend filter can reduce false signals by only allowing trades aligned with the larger trend. This step is optional.",
      tips: [
        "A simple price-above-200-EMA filter removes most counter-trend trades.",
        "EMA cross filters (e.g. 50/200) are slower to react but more reliable.",
        "Apply the filter to 'both' directions to only trade in the direction of the trend."
      ]
    },
    {
      title: "Review & Create",
      question: "Does everything look right?",
      description: "Review your strategy configuration before saving. You can always edit it later from the strategy list.",
      tips: [
        "After saving, run a backtest to see how this strategy would have performed historically.",
        "You can switch to the full builder to fine-tune any detail before saving.",
        "The AI review feature can analyse your strategy for potential issues once saved."
      ]
    }
  ];
}
