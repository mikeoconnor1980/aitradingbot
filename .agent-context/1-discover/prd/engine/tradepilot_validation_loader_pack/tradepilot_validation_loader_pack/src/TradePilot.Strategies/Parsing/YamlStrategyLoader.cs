using FluentValidation;
using FluentValidation.Results;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TradePilot.Strategies.Validation;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TradePilot.Strategies.Parsing;

public sealed class YamlStrategyLoader
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    private readonly IValidator<SignalStrategyYaml> _signalValidator;
    private readonly IValidator<DcaStrategyYaml> _dcaValidator;
    private readonly IValidator<GridStrategyYaml> _gridValidator;

    public YamlStrategyLoader(
        IValidator<SignalStrategyYaml>? signalValidator = null,
        IValidator<DcaStrategyYaml>? dcaValidator = null,
        IValidator<GridStrategyYaml>? gridValidator = null)
    {
        _deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _signalValidator = signalValidator ?? new SignalStrategyValidator();
        _dcaValidator = dcaValidator ?? new DcaStrategyValidator();
        _gridValidator = gridValidator ?? new GridStrategyValidator();
    }

    public IReadOnlyList<StrategyBase> LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        using var reader = File.OpenText(filePath);
        return Load(reader);
    }

    public IReadOnlyList<StrategyBase> Load(TextReader reader)
    {
        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0)
            return Array.Empty<StrategyBase>();

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        if (!root.Children.TryGetValue(new YamlScalarNode("strategies"), out var strategiesNode))
            throw new InvalidOperationException("Root YAML must contain a 'strategies' sequence.");

        if (strategiesNode is not YamlSequenceNode sequence)
            throw new InvalidOperationException("'strategies' must be a sequence.");

        var results = new List<StrategyBase>();
        var allFailures = new List<ValidationFailure>();

        foreach (var node in sequence.Children.OfType<YamlMappingNode>())
        {
            var strategyType = ReadRequiredScalar(node, "strategy_type");
            var nodeYaml = _serializer.Serialize(node);

            switch (strategyType)
            {
                case "signal":
                {
                    var parsed = _deserializer.Deserialize<SignalStrategyYaml>(nodeYaml);
                    var validation = _signalValidator.Validate(parsed);
                    if (!validation.IsValid)
                    {
                        allFailures.AddRange(validation.Errors);
                        continue;
                    }

                    results.Add(StrategyMapper.MapSignal(parsed));
                    break;
                }

                case "dca":
                {
                    var parsed = _deserializer.Deserialize<DcaStrategyYaml>(nodeYaml);
                    var validation = _dcaValidator.Validate(parsed);
                    if (!validation.IsValid)
                    {
                        allFailures.AddRange(validation.Errors);
                        continue;
                    }

                    results.Add(StrategyMapper.MapDca(parsed));
                    break;
                }

                case "grid":
                {
                    var parsed = _deserializer.Deserialize<GridStrategyYaml>(nodeYaml);
                    var validation = _gridValidator.Validate(parsed);
                    if (!validation.IsValid)
                    {
                        allFailures.AddRange(validation.Errors);
                        continue;
                    }

                    results.Add(StrategyMapper.MapGrid(parsed));
                    break;
                }

                default:
                    allFailures.Add(new ValidationFailure("strategy_type", $"Unknown strategy_type '{strategyType}'."));
                    break;
            }
        }

        if (allFailures.Count > 0)
            throw new StrategyValidationException(allFailures);

        EnsureUniqueStrategyIds(results);

        return results;
    }

    private static string ReadRequiredScalar(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var valueNode))
            throw new InvalidOperationException($"Strategy node is missing required key '{key}'.");

        if (valueNode is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            throw new InvalidOperationException($"Strategy key '{key}' must be a scalar string.");

        return scalar.Value!;
    }

    private static void EnsureUniqueStrategyIds(IReadOnlyList<StrategyBase> strategies)
    {
        var duplicates = strategies
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count == 0)
            return;

        var failures = duplicates
            .Select(id => new ValidationFailure("id", $"Duplicate strategy id '{id}'."))
            .ToList();

        throw new StrategyValidationException(failures);
    }
}

public sealed class StrategyValidationException : Exception
{
    public StrategyValidationException(IReadOnlyCollection<ValidationFailure> failures)
        : base(BuildMessage(failures))
    {
        Failures = failures;
    }

    public IReadOnlyCollection<ValidationFailure> Failures { get; }

    private static string BuildMessage(IReadOnlyCollection<ValidationFailure> failures)
        => "Strategy validation failed:" + Environment.NewLine
         + string.Join(Environment.NewLine, failures.Select(x => $"- {x.PropertyName}: {x.ErrorMessage}"));
}