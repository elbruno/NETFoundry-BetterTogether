#requires -Version 7.0
# Configure noncredential bindings only. This never signs in, deploys or calls a model.
[CmdletBinding()]
param(
    [switch] $Validate,
    [switch] $SelfTest,
    [string] $DefaultsPath = (Join-Path $PSScriptRoot 'setup.defaults.local.json')
)
$ErrorActionPreference = 'Stop'
if ($Validate -and $SelfTest) { throw 'Choose either -Validate or -SelfTest.' }

function Test-Https([string] $Value) {
    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref] $uri) -or
        $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment) { return $false }
    return -not $uri.Query
}

function Test-Binding([string] $Name, [string] $Value) {
    if ($Name.EndsWith('ENDPOINT', [StringComparison]::Ordinal)) { return Test-Https $Value }
    return $Value -cmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$'
}

if ($SelfTest) {
    foreach ($invalid in @('http://example.com', 'https://user:password@example.com', 'https://example.com/#fragment', '')) {
        if (Test-Binding 'FOUNDRY_PROJECT_ENDPOINT' $invalid) { throw 'Unsafe endpoint accepted.' }
    }
    if (Test-Binding 'BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME' 'not a deployment') { throw 'Invalid deployment accepted.' }
    if (Test-Binding 'FOUNDRY_AGENT_NAME' 'not an agent') { throw 'Invalid agent name accepted.' }
    if (-not (Test-Binding 'FOUNDRY_AGENT_NAME' 'wikipedia-01')) { throw 'Valid agent name rejected.' }
    if (-not (Test-Binding 'WIKIPEDIA_MCP_ENDPOINT' 'https://wikipedia.caseyjhand.com/mcp')) { throw 'Valid MCP endpoint rejected.' }
    Write-Host 'Offline setup tests passed. No settings were read or written.'
    return
}

$projects = @(
    @{ Path = '02-hosted-guardrails\ApplicationGuardrails.csproj'; Settings = @{ AZURE_AI_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_AI_MODEL_DEPLOYMENT_NAME = 'ModelDeployment' } },
    @{ Path = '03-wikipedia-mcp-console\WikipediaConsole.csproj'; Settings = @{ FOUNDRY_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_OPENAI_DEPLOYMENT_NAME = 'ModelDeployment'; WIKIPEDIA_MCP_ENDPOINT = 'WikipediaMcpEndpoint' } },
    @{ Path = '04-wikipedia-hosted\WikipediaHosted.csproj'; Settings = @{ AZURE_AI_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_AI_MODEL_DEPLOYMENT_NAME = 'ModelDeployment'; WIKIPEDIA_MCP_ENDPOINT = 'WikipediaMcpEndpoint' } },
    @{ Path = '05-wikipedia-blocklist-console\WikipediaBlocklist.csproj'; Settings = @{ FOUNDRY_PROJECT_ENDPOINT = 'ProjectEndpoint'; BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME = 'BlocklistDeployment' } },
    @{ Path = '03-hosted-guardrails\ApplicationGuardrailsHosted.csproj'; Settings = @{ AZURE_AI_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_AI_MODEL_DEPLOYMENT_NAME = 'ModelDeployment' } },
    @{ Path = '06-wikipedia-local-host\WikipediaLocal.csproj'; Settings = @{ AZURE_AI_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_AI_MODEL_DEPLOYMENT_NAME = 'ModelDeployment'; WIKIPEDIA_MCP_ENDPOINT = 'WikipediaMcpEndpoint' } },
    @{ Path = '07-mslearn-local-host\LearnLocal.csproj'; Settings = @{ AZURE_AI_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_AI_MODEL_DEPLOYMENT_NAME = 'ModelDeployment' } },
    @{ Path = '08-mslearn-hosted\LearnHosted.csproj'; Settings = @{ AZURE_AI_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_AI_MODEL_DEPLOYMENT_NAME = 'ModelDeployment' } },
    @{ Path = '09-ebsearch-mcp-console\EBSearchConsole.csproj'; Settings = @{ FOUNDRY_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_OPENAI_DEPLOYMENT_NAME = 'ModelDeployment' } },
    @{ Path = '10-mslearn-mcp-console\LearnConsole.csproj'; Settings = @{ FOUNDRY_PROJECT_ENDPOINT = 'ProjectEndpoint'; AZURE_OPENAI_DEPLOYMENT_NAME = 'ModelDeployment' } },
    @{ Path = '11-ebsearch-hosted-client\EBSearchClient.csproj'; Settings = @{ FOUNDRY_PROJECT_ENDPOINT = 'ProjectEndpoint'; FOUNDRY_AGENT_NAME = 'EBSearchAgentName' } },
    @{ Path = '12-wikipedia-hosted-client\WikipediaClient.csproj'; Settings = @{ FOUNDRY_PROJECT_ENDPOINT = 'ProjectEndpoint'; FOUNDRY_AGENT_NAME = 'WikipediaAgentName' } },
    @{ Path = '13-mslearn-hosted-client\LearnClient.csproj'; Settings = @{ FOUNDRY_PROJECT_ENDPOINT = 'ProjectEndpoint'; FOUNDRY_AGENT_NAME = 'LearnAgentName' } }
)

function Get-SavedSettings([string] $ProjectPath) {
    [xml] $project = Get-Content -LiteralPath $ProjectPath -Raw
    $id = [string] $project.Project.PropertyGroup.UserSecretsId
    if (-not $id) { throw "No UserSecretsId in $ProjectPath." }
    $path = if ($IsWindows) {
        Join-Path $env:APPDATA "Microsoft\UserSecrets\$id\secrets.json"
    } else {
        Join-Path $HOME ".microsoft/usersecrets/$id/secrets.json"
    }
    if (Test-Path -LiteralPath $path) {
        return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    }
    return @{}
}

