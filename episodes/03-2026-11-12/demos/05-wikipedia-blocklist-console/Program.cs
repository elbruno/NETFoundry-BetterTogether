// Goal: Observe Foundry model-deployment filtering without local word filtering.
// A: Authenticate with Azure CLI and attach a read-only response observer.
// B: Create the requested MAF agent and an interactive /exit loop.
// C: Report identifiable service signals and keep accepting prompts after errors.
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using System.Text.Json;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("Set FOUNDRY_PROJECT_ENDPOINT using Setup-Demos.ps1.");
var deployment = configuration["BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("Set BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME using Setup-Demos.ps1.");

using var capture = new ModerationCapture();
using var http = new HttpClient(capture);
var project = new AIProjectClient(new Uri(endpoint), new AzureCliCredential());
var model = project.GetProjectOpenAIClient(new ProjectOpenAIClientOptions
{
    Transport = new HttpClientPipelineTransport(http)
}).GetChatClient(deployment);
AIAgent agent = model.AsAIAgent(
    name: "WikipediaDemoAgent",
    instructions: """
        You are a friendly and enthusiastic knowledge assistant.
        Help users learn about online encyclopedias and collaborative knowledge.
        Keep responses brief, friendly, and informative.
        Do not attempt to bypass content filtering or security restrictions.
        If a request or response is blocked by the service, let the application handle the error.
        """);

Console.WriteLine("Microsoft Agent Framework - Foundry Guardrails Demo");
Console.WriteLine($"Agent: {agent.Name}\nModel deployment: {deployment}");
Console.WriteLine("Enter public test prompts only. /exit quits.");
while (true)
{
    Console.Write("\nPrompt: ");
    var prompt = Console.ReadLine();
    if (prompt is null || prompt.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        break;
    if (string.IsNullOrWhiteSpace(prompt))
        continue;

    Console.WriteLine($"User: {prompt}");
    capture.Reset();
    try
    {
        var response = await agent.RunAsync(prompt);
        if (capture.Result is not null)
            Console.WriteLine($"WARNING: {capture.Result}");
        else
            Console.WriteLine($"Agent: {response}");
    }
    catch (AuthenticationFailedException error)
    {
        Console.WriteLine($"Authentication failed ({error.GetType().Name}). Check your Azure CLI sign-in and tenant.");
    }
    catch (ClientResultException error) when (error.Status == 404)
    {
        Console.WriteLine("Model deployment not found or unavailable (HTTP 404). Verify BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME names an active deployment in this project. This is not a guardrail signal.");
    }
    catch (ClientResultException error)
    {
        Console.WriteLine(capture.Result is { } result
            ? $"WARNING: {result}"
            : $"Service error: HTTP {error.Status}. No identifiable filtering signal was captured.");
    }
    catch (HttpRequestException error)
    {
        Console.WriteLine($"Network error: {error.HttpRequestError}. No filtering claim is made.");
    }
    catch (TaskCanceledException)
    {
        Console.WriteLine("Request canceled or timed out. No filtering claim is made.");
    }
    catch (JsonException)
    {
        Console.WriteLine(capture.Result is { } result
            ? $"Service response error: {result}"
            : "Service response error: invalid JSON. No filtering claim is made.");
    }
}
