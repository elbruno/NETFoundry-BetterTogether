# Episode 3: Toolboxes and guardrails with .NET and Foundry

**Date:** November 12, 2026, 12:00-1:00 PM EST (17:00-18:00 UTC).

**Event and registration:** [Microsoft Reactor](https://developer.microsoft.com/en-us/reactor/events/27632/)

## Overview

Explore Toolbox discovery, MCP tools, hosted agents, and Foundry guardrails
with C#. The EBSearch, Wikipedia, and Microsoft Learn examples progress from
local agents to hosted agents and local clients for those hosted agents.

## Recording

The recording link will be added after the episode.

## Demos and resources

1. [Discover Toolbox tools from C#](demos/01-toolbox-discovery/README.md): compare tools exposed by two Toolbox versions using MCP.
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
14. [Wikipedia blocklist observer](demos/05-wikipedia-blocklist-console/README.md): observe Foundry prompt and completion filtering without local word filtering.

Each MCP family follows the same six stages: a local `dotnet run` console, a
local `azd ai agent run` Responses host, three separately named deployments
of the same hosted image, and a local console that calls one hosted agent.
Local applications use `AzureCliCredential`; deployed containers use
`DefaultAzureCredential` with an agent-instance managed identity.

The Toolbox discovery example and EBSearch MCP server are separate. Configure
guardrails in Foundry and inspect actual policy evidence before claiming that
a request was blocked. Model-deployment content filters apply to all
consumers of that deployment. The Wikipedia MCP endpoint is a community
service, not a Wikimedia service; use public questions only.

Run the episode-level configuration script from this directory:

```powershell
.\setup.ps1
.\setup.ps1 -Validate
.\setup.ps1 -SelfTest
```

The script asks only for nonsecret resource bindings, displays available
defaults, and stores them in all 14 projects' .NET user secrets. Environment
variables override saved settings. Validation checks configuration formats
without writing; self-tests are offline. Neither checks cloud permissions.
For local cleanup, run `dotnet user-secrets clear --project <demo-project>`
for each demo you configured. See the demo READMEs for prerequisites, exact
commands, cloud costs, and hosted-session cleanup.

Slides will be added when available.

[Back to the series](../../README.md)
