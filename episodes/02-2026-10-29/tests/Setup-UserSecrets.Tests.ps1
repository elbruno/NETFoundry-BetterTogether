<#
.SYNOPSIS
Checks setup without writing user secrets, user environment values, or calling Azure.
.EXAMPLE
pwsh -File .\tests\Setup-UserSecrets.Tests.ps1
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$setup = Join-Path (Split-Path $PSScriptRoot) 'Setup-UserSecrets.ps1'

& {
    param($SetupPath)
    $state = @{
        Answers = [Collections.Generic.Queue[string]]::new()
        Calls = [Collections.Generic.List[object]]::new()
        ExitCode = 0
    }
    $oldEndpoint = $env:Decision__Endpoint
    $oldDeployment = $env:Decision__Deployment
    $oldExitCode = Get-Variable LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue

    function Assert([bool] $Condition, [string] $Message) {
        if (-not $Condition) { throw $Message }
    }
    function Read-Host {
        param([string] $Prompt)
        if ($state.Answers.Count -eq 0) { throw 'Unexpected prompt.' }
        return $state.Answers.Dequeue()
    }
    function dotnet {
        $state.Calls.Add([pscustomobject]@{
            Arguments = @($args)
            InputJson = @($input) -join "`n"
        })
        $global:LASTEXITCODE = $state.ExitCode
        'SYNTHETIC_PRIVATE_CLI_OUTPUT'
    }

    try {
        $env:Decision__Endpoint = 'original-endpoint'
        $env:Decision__Deployment = 'original-deployment'
        $transcript = & $SetupPath -ValidateOnly 6>&1 | Out-String
        Assert ($state.Calls.Count -eq 0) 'Validation invoked dotnet.'
        Assert ($env:Decision__Endpoint -eq 'original-endpoint') 'Validation changed the environment.'

        foreach ($answer in @('http://example.invalid/', 'https://example.invalid/api/projects/demo',
            'https://user:password@example.invalid/', 'https://example.invalid/?key=test',
            'https://example.invalid/#fragment', ' https://example.invalid ', '', ' synthetic-deployment ')) {
            $state.Answers.Enqueue($answer)
        }
        $transcript = & $SetupPath -SessionOnly 3>&1 6>&1 | Out-String
        Assert ($state.Answers.Count -eq 0) 'Prompts did not validate or trim input correctly.'
        Assert ($state.Calls.Count -eq 1) 'Setup must import the hosted secrets exactly once.'
        $call = $state.Calls[0]
        Assert (($call.Arguments[0..2] -join ' ') -eq 'user-secrets set --project') 'Incorrect secrets command.'
        Assert ($call.Arguments[-1] -eq (Join-Path (Split-Path $SetupPath) 'demos\04-foundry-hosted-agent\src\SupportRoutingAgent\SupportRoutingAgent.csproj')) 'Wrong project.'
        $values = $call.InputJson | ConvertFrom-Json -AsHashtable
        Assert ($values.Count -eq 2) 'Unexpected secrets.'
        Assert ($values['Decision:Endpoint'] -eq 'https://example.invalid/') 'Incorrect endpoint import.'
        Assert ($values['Decision:Deployment'] -eq 'synthetic-deployment') 'Incorrect deployment import.'
        Assert ($env:Decision__Endpoint -eq $values['Decision:Endpoint']) 'Console endpoint differs from hosted secrets.'
        Assert ($env:Decision__Deployment -eq $values['Decision:Deployment']) 'Console deployment differs from hosted secrets.'
        foreach ($text in @('SYNTHETIC_PRIVATE_CLI_OUTPUT', 'synthetic-deployment', 'example.invalid')) {
            Assert (-not $transcript.Contains($text)) 'Configuration or CLI output leaked.'
            Assert (-not ($call.Arguments -join ' ').Contains($text)) 'Configuration leaked into process arguments.'
        }

        $state.ExitCode = 1
        $state.Answers.Enqueue('https://other.invalid/')
        $state.Answers.Enqueue('other-deployment')
        $failed = $false
        try {
            $null = & $SetupPath -SessionOnly 6>&1
        } catch {
            $failed = $true
            Assert ($_.Exception.Message.Contains('dotnet user-secrets failed')) 'Failure was not actionable.'
        }
        Assert $failed 'A failed secrets import must stop setup.'
        Assert ($env:Decision__Endpoint -eq 'https://example.invalid/') 'Failed import changed the environment.'
        Assert ($env:Decision__Deployment -eq 'synthetic-deployment') 'Failed import changed the deployment.'
        Write-Host 'All Episode 2 setup checks passed.'
    } finally {
        [Environment]::SetEnvironmentVariable('Decision__Endpoint', $oldEndpoint, 'Process')
        [Environment]::SetEnvironmentVariable('Decision__Deployment', $oldDeployment, 'Process')
        if ($null -ne $oldExitCode) {
            $global:LASTEXITCODE = $oldExitCode.Value
        } else {
            Remove-Variable LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
        }
    }
} $setup
