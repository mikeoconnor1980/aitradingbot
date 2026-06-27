using System.Text.Json;

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

if (args.Length == 0 || args.Any(static arg => arg is "--help" or "-h"))
{
    PrintUsage();
    return;
}

var options = CliOptions.Parse(args);
if (string.IsNullOrWhiteSpace(options.VaultName))
{
    Console.Error.WriteLine("Missing required argument: --vault-name <name>");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
}

IReadOnlyList<SecretSpec> secretSpecs;
if (!string.IsNullOrWhiteSpace(options.ConfigPath))
{
    secretSpecs = await LoadSecretSpecsAsync(options.ConfigPath);
}
else
{
    secretSpecs = options.SecretNames.Count > 0
        ? options.SecretNames.Select(SecretSpec.CreateDefault).ToArray()
        : SecretSpec.DefaultGitHubDevSecrets;
}

var credential = new DefaultAzureCredential();
var secretClient = new SecretClient(new Uri($"https://{options.VaultName}.vault.azure.net/"), credential);

var uploaded = 0;
var skipped = 0;

foreach (var secretSpec in secretSpecs)
{
    var value = Environment.GetEnvironmentVariable(secretSpec.SourceName);
    if (string.IsNullOrWhiteSpace(value) && options.PromptMissing)
    {
        value = PromptForSecret(secretSpec);
    }

    if (string.IsNullOrWhiteSpace(value))
    {
        Console.WriteLine($"Skipping {secretSpec.SourceName}: no value supplied.");
        skipped++;
        continue;
    }

    if (options.DryRun)
    {
        Console.WriteLine($"Would upload {secretSpec.SourceName} -> {secretSpec.KeyVaultName}");
        uploaded++;
        continue;
    }

    await secretClient.SetSecretAsync(secretSpec.KeyVaultName, value);
    Console.WriteLine($"Uploaded {secretSpec.SourceName} -> {secretSpec.KeyVaultName}");
    uploaded++;
}

Console.WriteLine($"Completed. Uploaded: {uploaded}, skipped: {skipped}.");

static async Task<IReadOnlyList<SecretSpec>> LoadSecretSpecsAsync(string configPath)
{
    await using var stream = File.OpenRead(configPath);
    var items = await JsonSerializer.DeserializeAsync<List<SecretSpecFileModel>>(stream, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (items is null || items.Count == 0)
    {
        throw new InvalidOperationException("The config file did not contain any secret definitions.");
    }

    return items.Select(SecretSpec.FromFileModel).ToArray();
}

static string PromptForSecret(SecretSpec secretSpec)
{
    Console.Write($"Enter value for {secretSpec.SourceName}");
    if (!string.Equals(secretSpec.SourceName, secretSpec.KeyVaultName, StringComparison.Ordinal))
    {
        Console.Write($" (Key Vault: {secretSpec.KeyVaultName})");
    }

    Console.Write(": ");

    var buffer = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (buffer.Count == 0)
            {
                continue;
            }

            buffer.RemoveAt(buffer.Count - 1);
            continue;
        }

        buffer.Add(key.KeyChar);
    }

    return new string(buffer.ToArray());
}

static void PrintUsage()
{
    Console.WriteLine("KeyVaultSecretUploader");
    Console.WriteLine();
    Console.WriteLine("Uploads secret values into Azure Key Vault from environment variables or interactive prompts.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project tmp-tools/KeyVaultSecretUploader.csproj -- --vault-name <vault-name> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --vault-name <name>         Required. Azure Key Vault name without the FQDN.");
    Console.WriteLine("  --secret <NAME>             Upload one source secret name. Repeat for multiple values.");
    Console.WriteLine("  --config <path>             JSON file describing source/key-vault name mappings.");
    Console.WriteLine("  --prompt-missing            Prompt for any missing values instead of skipping them.");
    Console.WriteLine("  --dry-run                   Show which secrets would be uploaded without writing them.");
    Console.WriteLine();
    Console.WriteLine("If neither --secret nor --config is supplied, the tool uses the current GitHub dev secret names:");
    Console.WriteLine($"  {string.Join(", ", SecretSpec.DefaultGitHubDevSecrets.Select(static secret => secret.SourceName))}");
    Console.WriteLine();
    Console.WriteLine("Key Vault secret names are normalized to lower-case hyphenated names by default.");
    Console.WriteLine("Example: AZURE_SUBSCRIPTION_ID -> azure-subscription-id");
}

internal sealed record CliOptions(
    string? VaultName,
    string? ConfigPath,
    bool PromptMissing,
    bool DryRun,
    IReadOnlyList<string> SecretNames)
{
    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        string? vaultName = null;
        string? configPath = null;
        var promptMissing = false;
        var dryRun = false;
        var secretNames = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--vault-name":
                    vaultName = ReadValue(args, ref i, "--vault-name");
                    break;
                case "--config":
                    configPath = ReadValue(args, ref i, "--config");
                    break;
                case "--secret":
                    secretNames.Add(ReadValue(args, ref i, "--secret"));
                    break;
                case "--prompt-missing":
                    promptMissing = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument: {args[i]}");
            }
        }

        return new CliOptions(vaultName, configPath, promptMissing, dryRun, secretNames);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
        {
            throw new InvalidOperationException($"Missing value for {optionName}");
        }

        index++;
        return args[index];
    }
}

internal sealed record SecretSpec(string SourceName, string KeyVaultName)
{
    public static readonly IReadOnlyList<SecretSpec> DefaultGitHubDevSecrets =
    [
        CreateDefault("API_FQDN"),
        CreateDefault("AZURE_CLIENT_ID"),
        CreateDefault("AZURE_SUBSCRIPTION_ID"),
        CreateDefault("AZURE_TENANT_ID"),
        CreateDefault("GHCR_PAT"),
        CreateDefault("JWT_SECRET_KEY"),
        CreateDefault("LLM_API_KEY"),
        CreateDefault("SQL_ADMIN_LOGIN"),
        CreateDefault("SQL_ADMIN_PASSWORD"),
        CreateDefault("SWA_DEPLOYMENT_TOKEN"),
        CreateDefault("SWA_URL")
    ];

    public static SecretSpec CreateDefault(string sourceName)
    {
        return new SecretSpec(sourceName, NormalizeKeyVaultName(sourceName));
    }

    public static SecretSpec FromFileModel(SecretSpecFileModel model)
    {
        if (string.IsNullOrWhiteSpace(model.SourceName))
        {
            throw new InvalidOperationException("Each config entry must include sourceName.");
        }

        return new SecretSpec(
            model.SourceName,
            string.IsNullOrWhiteSpace(model.KeyVaultName)
                ? NormalizeKeyVaultName(model.SourceName)
                : model.KeyVaultName);
    }

    private static string NormalizeKeyVaultName(string sourceName)
    {
        return sourceName.Trim().ToLowerInvariant().Replace("_", "-");
    }
}

internal sealed record SecretSpecFileModel(string SourceName, string? KeyVaultName);