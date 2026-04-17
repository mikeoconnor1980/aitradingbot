namespace TradePilot.Api.Models;

public sealed class TestSignResponse
{
    public string DomainSeparator { get; set; } = default!;
    public string TypeHash { get; set; } = default!;
    public string MessageHash { get; set; } = default!;
    public SignatureDto Signature { get; set; } = default!;
}

public sealed class SignatureDto
{
    public int V { get; set; }
    public string R { get; set; } = default!;
    public string S { get; set; } = default!;
}
