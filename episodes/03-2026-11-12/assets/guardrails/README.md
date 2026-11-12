# Foundry guardrail diagrams

This original learning diagram was checked against official Microsoft Learn documentation on November 12, 2026. It is a conceptual illustration, not deployment evidence or a screenshot of the portal.

| Image | Purpose |
| --- | --- |
| [01: Intervention points](01-guardrail-intervention-points.png) | Follow input, supported tool calls, supported tool responses, and final output. |

Use the 2400 x 1350 PNG directly in PowerPoint. The matching SVG provides a vector image. The matching `.excalidraw` file preserves editable diagram elements and can be opened at https://aka.ms/excalidraw. All labels are original, with no private resource bindings.

## Accuracy notes

- A guardrail is a named collection of controls. Each control identifies a risk, an intervention point, and an action. It is not a guarantee of correct or harmless behavior.
- Model deployment filters screen model prompts and completions, including calls made by locally running applications. Tool-call and tool-response intervention points are agent features, not model-deployment features.
- Agent guardrails are Preview and apply to agents developed in Foundry Agent Service, not automatically to every agent registered in the control plane.
- Tool-call and tool-response moderation requires supported tools. The current documented list is Azure AI Search, Azure Functions, OpenAPI, SharePoint Grounding, Fabric Data Agent, Bing Grounding, Bing Custom Search, and Browser Automation. Arbitrary MCP servers are not on that list. Do not claim that Wikipedia MCP, Learn MCP, or custom in-process tools are automatically scanned at these checkpoints.
- The general guardrails overview describes model-policy inheritance when an agent has no custom policy, and full override when an agent has an explicit policy. The hosted-agent-specific documentation describes a separate boundary: omitting `rai_config` means no hosted-agent-boundary content safety policy; including it without a policy name selects `Microsoft.DefaultV2`. Hosted boundary filtering is independent of model-call filtering. Do not combine these into a universal "all policies stack" or "all agents inherit" claim.
- Hosted Responses moderation uses the assigned policy. Invocations also requires correct moderation paths/selectors. Invocations WebSocket content moderation is unavailable. Check current limits, protocol, region, and supported tools.
- Diagram 01 illustrates default synchronous output screening. Asynchronous filtering can release text before a later violation is detected. A streamed block may arrive as a terminal content-filter event after HTTP 200.
- Network egress is a hosted-agent-only Preview feature. Rules are ordered, with the first match winning and a default action if none matches. Required platform domains are automatically allowed. Audit logs would-deny traffic without blocking it; Enforce applies denials. Transform and Rewrite actions still operate in Audit mode.
- Content controls, egress policies, tool authorization, identity, DLP, application validation, and human approval protect different boundaries. None substitutes for all the others.
- Confirm that a referenced policy exists, then test actual enforcement. A hosted agent referencing a nonexistent policy can become active without content filtering. These images do not establish that any episode demo policy was assigned or tested.

## Official sources

1. [Guardrails and controls overview](https://learn.microsoft.com/azure/foundry/guardrails/guardrails-overview): definition, model/agent applicability, inheritance, override, and actions.
2. [Intervention points](https://learn.microsoft.com/azure/foundry/guardrails/intervention-points): four checkpoints, block behavior, and supported tools.
3. [Hosted agent content safety overview](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agent-guardrails): hosted-boundary policy semantics, protocols, streaming, and limitations.
4. [Add guardrails to a hosted agent](https://learn.microsoft.com/azure/foundry/agents/how-to/add-hosted-agent-guardrails#network-egress-controls-preview): attachment, policy verification, egress rules, and enforcement modes.

Recheck Preview behavior before presenting. The image intentionally omits fixed latency estimates, unsupported safety guarantees, and invented portal screens.
