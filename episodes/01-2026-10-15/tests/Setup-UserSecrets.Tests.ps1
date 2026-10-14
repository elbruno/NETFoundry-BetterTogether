<#
.SYNOPSIS
Runs setup-script checks without writing secrets or contacting Azure.
.DESCRIPTION
A. Replace prompts and dotnet with in-process test doubles.
B. Exercise identity, key, validation-only, and failure paths.
C. Assert stdin imports, stale-key removal, and secret-free output.
Run: pwsh -File .\tests\Setup-UserSecrets.Tests.ps1
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$setup = Join-Path (Split-Path $PSScriptRoot) 'Setup-UserSecrets.ps1'

# This child scope keeps test doubles from leaking into the caller's session.
& {
    param($SetupPath)
    $global:EpisodeSetupTestState = @{
        Answers = [Collections.Generic.Queue[string]]::new()
        Calls = [Collections.Generic.List[object]]::new()
        FailImport = $false
    }
    $global:LASTEXITCODE = 0
    try {

    function Assert([bool] $Condition, [string] $Message) {
        if (-not $Condition) { throw $Message }
    }

    function Read-Host {
        param([string] $Prompt, [switch] $AsSecureString)
        if ($global:EpisodeSetupTestState.Answers.Count -eq 0) { throw 'Unexpected prompt.' }
        $answer = $global:EpisodeSetupTestState.Answers.Dequeue()
        if ($AsSecureString) { return ConvertTo-SecureString $answer -AsPlainText -Force }
        return $answer
    }

    function dotnet {
        $global:EpisodeSetupTestState.Calls.Add([pscustomobject]@{
            Arguments = @($args)
            InputJson = @($input) -join "`n"
        })
        $global:LASTEXITCODE = if ($global:EpisodeSetupTestState.FailImport) { 1 } else { 0 }
        # Deliberately hostile CLI output must never reach the transcript.
        'SYNTHETIC_SECRET_CLI_OUTPUT'
    }

    $projects = @(Get-ChildItem (Join-Path (Split-Path $SetupPath) 'demos') -Filter '*.csproj' -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](tests|bin|obj)[\\/]' })
    $count = $projects.Count

    # A/B. Validate-only must not prompt or invoke the secrets CLI.
    $transcript = & $SetupPath -ValidateOnly 6>&1 | Out-String
    Assert ($global:EpisodeSetupTestState.Calls.Count -eq 0) 'Validate-only invoked dotnet.'

    # Identity mode retries malformed URLs and clears stale key authentication.
    foreach ($value in @('http://bad.example/', 'https://example.invalid/',
        'synthetic-deployment', '', 'y', '', 'nimble')) {
        $global:EpisodeSetupTestState.Answers.Enqueue($value)
    }
    $transcript = & $SetupPath 3>&1 6>&1 | Out-String
    Assert ($global:EpisodeSetupTestState.Answers.Count -eq 0) 'Identity prompts changed unexpectedly.'
    Assert ($global:EpisodeSetupTestState.Calls.Count -eq 2 * $count) 'Identity mode did not import and remove keys for every project.'
    $imports = @($global:EpisodeSetupTestState.Calls | Where-Object { $_.Arguments[1] -eq 'set' })
    foreach ($call in $imports) {
        $json = $call.InputJson | ConvertFrom-Json -AsHashtable
        Assert ($json['Foundry:Model'] -eq 'synthetic-deployment') 'Deployment was not imported.'
        Assert (-not $json.ContainsKey('Foundry:ApiKey')) 'Identity import unexpectedly contains an API key.'
        $isGame = ($call.Arguments -join ' ') -match 'DecisionSpaceGame\.csproj'
        Assert ($json.ContainsKey('Ollama:Model') -eq $isGame) 'Ollama configuration was applied to the wrong sample.'
    }
    Assert (-not $transcript.Contains('SYNTHETIC_SECRET_CLI_OUTPUT')) 'CLI output leaked.'

    # A/B. Key mode sends the key through stdin, never process arguments.
    $global:EpisodeSetupTestState.Calls.Clear()
    foreach ($value in @('https://example.invalid/', 'synthetic-deployment', 'K',
        'SYNTHETIC_API_KEY_FOR_TEST_ONLY', 'n')) { $global:EpisodeSetupTestState.Answers.Enqueue($value) }
    $transcript = & $SetupPath 6>&1 | Out-String
    Assert ($global:EpisodeSetupTestState.Calls.Count -eq $count) 'Key mode invoked an unexpected command.'
    foreach ($call in $global:EpisodeSetupTestState.Calls) {
        Assert (-not (($call.Arguments -join ' ').Contains('SYNTHETIC_API_KEY'))) 'Key appeared in process arguments.'
        $json = $call.InputJson | ConvertFrom-Json -AsHashtable
        Assert ($json['Foundry:ApiKey'] -eq 'SYNTHETIC_API_KEY_FOR_TEST_ONLY') 'Key not imported through stdin.'
    }
    Assert (-not $transcript.Contains('SYNTHETIC_API_KEY')) 'Key leaked into the transcript.'

    # C. CLI failures stop subsequent writes and surface an explicit error.
    $global:EpisodeSetupTestState.Calls.Clear()
    $global:EpisodeSetupTestState.FailImport = $true
    foreach ($value in @('https://example.invalid/', 'synthetic-deployment', '', 'n')) {
        $global:EpisodeSetupTestState.Answers.Enqueue($value)
    }
    $failed = $false
    try { $null = & $SetupPath 6>&1 }
    catch { $failed = $_.Exception.Message.Contains('earlier projects may already be configured') }
    Assert $failed 'CLI failure was not reported.'
    Assert ($global:EpisodeSetupTestState.Calls.Count -eq 1) 'Setup continued writing after a CLI failure.'
    Write-Host "PASS: setup validation, identity/Ollama, key/stdin, and failure paths across $count samples."
    }
    finally {
        Remove-Variable EpisodeSetupTestState -Scope Global
    }
} $setup
