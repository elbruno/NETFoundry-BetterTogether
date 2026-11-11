# Discover Toolbox tools from C#

Compare the names, descriptions and schemas exposed by two existing Microsoft
Foundry Toolbox versions over MCP. This sample only calls `tools/list`: it does
not invoke a tool, change a Toolbox, or promote a version.

## Prerequisites

- .NET SDK 11.0.100 (later 11.0.1xx patches are allowed); PowerShell 7.
- Azure CLI signed in to an identity authorized to read an existing Toolbox.
- Two existing Toolbox versions and their HTTPS MCP endpoint. Edit the two
  version strings in `Program.cs` to match your Toolbox.
- Direct packages: Azure.Identity 1.21.0, ModelContextProtocol.Core 2.2.0 and
  Microsoft.Extensions.Configuration.UserSecrets 11.0.0. The lockfile pins
  transitive packages.

The SDK and package versions were used in the original sample; this episode's
adaptation must be rehearsed against your own environment. The authenticated
discovery call can use billable cloud resources; check your pricing and quotas.

## Configuration

From the episode directory, run `.\setup.ps1` and enter the Toolbox endpoint,
Foundry project endpoint and model deployment name. The latter two configure
the other demo. The script writes nonsecret values to .NET user secrets, which
are unencrypted local development storage. Do not enter credentials.
Alternatively set `TOOLBOX_ENDPOINT` as an environment variable. Environment
variables override user secrets, including empty values.

Run `.\setup.ps1 -Validate` to check configuration format without a cloud call
or changing configuration. `.\setup.ps1 -SelfTest` runs offline validation tests.
Neither checks authorization or whether the chosen versions exist.

## Run

From this demo directory:

```powershell
dotnet restore .\ToolboxDiscovery.csproj --locked-mode
dotnet run --project .\ToolboxDiscovery.csproj --no-restore
```

The program compares versions `1` and `3` by default; change them to two
versions that exist in your Toolbox before running. The endpoint must include
`?api-version=v1`; that is the API version, not the Toolbox version.

## Expected result and troubleshooting

For each version, the console prints discovered tool names, descriptions and
input schemas, followed by counts. With Tool search enabled, `tools/list` can
expose `tool_search`, `call_tool` and pinned tools rather than every unpinned
tool, so a change in tools need not change the count. Authentication, missing
versions and access failures are reported rather than silently ignored.
Approval enforcement belongs to the calling runtime.

## Cleanup

Remove this demo's saved values with
`dotnet user-secrets clear --project .\ToolboxDiscovery.csproj`, and unset any
environment variable you set. This sample creates no cloud resources. Do not
delete a shared Toolbox.

## Resources

- [Toolbox versions](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/toolbox#manage-toolbox-versions)
- [Tool search](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/tool-search#how-tool-search-works)
- [Episode guide](../../README.md)
