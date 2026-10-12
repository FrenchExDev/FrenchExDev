<#
.SYNOPSIS
Serialize each input envelope as bounded compact JSON.
.DESCRIPTION
Define ConvertTo-OutputPumpJson for the OutputPumps runtime. Sanitize and bound each result independently; use a task envelope when several calls share one context budget.
#>


function ConvertTo-OutputPumpJson {
    <#
    .SYNOPSIS
    Serialize each input envelope as bounded compact JSON.
    .DESCRIPTION
    Sanitize and bound each result independently; use a task envelope when several calls share one context budget.
    #>
    
    [CmdletBinding()]
    param([Parameter(Mandatory,ValueFromPipeline)]$InputObject,
          [ValidateRange(1,1000)][int]$MaxItems=20,[ValidateRange(1024,8192)][int]$MaxOutputBytes=8192)
    process {
        try {
            foreach ($field in @('Summary','Outcome','Data','ExitCode','Error','RetryAdvice','Truncation')) {
                if (!$InputObject.PSObject.Properties[$field]) { throw 'Expected an OutputPump envelope; passthrough is not agent-safe.' }
            }
            $result = Complete-OutputPumpResult $InputObject 'Serialized' $null $null $MaxItems $MaxOutputBytes
        } catch { $result=New-PumpResult -Summary 'Only structured pump results can be serialized.' -Outcome Failed -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='InvalidEnvelope'} }
        ConvertTo-Json -InputObject $result -Depth 24 -Compress
    }
}
