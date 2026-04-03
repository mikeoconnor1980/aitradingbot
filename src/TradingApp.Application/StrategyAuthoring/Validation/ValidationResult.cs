namespace TradingApp.Application.StrategyAuthoring.Validation;

public sealed class ValidationResult
{
    private readonly List<ValidationError> _all = [];

    public bool IsValid => !_all.Exists(error => error.Severity == ValidationSeverity.Error);

    public IReadOnlyList<ValidationError> All => _all;

    public IReadOnlyList<ValidationError> Errors =>
        _all.Where(error => error.Severity == ValidationSeverity.Error).ToList();

    public IReadOnlyList<ValidationError> Warnings =>
        _all.Where(error => error.Severity == ValidationSeverity.Warning).ToList();

    public IReadOnlyList<ValidationError> InfoMessages =>
        _all.Where(error => error.Severity == ValidationSeverity.Info).ToList();

    public void Add(ValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _all.Add(error);
    }

    public void AddRange(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _all.AddRange(errors);
    }
}