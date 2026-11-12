#requires -Version 7.0
# Goal: Verify setup validation is offline, non-mutating and rejects bad bindings.
if (-not $IsWindows) { throw 'This isolation check currently requires Windows.' }

$setup = Join-Path $PSScriptRoot '..\..\demos\Setup-Demos.ps1'
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ("netfoundry-setup-check-" + [guid]::NewGuid())
$bindings = @{
    APPDATA = $sandbox
    TOOLBOX_ENDPOINT = 'https://example.services.ai.azure.com/api/projects/example/toolboxes/example/mcp?api-version=v1'
    AZURE_AI_PROJECT_ENDPOINT = 'https://example.services.ai.azure.com/api/projects/example'
    FOUNDRY_PROJECT_ENDPOINT = 'https://example.services.ai.azure.com/api/projects/example'
    AZURE_AI_MODEL_DEPLOYMENT_NAME = 'example-model'
    AZURE_OPENAI_DEPLOYMENT_NAME = 'example-model'
    WIKIPEDIA_MCP_ENDPOINT = 'https://wikipedia.caseyjhand.com/mcp'
    FOUNDRY_AGENT_NAME = 'wikipedia-01'
}
$previous = @{}
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

    Write-Host 'Offline setup integration checks passed.'
}
finally {
    foreach ($entry in $previous.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
    }
}