$existing = @{}
foreach ($project in $projects) {
    $path = Join-Path $PSScriptRoot $project.Path
    $saved = Get-SavedSettings $path
    $existing[$project.Path] = $saved
    if ($Validate) {
        foreach ($setting in $project.Settings.GetEnumerator()) {
            $value = [Environment]::GetEnvironmentVariable($setting.Key)
            if ($null -eq $value) { $value = $saved[$setting.Key] }
            if (-not (Test-Binding $setting.Key $value)) {
                throw "Missing or invalid $($setting.Key) for $($project.Path). No settings were changed."
            }
        }
    }
}
if ($Validate) {
    $blocklistPath = '05-wikipedia-blocklist-console\WikipediaBlocklist.csproj'
    $blocklistDeployment = [Environment]::GetEnvironmentVariable('BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME')
    if ($null -eq $blocklistDeployment) { $blocklistDeployment = $existing[$blocklistPath]['BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME'] }
    $sharedDeployments = @(
        @{
            Path = '02-hosted-guardrails\ApplicationGuardrails.csproj'
            Key = 'AZURE_AI_MODEL_DEPLOYMENT_NAME'
        },
        @{
            Path = '03-wikipedia-mcp-console\WikipediaConsole.csproj'
            Key = 'AZURE_OPENAI_DEPLOYMENT_NAME'
        }
    ) | ForEach-Object {
        $value = [Environment]::GetEnvironmentVariable($_.Key)
        if ($null -eq $value) { $value = $existing[$_.Path][ $_.Key ] }
        $value
    }
    if ($sharedDeployments | Where-Object { [string]::Equals($blocklistDeployment, $_, [StringComparison]::OrdinalIgnoreCase) }) {
        throw 'BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME must identify a dedicated model deployment, not the general episode model. No settings were changed.'
    }
    Write-Host 'All 13 active demo projects have valid configuration format. No writes or cloud calls were made.'
    return
}

$defaults = @{}
foreach ($project in $projects) {
    foreach ($setting in $project.Settings.GetEnumerator()) {
        $saved = $existing[$project.Path][$setting.Key]
        if ($saved -and -not $defaults.ContainsKey($setting.Value)) { $defaults[$setting.Value] = $saved }
    }
}
foreach ($fallback in @{
    WikipediaMcpEndpoint = 'https://wikipedia.caseyjhand.com/mcp'
    EBSearchAgentName = 'ebsearch-01'
    WikipediaAgentName = 'wikipedia-01'
    LearnAgentName = 'mslearn-01'
}.GetEnumerator()) {
    if (-not $defaults[$fallback.Key]) { $defaults[$fallback.Key] = $fallback.Value }
}
if (Test-Path -LiteralPath $DefaultsPath) {
    $privateDefaults = Get-Content -LiteralPath $DefaultsPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($name in @('ProjectEndpoint', 'ModelDeployment', 'BlocklistDeployment', 'WikipediaMcpEndpoint', 'EBSearchAgentName', 'WikipediaAgentName', 'LearnAgentName')) {
        if ($privateDefaults.ContainsKey($name)) { $defaults[$name] = [string] $privateDefaults[$name] }
    }
}
Write-Host 'Visible defaults are nonsecret bindings. Press Enter to accept. Never enter a key, token or password.'
$labels = [ordered]@{
    ProjectEndpoint = 'Foundry project endpoint'
    ModelDeployment = 'Existing Azure OpenAI model deployment'
    BlocklistDeployment = 'Dedicated blocklist test deployment (must differ from the general model)'
    WikipediaMcpEndpoint = 'Public community Wikipedia MCP endpoint'
    EBSearchAgentName = 'EBSearch hosted agent name'
    WikipediaAgentName = 'Wikipedia hosted agent name'
    LearnAgentName = 'Microsoft Learn hosted agent name'
}
foreach ($name in $labels.Keys) {
    $answer = [string] (Read-Host "$($labels[$name]) [$($defaults[$name])]")
    if ($answer.Trim()) { $defaults[$name] = $answer.Trim() }
}
# Validate every value before writing any project's configuration.
if ([string]::Equals($defaults.ModelDeployment, $defaults.BlocklistDeployment, [StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($env:AZURE_AI_MODEL_DEPLOYMENT_NAME, $defaults.BlocklistDeployment, [StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($env:AZURE_OPENAI_DEPLOYMENT_NAME, $defaults.BlocklistDeployment, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'BlocklistDeployment must identify a dedicated model deployment, not the general episode model. No settings were written.'
}
foreach ($project in $projects) {
    foreach ($setting in $project.Settings.GetEnumerator()) {
        if (-not (Test-Binding $setting.Key $defaults[$setting.Value])) { throw "Invalid $($setting.Value). No settings were written." }
    }
}
foreach ($project in $projects) {
    foreach ($setting in $project.Settings.GetEnumerator()) {
        & dotnet user-secrets set $setting.Key $defaults[$setting.Value] --project (Join-Path $PSScriptRoot $project.Path)
        if ($LASTEXITCODE -ne 0) { throw "Failed to write $($setting.Key). Earlier settings may have been saved." }
        if ($null -ne [Environment]::GetEnvironmentVariable($setting.Key)) {
            Write-Warning "$($setting.Key) exists in this process and overrides saved settings."
        }
    }
}
Write-Host 'Configured all 13 active demo projects. User secrets are local only; configure hosted azd bindings separately before deployment.'
