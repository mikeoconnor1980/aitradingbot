# Kronos Foundation Model — Evaluation & Assessment

**Evaluated:** 2026-04-12
**Repository:** https://github.com/shiyu-coder/Kronos
**Paper:** https://arxiv.org/abs/2508.02739 (Accepted AAAI 2026)
**Authors:** Yu Shi, Zongliang Fu, Shuo Chen, Bohan Zhao, Wei Xu, Changshui Zhang, Jian Li (Tsinghua University)
**License:** MIT
**Stars/Forks:** ~15.2K / 2.9K (as of evaluation date)

## What It Is

Kronos is a decoder-only foundation model pre-trained specifically on financial candlestick (K-line) sequences. It tokenizes OHLCV data into hierarchical discrete tokens using Binary Spherical Quantization (BSQ), then uses autoregressive Transformer prediction — treating financial candle sequences like a language.

### Key Architecture

1. **K-line Tokenizer**: Transformer-based autoencoder quantizes each OHLCV candle into a coarse + fine subtoken pair (20-bit codebook total, split 10+10).
2. **Autoregressive Predictor**: Decoder-only Transformer predicts next-candle subtokens sequentially (coarse first, then fine conditioned on coarse).
3. **Inference**: Monte Carlo rollouts with temperature/top-p sampling, averaged for stability.

### Model Sizes

| Model | Params | Context | Available |
|-------|--------|---------|-----------|
| Kronos-mini | 4.1M | 2048 | Yes (HuggingFace) |
| Kronos-small | 24.7M | 512 | Yes (HuggingFace) |
| Kronos-base | 102.3M | 512 | Yes (HuggingFace) |
| Kronos-large | 499.2M | 512 | **Not released** |

### Training Data

- 12.11 billion K-line records from 45+ global exchanges
- 7 temporal frequencies (1min to weekly)
- Asset classes: stocks, crypto, forex, futures, ETFs, indices
- 100% financial data (vs <1% financial data in general TSFMs)

## Claim Verification

| Claim | Verdict | Detail |
|-------|---------|--------|
| "First open-source foundation model for financial K-lines" | Mostly true | Competitors (PLUTUS, DELPHYNE) exist but have not released code/models |
| "12 billion records, 45 exchanges" | True | Confirmed in paper Table 13 |
| "93% more accurate than leading TSFM" | Misleading | 93% improvement in RankIC (a rank correlation metric). Actual values are ~0.02→0.039 — tiny absolute numbers. Not 93% accuracy. |
| "87% over best non-pretrained baseline" | Same caveat | Relative improvement on small-value metric |
| "Zero-shot, no fine-tuning" | True | Legitimate zero-shot evaluation including out-of-distribution exchanges |
| "4 model sizes" | Partially true | Kronos-large (499M) is NOT released |
| "Accepted at AAAI 2026" | True | arXiv preprint 2508.02739 confirmed |
| "MIT License, free" | True | Confirmed |
| "Runs on your laptop" | True for mini model | Larger models need GPU |

## Security Assessment

**No malicious code detected.** Full source review found:

- No obfuscated code, no `eval()`, no hidden `exec()`, no encoded payloads
- No data exfiltration, no telemetry, no hidden network requests
- No supply chain attacks — minimal dependencies (torch, numpy, pandas, etc.)
- Model weights from HuggingFace via standard PyTorchModelHubMixin
- WebUI is straightforward Flask on localhost:7070

## Critical Limitations

1. **The "93% better" framing is deceptive.** RankIC values are 0.02–0.06 range — the model explains a tiny fraction of actual price variance. A 93% relative gain on near-zero baselines is mathematically correct but practically marginal for trading.

2. **The paper itself disclaims trading use:** *"This pipeline is intended as a demonstration... not a production-ready quantitative trading system."*

3. **Backtesting only on Chinese A-shares (CSI 300/800).** No validated live P&L. The "live demo" is visual only — predicted vs actual candle charts.

4. **No transaction costs, slippage, liquidity, or market impact** modeled in the benchmarks.

5. **512-token context limit** for small/base models (Kronos-mini has 2048).

6. **Predicts price levels, not trading signals.** Outputs raw OHLCV predictions, not buy/sell/hedge decisions.

## Relevance to This Project

### Potential Use: Context Signal Provider

Kronos could serve as a **context signal** in our architecture, similar to how we integrate LLM signals (see `17-llm-context-sentiment-architecture.md`):

- **Predicted volatility regime** → feed into RiskEngine for position sizing
- **Price direction confidence** → one input among many for strategy decisions
- **NOT a replacement** for StrategyEngine, GridController, or RiskEngine

### Integration Challenges

- **Python/PyTorch model vs our C#/.NET stack** — requires a Python sidecar service or HTTP microservice
- **Inference latency** — autoregressive generation is slow (sequential token-by-token)
- **Post-processing needed** — raw OHLCV predictions must be transformed into actionable signals
- **No real-time streaming** — batch prediction on historical context windows

### Decision

**Not incorporating at this time.** The model is academically interesting but:

- Our LLM context provider architecture (`17-llm-context-sentiment-architecture.md`) already handles external signal integration
- The cross-language integration overhead (Python sidecar) adds complexity for marginal signal quality
- The model's actual predictive edge (RankIC ~0.04) is too small to justify infrastructure changes
- If we revisit, it would slot in as another `IContextSignalProvider` implementation

## References

- Paper: https://arxiv.org/abs/2508.02739
- HuggingFace models: https://huggingface.co/NeoQuasar
- Live demo: https://shiyu-coder.github.io/Kronos-demo/
