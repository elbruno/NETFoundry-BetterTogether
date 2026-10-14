<#
.SYNOPSIS
Prompts once and configures user secrets for this episode's runnable samples.
.DESCRIPTION
A. Discover the sample projects and validate their UserSecretsId values.
B. Prompt for Foundry configuration and optional local Ollama configuration.
C. Import values through standard input, never through command-line arguments.

Run before screen sharing. User secrets are local development storage, not
encrypted production secret storage. This script never provisions resources,
signs in, runs inference, or prints the values supplied.
.EXAMPLE
pwsh -File .\Setup-UserSecrets.ps1
.EXAMPLE
pwsh -File .\Setup-UserSecrets.ps1 -ValidateOnly
#>
[CmdletBinding()]
param(
    [switch] $ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-RequiredValue {
    param([string] $Prompt)
    do {
        $value = Read-Host $Prompt
        if ([string]::IsNullOrWhiteSpace($value)) {
            Write-Warning 'A non-empty value is required.'
        }
    } while ([string]::IsNullOrWhiteSpace($value))
    return $value.Trim()
}

function Read-Endpoint {
    param([string] $Prompt, [switch] $AllowHttp, [string] $Default)
    while ($true) {
        $text = Read-Host $Prompt
        if ([string]::IsNullOrWhiteSpace($text) -and $Default) { $text = $Default }
        $uri = $null
        $valid = [Uri]::TryCreate($text, [UriKind]::Absolute, [ref] $uri)
        if ($valid -and $uri.Scheme -in @('https', 'http') -and
            ($AllowHttp -or $uri.Scheme -eq 'https') -and
            -not $uri.UserInfo -and -not $uri.Query -and -not $uri.Fragment -and
            $uri.AbsolutePath -eq '/') {
            return $uri.AbsoluteUri
        }
        Write-Warning 'Enter a resource root URL, without credentials, query, fragment, or API path. Foundry requires HTTPS.'
    }
}

function Invoke-SecretsCommand {
    param([string[]] $Arguments, [string] $Json)
    # Piping JSON keeps the API key out of the dotnet process command line.
    # Suppress CLI output because third-party diagnostics could echo input.
    $output = if ($PSBoundParameters.ContainsKey('Json')) {
        $Json | & dotnet @Arguments 2>&1
    } else {
        & dotnet @Arguments 2>&1
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet user-secrets failed. Setup stopped; earlier projects may already be configured. Check SDK and local secrets-file permissions, then rerun. Diagnostic output was withheld to protect configuration values.'
    }
}

# A. Only runnable projects under this episode are targets; test projects are excluded.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 10 SDK before running this script.'
}
$projects = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'demos') -Filter '*.csproj' -Recurse |
    Where-Object {
        $_.FullName -notmatch '[\\/](tests|bin|obj)[\\/]' -and
        $_.Name -notmatch '\.(AppHost|ServiceDefaults)\.csproj$'
    } |
    Sort-Object FullName)
if ($projects.Count -eq 0) { throw 'No sample projects were found under this episode.' }
$ids = @{}
foreach ($project in $projects) {
    [xml] $xml = Get-Content -LiteralPath $project.FullName -Raw
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/UserSecretsId')
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw "Missing UserSecretsId in $($project.Name)."
    }
    if ($ids.ContainsKey($node.InnerText)) { throw 'Each sample must have a unique UserSecretsId.' }
    $ids[$node.InnerText] = $true
    Write-Host "Sample: $($project.Directory.Name) ($($project.Name))"
}
if ($ValidateOnly) {
    Write-Host "Validated $($projects.Count) sample projects. No secrets were read or changed."
    return
}

# B. Foundry deployment name is not necessarily the catalog model name.
Write-Host 'Run setup before recording or sharing your screen. No supplied values will be printed by this script.'
$endpoint = Read-Endpoint 'Foundry resource root (https://<resource>.services.ai.azure.com)'
$model = Read-RequiredValue 'Foundry deployment name (not the catalog model name)'
$generativeModel = Read-RequiredValue 'Generative deployment name for comparison demos 03 and 06 (for example gpt-6.1-sol, on the same resource)'
do {
    $auth = (Read-Host 'Authentication: Azure CLI identity [Enter] or API key [K]').Trim()
} while ($auth -notin @('', 'K', 'k'))

$values = @{
    'Foundry:Endpoint' = $endpoint
    'Foundry:Model' = $model
}
$keyPointer = [IntPtr]::Zero
$secureKey = $null
try {
    if ($auth -in @('K', 'k')) {
        do {
            $secureKey = Read-Host 'Foundry API key (hidden input)' -AsSecureString
            if ($secureKey.Length -eq 0) { Write-Warning 'An API key is required for key authentication.' }
        } while ($secureKey.Length -eq 0)
        $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        $values['Foundry:ApiKey'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    }
    $local = (Read-Host 'Also configure local Ollama for demo 02? [y/N]').Trim()
    $ollama = $null
    if ($local -in @('y', 'yes')) {
        $ollama = @{
            'Ollama:Endpoint' = Read-Endpoint 'Ollama root URL [http://localhost:11434/]' -AllowHttp -Default 'http://localhost:11434/'
            'Ollama:Model' = Read-RequiredValue 'Local native decision model name (for example nimble)'
        }
    }

    # C. Import merges supplied keys. Identity mode removes a stale key, which
    # would otherwise take precedence over Azure CLI authentication.
    foreach ($project in $projects) {
        $localOnly = $project.Name -eq 'HelloDecisionsOllama.csproj'
        if ($localOnly -and $null -eq $ollama) {
            Write-Host "Skipped $($project.Directory.Name): Ollama settings unchanged."
            continue
        }
        $projectValues = if ($localOnly) { @{} } else { @{} + $values }
        if ($project.Name -in @('DecisionComparison.csproj', 'DecisionLab.Web.csproj')) {
            $projectValues['Foundry:GenerativeModel'] = $generativeModel
        }
        if ($null -ne $ollama -and $localOnly) {
            foreach ($entry in $ollama.GetEnumerator()) { $projectValues[$entry.Key] = $entry.Value }
        }
        $json = $projectValues | ConvertTo-Json -Compress
        Invoke-SecretsCommand -Arguments @('user-secrets', 'set', '--project', $project.FullName) -Json $json
        if ($auth -eq '' -and -not $localOnly) {
            Invoke-SecretsCommand -Arguments @('user-secrets', 'remove', 'Foundry:ApiKey', '--project', $project.FullName)
        }
        Write-Host "Configured $($project.Directory.Name)."
        $projectValues.Clear()
        $json = $null
    }
    Write-Host 'Done. Identity mode requires az login and deployment access. Environment variables can override user secrets.'
    Write-Host 'Ollama configuration does not install a server or download a model. No inference requests were made.'
}
finally {
    if ($keyPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer) }
    if ($null -ne $secureKey) { $secureKey.Dispose() }
    $values.Clear()
    $json = $null
}
