# Episode 2: .NET + Foundry, Better Together: Decision Models in Action

**Date:** October 29, 2026, 12:00-1:00 PM EDT (16:00-17:00 UTC).

**Event and registration:** [Microsoft Reactor](https://developer.microsoft.com/en-us/reactor/events/27631/)

## Hosts

- [Bruno Capuano](https://aka.ms/elbruno)
- [Jose Luis Latorre](https://www.linkedin.com/in/joslat/), co-speaker

## Overview

This episode focuses on two topics: Hosted Agents using decision models and
agent memory. The support-routing demo progresses through three stages: a C#
console app calls a decision model in Microsoft Foundry, a local Microsoft
Agent Framework agent calls the same model, and a Foundry Hosted Agent runs
that agent. Co-speaker Jose Luis Latorre joins to compare Foundry Memory Stores
with custom AgentMemory.

Using custom AgentMemory from a Foundry Hosted Agent is exploratory, is not
part of the current support-routing prototype, and has not been verified.
AgentEval and hybrid evaluation are possible related discussion if time allows.
GraphBridge and GraphRAG may be mentioned only if ready and Foundry-aligned.
Jose's repositories and resources will be added when shared and reviewed.

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

1. [Call a Foundry decision model from C#](demos/01-console-decision-model/README.md):
   a console app makes a direct, structured decision-model call.
2. [Run the decision inside a local agent](demos/02-local-agent/README.md):
   a console app invokes a Microsoft Agent Framework agent in-process.
3. [Run the agent as a Foundry Hosted Agent](demos/03-foundry-hosted-agent/README.md):
   a feasibility prototype with one successful keyless hosted request.
   Repeatable deployment instructions, a remote C# caller, and live evaluation
   fixtures remain incomplete; the current Foundry User Azure RBAC assignment
   is broad and is not a least-privilege recommendation.

## Guest demos and resources

Jose's repositories and related resources are pending and will be added after
they are shared and reviewed.

[Back to the series](../../README.md)
