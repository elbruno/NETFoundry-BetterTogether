/*
 * Goal: Load external Foundry settings without printing machine-specific values.
 * A. Combine this project's user secrets and environment variables.
 * B. Validate the resource root or native invocation URL and deployment name.
 * C. Create community SDK options, preferring Azure CLI identity by default.
 * Inputs: Foundry:Endpoint, Foundry:Model, optional Foundry:ApiKey.
 * Output: Validated options. Boundary: No resources are created or secrets logged.
 */
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;

namespace HelloDecisions;

public static class DemoConfiguration
{
    public static FoundryDecisionOptions Load()
    {
        // A. Environment variables (Foundry__Endpoint, etc.) override user secrets.
        IConfiguration config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DemoConfiguration).Assembly, optional: true)
            .AddEnvironmentVariables().Build();
        return Read(config);
    }

    public static FoundryDecisionOptions Read(IConfiguration config)
    {
        // B. A deployment name can differ from the Microsoft-Decision-1 catalog name.
        string model = config["Foundry:Model"] ?? "";
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Set Foundry:Model in user secrets or Foundry__Model.");
        if (!Uri.TryCreate(config["Foundry:Endpoint"], UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != "https" || endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.AbsolutePath is not ("/" or "/providers/microsoft/v1/systemone"))
            throw new ArgumentException("Foundry:Endpoint must be an HTTPS resource root or /providers/microsoft/v1/systemone URL, without credentials, query or fragment.");

        // C. The pinned community SDK uses AzureCliCredential when no API key is supplied.
        return new FoundryDecisionOptions
        {
            Endpoint = endpoint, Model = model, ApiKey = config["Foundry:ApiKey"],
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
