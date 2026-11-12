# Episode 3: Guardrails with .NET and Foundry

**Date:** November 12, 2026, 12:00-1:00 PM EST (17:00-18:00 UTC).

**Event and registration:** [.NET + Foundry, better together](https://developer.microsoft.com/en-us/reactor/events/27632/)

## Overview

This episode focuses on Foundry guardrails: compare a model in the playground
before and after applying a Wikipedia blocklist, then explore hosted-agent
content controls and network egress with C#. The EBSearch, Wikipedia, and
Microsoft Learn examples use MCP tools as agent workloads.

## First demo: baseline and blocklist

Deploy a dedicated Azure OpenAI chat model and ask it Wikipedia questions in
the playground. Add an existing or new Wikipedia blocklist to a prompt content
filter, associate that filter with the dedicated deployment, then repeat the
same questions in a fresh chat. Show the actual filtering result; the
[blocklist demo guide](demos/05-wikipedia-blocklist-console/README.md) includes
copy-ready prompts, the C# observer, setup, and troubleshooting.

## Recording

The recording link will be added after the episode.

## Demos and resources

1. [Wikipedia blocklist observer](demos/05-wikipedia-blocklist-console/README.md): compare playground results before and after a prompt blocklist, then inspect service filtering from C#.
2. [EBSearch local MCP console](demos/09-ebsearch-mcp-console/README.md): call two fixed-feed blog tools from a local agent.
3. [EBSearch local Responses host](demos/02-hosted-guardrails/README.md): run the same MCP behavior with the local Agent Inspector.
4. [EBSearch hosted agents](demos/03-hosted-guardrails/README.md): deploy the same image as `ebsearch-01`, `ebsearch-02`, and `ebsearch-03`.
5. [EBSearch hosted-agent client](demos/11-ebsearch-hosted-client/README.md): call a selected hosted agent from a local MAF console.
6. [Wikipedia local MCP console](demos/03-wikipedia-mcp-console/README.md): ask two public questions using community Wikipedia MCP tools.
7. [Wikipedia local Responses host](demos/06-wikipedia-local-host/README.md): run the Wikipedia agent locally with the Agent Inspector.
8. [Wikipedia hosted agents](demos/04-wikipedia-hosted/README.md): deploy the same image as `wikipedia-01`, `wikipedia-02`, and `wikipedia-03`.
9. [Wikipedia hosted-agent client](demos/12-wikipedia-hosted-client/README.md): call a selected hosted agent from a local MAF console.
10. [Microsoft Learn local MCP console](demos/10-mslearn-mcp-console/README.md): search official documentation and code samples.
11. [Microsoft Learn local Responses host](demos/07-mslearn-local-host/README.md): run the Learn agent locally with the Agent Inspector.
12. [Microsoft Learn hosted agents](demos/08-mslearn-hosted/README.md): deploy the same image as `mslearn-01`, `mslearn-02`, and `mslearn-03`.
13. [Microsoft Learn hosted-agent client](demos/13-mslearn-hosted-client/README.md): call a selected hosted agent from a local MAF console.

Each MCP family follows the same six stages: a local `dotnet run` console, a
local `azd ai agent run` Responses host, three separately named deployments
of the same hosted image, and a local console that calls one hosted agent.
Local applications use `AzureCliCredential`; deployed containers use
`DefaultAzureCredential` with an agent-instance managed identity.

Configure guardrails in Foundry and inspect actual policy evidence before claiming that
a request was blocked. Model-deployment content filters apply to all
consumers of that deployment. The Wikipedia MCP endpoint is a community
service, not a Wikimedia service; use public questions only. Agent
tool-call/tool-response moderation supports only documented tool types and
does not currently list arbitrary MCP servers.

Run the episode-level configuration script from this directory:

```powershell
.\setup.ps1
.\setup.ps1 -Validate
.\setup.ps1 -SelfTest
```

The script asks only for nonsecret resource bindings, displays available
defaults, and stores them in all 13 active projects' .NET user secrets. Environment
variables override saved settings. Validation checks configuration formats
without writing, including that the blocklist demo uses a separate model
deployment. It cannot confirm that a deployment exists or that a guardrail is
assigned. Self-tests are offline and neither mode checks cloud permissions.
For local cleanup, run `dotnet user-secrets clear --project <demo-project>`
for each demo you configured. See the demo READMEs for prerequisites, exact
commands, cloud costs, and hosted-session cleanup.

The [guardrail intervention-points diagram](assets/guardrails/01-guardrail-intervention-points.png)
illustrates content checks before input, supported tool calls, supported tool
responses, and output.

Slides will be added when available.

[Back to the series](../../README.md)
