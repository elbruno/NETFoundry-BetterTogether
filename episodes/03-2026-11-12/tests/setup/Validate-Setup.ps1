#requires -Version 7.0
# Goal: Verify setup validation is offline, non-mutating and rejects bad bindings.
if (-not $IsWindows) { throw 'This isolation check currently requires Windows.' }

$setup = Join-Path $PSScriptRoot '..\..\demos\Setup-Demos.ps1'
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ("netfoundry-setup-check-" + [guid]::NewGuid())
$bindings = @{
    APPDATA = $sandbox
    TOOLBOX_ENDPOINT = 'not-an-active-demo-setting'
    AZURE_AI_PROJECT_ENDPOINT = 'https://example.services.ai.azure.com/api/projects/example'
    AZURE_AI_MODEL_DEPLOYMENT_NAME = 'example-model'
    FOUNDRY_PROJECT_ENDPOINT = 'https://example.services.ai.azure.com/api/projects/example'
    AZURE_OPENAI_DEPLOYMENT_NAME = 'example-model'
    BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME = 'example-blocklist'
    WIKIPEDIA_MCP_ENDPOINT = 'https://wikipedia.caseyjhand.com/mcp'
    FOUNDRY_AGENT_NAME = 'wikipedia-01'
}
$previous = @{}
$previousNativeExitCode = $global:LASTEXITCODE
try {
    foreach ($entry in $bindings.GetEnumerator()) {
        $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key)
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
    }

    & $setup -SelfTest
    & $setup -Validate
    if (Test-Path -LiteralPath $sandbox) { throw 'Validation created a settings directory.' }

    $env:FOUNDRY_AGENT_NAME = 'invalid agent name'
    $rejected = $false
    try { & $setup -Validate }
    catch { $rejected = $_.Exception.Message -like '*FOUNDRY_AGENT_NAME*' }
    if (-not $rejected) { throw 'Validation accepted an invalid hosted agent name.' }
    if (Test-Path -LiteralPath $sandbox) { throw 'Failed validation created a settings directory.' }

    $env:FOUNDRY_AGENT_NAME = 'wikipedia-01'
    $env:BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME = 'example-model'
    $rejected = $false
    try { & $setup -Validate }
    catch { $rejected = $_.Exception.Message -like '*dedicated model deployment*' }
    if (-not $rejected) { throw 'Validation accepted the general model as the blocklist deployment.' }
    if (Test-Path -LiteralPath $sandbox) { throw 'Failed blocklist validation created a settings directory.' }

    Remove-Item -LiteralPath 'Env:\BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME'
    $rejected = $false
    try { & $setup -Validate }
    catch { $rejected = $_.Exception.Message -like '*BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME*' }
    if (-not $rejected) { throw 'Validation accepted a missing dedicated blocklist deployment.' }
    if (Test-Path -LiteralPath $sandbox) { throw 'Missing blocklist validation created a settings directory.' }

    $env:FOUNDRY_AGENT_NAME = 'wikipedia-01'
    $env:BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME = 'example-blocklist'
    $setupPrompts = [Collections.Generic.List[string]]::new()
    $configuredProjects = [Collections.Generic.List[string]]::new()
    $configuredSettings = [Collections.Generic.List[object]]::new()
    function Read-Host {
        param([string] $Prompt)
        $setupPrompts.Add($Prompt)
        return ''
    }
    function dotnet {
        if ($args.Count -ne 6 -or $args[0] -ne 'user-secrets' -or
            $args[1] -ne 'set' -or $args[4] -ne '--project') {
            throw 'Unexpected dotnet invocation in the isolated setup test.'
        }
        $configuredSettings.Add([PSCustomObject]@{
            Key = [string] $args[2]
            Project = [string] $args[5]
        })
        $configuredProjects.Add([string] $args[5])
        $global:LASTEXITCODE = 0
    }
    $exampleDefaults = Join-Path $PSScriptRoot '..\..\demos\setup.defaults.example.json'
    & $setup -DefaultsPath $exampleDefaults -WarningAction SilentlyContinue

    if ($setupPrompts.Count -ne 7 -or
        @($setupPrompts | Where-Object { $_ -match 'toolbox' }).Count -ne 0) {
        throw 'Interactive setup did not ask for exactly the active demo settings.'
    }
    $actualProjects = @($configuredProjects |
        ForEach-Object { Split-Path (Split-Path $_ -Parent) -Leaf } |
        Sort-Object -Unique)
    $expectedProjects = @(
        '02-hosted-guardrails', '03-hosted-guardrails', '03-wikipedia-mcp-console',
        '04-wikipedia-hosted', '05-wikipedia-blocklist-console', '06-wikipedia-local-host',
        '07-mslearn-local-host', '08-mslearn-hosted', '09-ebsearch-mcp-console',
        '10-mslearn-mcp-console', '11-ebsearch-hosted-client',
        '12-wikipedia-hosted-client', '13-mslearn-hosted-client'
    )
    if (Compare-Object $actualProjects $expectedProjects) {
        throw 'Interactive setup did not configure exactly the active demo projects.'
    }
    $blocklistProject = @($configuredSettings | Where-Object {
        $_.Project -like '*05-wikipedia-blocklist-console*' -and
        $_.Key -eq 'BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME'
    })
    if ($blocklistProject.Count -ne 1) {
        throw 'Interactive setup did not configure the blocklist deployment separately.'
    }
    if (Test-Path -LiteralPath $sandbox) { throw 'Mocked setup created a settings directory.' }

    Write-Host 'Offline setup integration checks passed.'
}
finally {
    $global:LASTEXITCODE = $previousNativeExitCode
    foreach ($entry in $previous.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -LiteralPath "Env:\$($entry.Key)" -ErrorAction SilentlyContinue
        } else {
            [Environment]::SetEnvironmentVariable($entry.Key, [string] $entry.Value)
        }
    }
}
