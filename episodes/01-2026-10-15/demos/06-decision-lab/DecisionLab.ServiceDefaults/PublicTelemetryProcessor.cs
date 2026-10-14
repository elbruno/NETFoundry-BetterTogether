/*
 * Goal: Keep cloud resource identifiers out of the recorded trace walkthrough.
 * A. Inspect outgoing client spans before export.
 * B. Replace external host names and remove full request URLs.
 * C. Retain model, timing, status, and actual GenAI usage attributes.
 */
using System.Diagnostics;
using OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

public sealed class PublicTelemetryProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        if (activity.Kind != ActivityKind.Client) return;
        if (activity.GetTagItem("server.address") is string address &&
            address is not ("localhost" or "127.0.0.1" or "::1"))
        {
            activity.SetTag("server.address", "foundry");
            activity.SetTag("url.full", null);
        }
    }
}
