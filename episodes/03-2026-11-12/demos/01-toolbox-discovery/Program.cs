// Purpose: Compare MCP tool discovery for two existing Toolbox versions.
// Requires a prepared TOOLBOX_ENDPOINT and an already-authorized Azure CLI identity.
// Contract: https://learn.microsoft.com/azure/foundry/agents/how-to/tools/toolbox
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using ModelContextProtocol.Client;

// Step 1: Start with the consumer endpoint or any version-specific endpoint.
var endpoint = new Uri(DemoEnvironment.Read("TOOLBOX_ENDPOINT"));
if (endpoint.Scheme != Uri.UriSchemeHttps
    || !endpoint.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)
    || !endpoint.AbsolutePath.Contains("/toolboxes/", StringComparison.Ordinal)
    || !endpoint.AbsolutePath.EndsWith("/mcp", StringComparison.Ordinal)
    || endpoint.Query != "?api-version=v1")
    throw new InvalidOperationException("TOOLBOX_ENDPOINT must be a Foundry toolbox MCP HTTPS URL with ?api-version=v1.");

// Step 2: Change these Toolbox versions to compare other existing versions.
// The endpoint's api-version=v1 is the API version, not the Toolbox version.
var firstToolboxVersion = "1";
var secondToolboxVersion = "3";
Console.WriteLine($"Toolbox versions: {firstToolboxVersion}, {secondToolboxVersion}");

// Remove an existing version suffix before building each version-specific endpoint.
var consumerPath = Regex.Replace(endpoint.AbsolutePath,
    @"(/toolboxes/[^/]+)/versions/[^/]+/mcp$", "$1/mcp", RegexOptions.IgnoreCase);
var toolboxPath = consumerPath[..^"/mcp".Length];

// Step 3: Discover each version without changing the Toolbox or its default.
var firstCount = await DiscoverToolsAsync(firstToolboxVersion);
var secondCount = await DiscoverToolsAsync(secondToolboxVersion);
Console.WriteLine($"\nMCP tools/list count: version {firstToolboxVersion}: {firstCount}; version {secondToolboxVersion}: {secondCount} (change: {secondCount - firstCount:+#;-#;0}).");

async Task<int> DiscoverToolsAsync(string version)
{
    var versionEndpoint = new UriBuilder(endpoint)
    {
        Path = $"{toolboxPath}/versions/{Uri.EscapeDataString(version)}/mcp"
    }.Uri;
    using var httpClient = new HttpClient(new ToolboxAuthenticationHandler());
    await using var transport = new HttpClientTransport(new()
    {
        Name = "Existing Foundry Toolbox",
        Endpoint = versionEndpoint,
        TransportMode = HttpTransportMode.StreamableHttp,
        EnableStandaloneGetStream = false,
        MaxReconnectionAttempts = 0
    }, httpClient);
    await using var client = await McpClient.CreateAsync(transport);

    // Discovery only. Authentication, HTTP and consent errors remain visible.
    var tools = await client.ListToolsAsync();
    Console.WriteLine($"\nVersion {version}: {tools.Count} tools exposed by MCP tools/list");
    foreach (var tool in tools)
    {
        Console.WriteLine($"{tool.Name}: {tool.Description}");
        Console.WriteLine($"Input schema: {tool.JsonSchema}");
    }

    return tools.Count;
}

// Refresh the scoped bearer token per request. No browser consent or retry loop.
sealed class ToolboxAuthenticationHandler : DelegatingHandler
{
    private readonly AzureCliCredential credential = new();

    public ToolboxAuthenticationHandler()
        : base(new HttpClientHandler { AllowAutoRedirect = false })
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://ai.azure.com/.default"]), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await base.SendAsync(request, cancellationToken);
    }
}
