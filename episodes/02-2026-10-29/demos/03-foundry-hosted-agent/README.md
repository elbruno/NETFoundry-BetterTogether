# Demo 03: Run the agent as a Foundry Hosted Agent

The final step moves the Microsoft Agent Framework support-routing agent into
the Foundry Hosted Agent runtime. The agent still calls the same native Foundry
decision model, but its outbound request uses the hosted agent-instance
identity. A remote caller and the agent runtime are separate authentication and
authorization hops.

The hosted implementation already has a published path at
[`01-hosted-decision-agent`](../01-hosted-decision-agent/README.md). That path
is retained for compatibility with existing links; it contains the hosted
agent's source, manifest, tests, and current feasibility status. Read its
prerequisites and status before trying it.

## What this stage demonstrates

- A custom `AIAgent` is exposed through the Foundry Responses protocol.
- The agent calls the same decision-model deployment used in Demos 01 and 02.
- The hosted runtime uses an agent-instance identity for the outbound model
  call; authentication alone does not grant Azure resource access.
- The result remains a bounded suggestion. It does not assign a real ticket.

## Readiness and security

The current hosted sample is a feasibility prototype. A keyless hosted request
has succeeded in its existing environment, but the public project does not yet
provide reproducible deployment instructions, a remote C# caller, or live
evaluation fixtures. Do not treat it as a turnkey deployment or as proof that
the broad role used in that environment is least privilege.

For code, tested package versions, local run instructions, test coverage,
permission boundaries, and cleanup, see the
[hosted-agent prototype README](../01-hosted-decision-agent/README.md). Do not
deploy to a shared Foundry project or change role assignments as part of this
walkthrough.

Return to [Demo 01](../01-console-decision-model/README.md) or
[Demo 02](../02-local-agent/README.md) to compare the earlier stages.
