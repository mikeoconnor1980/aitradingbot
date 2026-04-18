# TradePilot FluentValidation + YamlDotNet Pack

This pack adds a practical starter implementation for:

- FluentValidation validators
- YamlDotNet loader
- YAML DTOs
- Mapping layer from YAML DTOs to TradePilot strategy contracts

## Assumptions

- You already have the `TradePilot.Strategies` contract models
- YAML is the authoring format
- C# models are the runtime format
- Validation happens before strategy activation

## Suggested NuGet packages

- YamlDotNet
- FluentValidation

## Files

- `src/TradePilot.Strategies/Parsing/StrategyYamlModels.cs`
- `src/TradePilot.Strategies/Parsing/YamlStrategyLoader.cs`
- `src/TradePilot.Strategies/Parsing/StrategyMapper.cs`
- `src/TradePilot.Strategies/Validation/CommonValidators.cs`
- `src/TradePilot.Strategies/Validation/SignalStrategyValidator.cs`
- `src/TradePilot.Strategies/Validation/DcaStrategyValidator.cs`
- `src/TradePilot.Strategies/Validation/GridStrategyValidator.cs`
- `src/TradePilot.Strategies/Validation/ValidationExtensions.cs`

## Notes

This is a deliberately clean starter implementation, not a complete production framework.
A few advanced engine primitives remain your responsibility, for example:

- candle pattern semantics
- liquidity sweep detection
- structure shift detection
- range state detection
- regime state detection

The loader is built around a discriminator:
- `signal`
- `dca`
- `grid`