[CmdletBinding(DefaultParameterSetName = 'Setup')]
param(
    [Parameter(ParameterSetName = 'Validate')]
    [switch] $Validate,
    [Parameter(ParameterSetName = 'Test')]
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
$toolboxProject = Join-Path $PSScriptRoot 'demos\01-toolbox-discovery\ToolboxDiscovery.csproj'
$agentProject = Join-Path $PSScriptRoot 'demos\02-hosted-guardrails\ApplicationGuardrails.csproj'

function Test-Settings([string] $Toolbox, [string] $Project, [string] $Model) {
    $toolboxUri = $null
    $projectUri = $null
    if (-not [Uri]::TryCreate($Toolbox, [UriKind]::Absolute, [ref] $toolboxUri) -or
        $toolboxUri.Scheme -ne 'https' -or
        -not $toolboxUri.Host.EndsWith('.services.ai.azure.com', [StringComparison]::OrdinalIgnoreCase) -or
        -not $toolboxUri.AbsolutePath.Contains('/toolboxes/') -or
        -not $toolboxUri.AbsolutePath.EndsWith('/mcp') -or
        $toolboxUri.Query -cne '?api-version=v1') { return $false }
    if (-not [Uri]::TryCreate($Project, [UriKind]::Absolute, [ref] $projectUri) -or
        $projectUri.Scheme -ne 'https' -or [string]::IsNullOrWhiteSpace($Model)) { return $false }
    return $true
}

if ($SelfTest) {
    $valid = @('https://example.services.ai.azure.com/api/projects/example/toolboxes/example/mcp?api-version=v1', 'https://example.services.ai.azure.com/api/projects/example', 'model')
    if (-not (Test-Settings @valid)) { throw 'Valid configuration was rejected.' }
    if (Test-Settings 'http://example.services.ai.azure.com/api/projects/example/toolboxes/example/mcp?api-version=v1' $valid[1] $valid[2]) { throw 'Insecure toolbox was accepted.' }
    if (Test-Settings $valid[0] 'http://example.com' $valid[2]) { throw 'Insecure project was accepted.' }
    if (Test-Settings $valid[0] $valid[1] '') { throw 'Empty deployment was accepted.' }
    if (Test-Settings ($valid[0] -replace 'api-version=v1', 'api-version=v2') $valid[1] $valid[2]) { throw 'Invalid toolbox API version was accepted.' }
    Write-Host 'Offline configuration tests passed.'
    return
}

if ($Validate) {
    function Get-EffectiveSetting([string] $Name, [string] $Project) {
        $environmentValue = [Environment]::GetEnvironmentVariable($Name)
        if ($null -ne $environmentValue) { return $environmentValue }
        $lines = @(& dotnet user-secrets list --project $Project)
        if ($LASTEXITCODE -ne 0) { throw "Cannot read user secrets for $Project." }
        $entry = $lines | Where-Object { $_ -match "^$([regex]::Escape($Name)) = " } | Select-Object -First 1
        if ($entry) { return $entry.Substring($Name.Length + 3) }
        return ''
    }
    $toolbox = Get-EffectiveSetting 'TOOLBOX_ENDPOINT' $toolboxProject
    $project = Get-EffectiveSetting 'AZURE_AI_PROJECT_ENDPOINT' $agentProject
    $model = Get-EffectiveSetting 'AZURE_AI_MODEL_DEPLOYMENT_NAME' $agentProject
    if (-not (Test-Settings $toolbox $project $model)) {
        throw 'Configuration is missing or invalid. Run setup.ps1, or set the three documented environment variables.'
    }
    Write-Host 'Configuration format is valid. Authentication, remote access and deployment were not checked.'
    return
}

Write-Host 'Enter nonsecret resource bindings only. Do not enter tokens, keys or passwords.'
$toolbox = Read-Host 'Toolbox MCP HTTPS URL (including ?api-version=v1)'
$project = Read-Host 'Foundry project HTTPS endpoint'
$model = Read-Host 'Foundry model deployment name'
if (-not (Test-Settings $toolbox $project $model)) {
    throw 'Invalid values. No settings were written.'
}
foreach ($setting in @(
    @{ Project = $toolboxProject; Key = 'TOOLBOX_ENDPOINT'; Value = $toolbox },
    @{ Project = $agentProject; Key = 'AZURE_AI_PROJECT_ENDPOINT'; Value = $project },
    @{ Project = $agentProject; Key = 'AZURE_AI_MODEL_DEPLOYMENT_NAME'; Value = $model }
)) {
    & dotnet user-secrets set $setting.Key $setting.Value --project $setting.Project
    if ($LASTEXITCODE -ne 0) { throw "Failed to write $($setting.Key); earlier settings may have been saved." }
}
Write-Host 'Saved nonsecret bindings in .NET user secrets. Environment variables take precedence.'
