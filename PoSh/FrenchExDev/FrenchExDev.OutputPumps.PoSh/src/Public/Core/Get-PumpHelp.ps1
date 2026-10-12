<#
.SYNOPSIS
Extract a bounded selection of local command help.
.DESCRIPTION
Define Get-PumpHelp for the OutputPumps runtime. Capture help through the process engine and return only the requested sections with explicit omission metadata.
#>


function Get-PumpHelp {
    <#
    .SYNOPSIS
    Extract a bounded selection of local command help.
    .DESCRIPTION
    Capture help through the process engine and return only the requested sections with explicit omission metadata.
    #>
    
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Executable, [string[]]$Arguments=@('--help'),
          [string]$Filter='--|^usage|^synopsis|^options|^commands', [string]$WorkingDirectory='.',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
    $capture=Invoke-PumpProcess $Executable $Arguments $WorkingDirectory $TimeoutSeconds
    Complete-PumpProcess $capture -Summary 'Selected local command documentation.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        $items=@(($c.Stdout + "`n" + $c.Stderr) -split '\r?\n' | Where-Object { $_ -match $Filter } | ForEach-Object { @{Section=$_.Trim()} })
        @{Items=$items;Source='LocalHelp'}
    }
}
