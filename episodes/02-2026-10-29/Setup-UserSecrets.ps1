<#
.SYNOPSIS
Prompts once for the decision resource and deployment, then configures all four local demos.
.DESCRIPTION
A. Validate the hosted project's existing user-secrets store.
B. Import the shared configuration through stdin.
C. Set environment variables for the console demos and Aspire.

Run in the PowerShell terminal used for the demos, before recording.
By default, environment values are also saved for future Windows terminals.
This script does not sign in, provision resources, change RBAC, or call a model.
.EXAMPLE
.\Setup-UserSecrets.ps1
.EXAMPLE
.\Setup-UserSecrets.ps1 -SessionOnly
.EXAMPLE
.\Setup-UserSecrets.ps1 -ValidateOnly
#>
[CmdletBinding()]
param(
    [switch] $SessionOnly,
    [switch] $ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-DecisionEndpoint {
    while ($true) {
        $text = (Read-Host 'Foundry resource root (https://<resource>.services.ai.azure.com/)').Trim()
        $uri = $null
        if ([Uri]::TryCreate($text, [UriKind]::Absolute, [ref] $uri) -and
            $uri.Scheme -eq 'https' -and -not $uri.UserInfo -and
            -not $uri.Query -and -not $uri.Fragment -and $uri.AbsolutePath -eq '/') {
            return $uri.AbsoluteUri
        }
        Write-Warning 'Enter an HTTPS resource root, not a project URL or API path, without credentials, query, or fragment.'
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 10 SDK before running this script.'
}
$project = Join-Path $PSScriptRoot 'demos\04-foundry-hosted-agent\src\SupportRoutingAgent\SupportRoutingAgent.csproj'
[xml] $xml = Get-Content -LiteralPath $project -Raw
$id = $xml.SelectSingleNode('/Project/PropertyGroup/UserSecretsId')
if ($null -eq $id -or [string]::IsNullOrWhiteSpace($id.InnerText)) {
    throw 'The hosted agent project must have a UserSecretsId.'
}
if ($ValidateOnly) {
    Write-Host 'Validated Episode 2 setup. No prompts, configuration changes, or Azure calls.'
    return
}
if (-not $SessionOnly -and [Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Persistent user environment setup requires Windows. Use -SessionOnly on other platforms.'
}

Write-Host 'Run before recording. Input is visible while typing; supplied values will not be printed afterward.'
Write-Host 'Existing Decision__Endpoint and Decision__Deployment values will be replaced.'
$endpoint = Read-DecisionEndpoint
do {
    $deployment = (Read-Host 'Decision deployment name (not the catalog model name)').Trim()
    if ([string]::IsNullOrWhiteSpace($deployment)) {
        Write-Warning 'A non-empty deployment name is required.'
    }
} while ([string]::IsNullOrWhiteSpace($deployment))

$values = @{
    'Decision:Endpoint' = $endpoint
    'Decision:Deployment' = $deployment
}
# stdin avoids placing configuration values in the dotnet process command line.
$json = $values | ConvertTo-Json -Compress
$output = $json | & dotnet user-secrets set --project $project 2>&1
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet user-secrets failed. Environment variables were not changed. Check the SDK and secrets-file permissions, then rerun. CLI output was withheld to protect configuration values.'
}

foreach ($entry in @{
    Decision__Endpoint = $endpoint
    Decision__Deployment = $deployment
}.GetEnumerator()) {
    if (-not $SessionOnly) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'User')
    }
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
}

Write-Host 'Configured all four local demos: process environment plus the hosted agent user-secrets store.'
if (-not $SessionOnly) {
    Write-Host 'Saved user environment values for future terminals. Restart existing VS Code windows and terminals to pick them up.'
}
Write-Host 'Use this terminal for the demos. Running via pwsh -File only configures that child process and future terminals.'
Write-Host 'Authentication uses az login and existing model permissions, not an API key. Hosted deployment configuration is unchanged.'
