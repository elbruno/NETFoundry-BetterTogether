# Episode 2: .NET + Foundry, Better together

**Date:** October 29, 2026, 12:00-1:00 PM EDT (16:00-17:00 UTC).

**Event and registration:** [Microsoft Reactor](https://developer.microsoft.com/en-us/reactor/events/27631/)

## Hosts

- [Bruno Capuano](https://aka.ms/elbruno)
- [Jose Luis Latorre](https://www.linkedin.com/in/joslat/), co-speaker

## Overview

Build a C# support-routing agent with Microsoft Agent Framework that calls a
decision model in Microsoft Foundry, then deploy it as a Foundry Hosted Agent.
Jose Luis Latorre joins as co-speaker for a
Foundry-focused discussion of Foundry Memory Stores, custom AgentMemory,
AgentEval, and hybrid evaluation.

GraphBridge and GraphRAG may be included if ready and Foundry-aligned. Any
custom-memory integration is exploratory and is not part of the current
support-routing prototype; its integration path has not been verified. Jose's
repositories and resources will be added when shared and reviewed.

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

## Guest demos and resources

Jose's repositories and related resources are pending and will be added after
they are shared and reviewed.

[Back to the series](../../README.md)
