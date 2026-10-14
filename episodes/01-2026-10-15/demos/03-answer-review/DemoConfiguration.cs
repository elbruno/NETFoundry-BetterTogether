/*
 * Goal: Configure answer review without embedding deployment settings.
 * A. Read this demo's user secrets and environment overrides.
 * B. Validate the native Foundry URL and deployment name.
 * C. Build bounded community SDK options with CLI identity by default.
 * Inputs: Foundry:Endpoint, Foundry:Model, optional Foundry:ApiKey.
 * Output: Validated options, never credentials or machine settings.
 * Boundary: No infrastructure is created and no answer is published.
 */
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;

namespace AnswerReview;

public static class DemoConfiguration
{
    public static FoundryDecisionOptions Load()
    {
        // A. Environment variables override this project's unique secrets store.
        IConfiguration config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DemoConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables().Build();
        return Read(config);
    }

    public static FoundryDecisionOptions Read(IConfiguration config)
    {
        // B. The model setting is a deployment name, not necessarily a catalog name.
        string model = config["Foundry:Model"] ?? "";
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Set Foundry:Model in user secrets or Foundry__Model.");
        if (!Uri.TryCreate(config["Foundry:Endpoint"], UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != "https" || endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.AbsolutePath is not ("/" or "/providers/microsoft/v1/systemone"))
            throw new ArgumentException("Foundry:Endpoint must be an HTTPS resource root or native SystemOne URL without credentials, query or fragment.");

        // C. A supplied key takes precedence; otherwise the SDK uses AzureCliCredential.
        return new FoundryDecisionOptions
        {
            Endpoint = endpoint, Model = model, ApiKey = config["Foundry:ApiKey"],
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
