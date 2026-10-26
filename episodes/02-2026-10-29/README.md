# Episode 2: .NET + Foundry, Better together

**Date:** October 29, 2026, 12:00-1:00 PM EDT (16:00-17:00 UTC).

**Event and registration:** [Microsoft Reactor](https://developer.microsoft.com/en-us/reactor/events/27631/)

## Overview

Build a C# support-routing agent with Microsoft Agent Framework. The agent
uses ElBruno.AI.Decisions.Foundry to call Microsoft-Decision-1, then runs as a
Microsoft Foundry Hosted Agent.

The example uses a synthetic support ticket and returns a bounded team
suggestion. It does not update a ticketing system. The session explains both
identity hops: the caller to the hosted endpoint, and the hosted runtime's
agent-instance identity to the decision-model resource. Authentication
identifies the caller; Azure RBAC authorizes the identity to access Azure
resources. This is distinct from assigning Microsoft Entra directory roles or
Microsoft Graph permissions.

## Recording

The recording link will be added after the episode.

## Demos and resources

1. [Hosted decision-agent demo](demos/01-hosted-decision-agent/README.md): A
   feasibility prototype with one successful keyless hosted request. Repeatable
   deployment instructions, a remote C# caller, and live evaluation fixtures
   remain incomplete; the current Foundry User Azure RBAC assignment is broad
   and is not a least-privilege recommendation.

[Back to the series](../../README.md)
