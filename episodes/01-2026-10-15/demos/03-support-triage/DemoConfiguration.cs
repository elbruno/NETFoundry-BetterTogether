/*
 * Goal: Load this standalone demo's external Foundry configuration safely.
 * A. Combine project-specific user secrets and environment overrides.
 * B. Validate a native resource URL and deployment name.
 * C. Return bounded community SDK options using CLI identity by default.
 * Inputs: Foundry:Endpoint, Foundry:Model, optional Foundry:ApiKey.
 * Output: Options, never resolved settings in console output.
 * Boundary: No cloud provisioning or accidental reuse of another demo's secrets ID.
 */
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;

namespace SupportTriage;

public static class DemoConfiguration
{
    public static FoundryDecisionOptions Load()
    {
        // A. Environment variables use double underscores for nested keys.
        IConfiguration config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DemoConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables().Build();
        return Read(config);
    }

    public static FoundryDecisionOptions Read(IConfiguration config)
    {
        // B. Use the deployment name, not necessarily the catalog name.
        string model = config["Foundry:Model"] ?? "";
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Set Foundry:Model in user secrets or Foundry__Model.");
        if (!Uri.TryCreate(config["Foundry:Endpoint"], UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != "https" || endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.AbsolutePath is not ("/" or "/providers/microsoft/v1/systemone"))
            throw new ArgumentException("Foundry:Endpoint must be an HTTPS resource root or native SystemOne URL without credentials, query or fragment.");

        // C. A supplied API key takes precedence; otherwise the SDK uses AzureCliCredential.
        return new FoundryDecisionOptions
        {
            Endpoint = endpoint, Model = model, ApiKey = config["Foundry:ApiKey"],
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
