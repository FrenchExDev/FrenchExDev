<#
.SYNOPSIS
Install the managed OutputPumps agent instruction block.
.DESCRIPTION
Preserve unrelated user instructions and keep profile activation a separate, explicit user action.
#>


#requires -Version 7.4
& (Join-Path $PSScriptRoot 'src/Scripts/Install-OutputPumps.ps1') @args
