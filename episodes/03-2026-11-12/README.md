# Episode 3: Toolboxes and guardrails with .NET and Foundry

**Date:** November 12, 2026, 12:00-1:00 PM EST (17:00-18:00 UTC).

**Event and registration:** [Microsoft Reactor](https://developer.microsoft.com/en-us/reactor/events/27632/)

## Overview

Explore how C# discovers versioned Microsoft Foundry Toolbox capabilities and
how network egress guardrails can restrict a hosted agent's access. The two
examples are separate: the blog agent uses C# functions, not the Toolbox
discovered in the first demo.

## Recording

The recording link will be added after the episode.

## Demos and resources

1. [Discover Toolbox tools from C#](demos/01-toolbox-discovery/README.md): compare tools exposed by two Toolbox versions using MCP.
2. [Hosted agent network guardrails](demos/02-hosted-guardrails/README.md): compare two fixed blog tools with and without a hosted network policy.

Run the episode-level configuration script from this directory:

```powershell
.\setup.ps1
.\setup.ps1 -Validate
.\setup.ps1 -SelfTest
```

The script asks only for nonsecret resource bindings and stores them in each
demo's .NET user secrets. Validation and self-tests do not call cloud services.
See the demo READMEs for prerequisites, execution, costs, and cleanup.

Slides will be added when available.

[Back to the series](../../README.md)
