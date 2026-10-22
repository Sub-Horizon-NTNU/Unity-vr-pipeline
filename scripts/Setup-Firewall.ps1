#Requires -Version 5.1
<#
.SYNOPSIS
Allow ROV video through Windows Firewall for one application.
.EXAMPLE
.\Setup-Firewall.ps1 -WhatIf
.EXAMPLE
.\Setup-Firewall.ps1 -ProgramPath 'C:\ROV\Unity-vr-pipeline-v1.exe'
.EXAMPLE
.\Setup-Firewall.ps1 -Remove
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string]$ProgramPath,
    [ValidateScript({
        $address = $null
        if ($_ -notmatch '^\d{1,3}(\.\d{1,3}){3}$' -or
            ![Net.IPAddress]::TryParse($_, [ref]$address) -or
            $address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or
            $_ -eq '0.0.0.0' -or $_ -eq '255.255.255.255') {
            throw 'RovAddress must be a single IPv4 address, for example 192.168.2.2.'
        }
        $true
    })]
    [string]$RovAddress = '192.168.2.2',
    [ValidateRange(1, 65535)][int]$VideoPort = 5601,
    [switch]$Remove
)
$ErrorActionPreference = 'Stop'
if (!$ProgramPath) {
    $ProgramPath = Join-Path $PSScriptRoot 'Unity-vr-pipeline-v1.exe'
    if (!(Test-Path -LiteralPath $ProgramPath -PathType Leaf)) {
        $ProgramPath = Join-Path $PSScriptRoot '..\Builds\v1\Unity-vr-pipeline-v1.exe'
    }
}
$program = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ProgramPath)
if ([IO.Path]::GetExtension($program) -ne '.exe') { throw 'ProgramPath must point to a Windows .exe.' }
if (!$Remove -and !(Test-Path -LiteralPath $program -PathType Leaf)) {
    throw "Application not found: $program. Place this script beside the EXE or supply -ProgramPath."
}
# Stable per application path: separate installations do not overwrite each other.
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $digest = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($program.ToLowerInvariant()))
    $suffix = ([BitConverter]::ToString($digest)).Replace('-', '').Substring(0, 16)
} finally { $sha.Dispose() }
$ruleName = 'SubHorizon-ROV-Video-' + $suffix
$group = 'Sub Horizon ROV VR'
$action = "Allow inbound UDP local port $VideoPort from $RovAddress on all network profiles"
if ($Remove) { $action = 'Remove the ROV video firewall rule for this application' }
Write-Host "Application: $program"
Write-Host "Rule: $ruleName"
if (!$PSCmdlet.ShouldProcess($program, $action)) { return }

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Open PowerShell as administrator, then run this script again. Use -WhatIf to preview without administrator rights.'
}
Import-Module NetSecurity -ErrorAction Stop
$existing = Get-NetFirewallRule -PolicyStore PersistentStore -Name $ruleName -ErrorAction SilentlyContinue
if ($existing -and $existing.Group -ne $group) {
    throw 'A rule with this name belongs to another group; it has not been changed.'
}
if ($Remove) {
    if ($existing) {
        $existing | Remove-NetFirewallRule
        Write-Host 'ROV video rule removed.'
    } else { Write-Host 'No ROV video rule exists for this application path.' }
    return
}
$settings = @{
    DisplayName = 'Sub Horizon ROV VR - video'
    Group = $group
    Description = 'ROV video only. Managed by Setup-Firewall.ps1.'
    Direction = 'Inbound'
    Action = 'Allow'
    Enabled = 'True'
    Profile = 'Any'
    Program = $program
    Protocol = 'UDP'
    LocalPort = $VideoPort
    RemotePort = 'Any'
    RemoteAddress = $RovAddress
    LocalAddress = 'Any'
    EdgeTraversalPolicy = 'Block'
}
if ($existing) {
    $settings.Remove('Group')
    $settings.Remove('DisplayName')
    $settings.NewDisplayName = 'Sub Horizon ROV VR - video'
    Set-NetFirewallRule -PolicyStore PersistentStore -Name $ruleName @settings | Out-Null
} else {
    New-NetFirewallRule -PolicyStore PersistentStore -Name $ruleName @settings | Out-Null
}
Write-Host "Ready: UDP $VideoPort from $RovAddress is allowed for this application. Windows Firewall remains enabled."
Write-Host 'If an explicit Block rule exists for this application, it takes precedence. This script does not change other rules.'
