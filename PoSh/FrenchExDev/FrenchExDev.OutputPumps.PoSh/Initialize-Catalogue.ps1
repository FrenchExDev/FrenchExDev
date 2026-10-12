<#
.SYNOPSIS
Initialize the reusable software catalogue.
.DESCRIPTION
Use the maintained catalogue script and command descriptors to produce consistent index and software documentation.
#>


#requires -Version 7.4
& (Join-Path $PSScriptRoot 'src/Scripts/Initialize-Catalogue.ps1') @args
