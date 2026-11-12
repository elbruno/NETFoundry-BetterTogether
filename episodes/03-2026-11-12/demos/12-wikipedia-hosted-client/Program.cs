// Goal: Call one existing hosted Wikipedia agent from a local MAF console.
// A: Load the target name; Azure CLI authenticates locally.
// B: Connect to its hosted Responses endpoint, which owns the tools and identity.
// C: Ask one editable public question and print the remote response.
using System.Reflection;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables().Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("Set FOUNDRY_PROJECT_ENDPOINT.");
var agentName = configuration["FOUNDRY_AGENT_NAME"] ?? "wikipedia-01";
var project = new AIProjectClient(new Uri(endpoint), new AzureCliCredential());
var agentEndpoint = new Uri($"{endpoint.TrimEnd('/')}/agents/{Uri.EscapeDataString(agentName)}/endpoint/protocols/openai");
var agent = project.AsAIAgent(agentEndpoint);
const string question = "Use Wikipedia to find who founded Wikipedia and in which year. Cite the article.";
Console.WriteLine($"Hosted agent: {agentName}");
Console.WriteLine($"Question: {question}");
Console.WriteLine(await agent.RunAsync(question));
