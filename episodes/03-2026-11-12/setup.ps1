#requires -Version 7.0
# Compatibility entry point; all episode configuration lives in demos\Setup-Demos.ps1.
[CmdletBinding()]
param(
    [switch] $Validate,
    [switch] $SelfTest,
    [string] $DefaultsPath = (Join-Path $PSScriptRoot 'demos\setup.defaults.local.json')
)
& (Join-Path $PSScriptRoot 'demos\Setup-Demos.ps1') @PSBoundParameters
