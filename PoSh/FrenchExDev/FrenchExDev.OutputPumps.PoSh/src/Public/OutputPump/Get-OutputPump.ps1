<#
.SYNOPSIS
Inspect active pump registrations without exposing captured state.
.DESCRIPTION
Define Get-OutputPump for the OutputPumps runtime. Return selected route and provider metadata while keeping closures and their captured variables private.
#>


function Get-OutputPump {
    <#
    .SYNOPSIS
    Inspect active pump registrations without exposing captured state.
    .DESCRIPTION
    Return selected route and provider metadata while keeping closures and their captured variables private.
    #>
    
    [CmdletBinding()]
    param([string]$Id='*',[string]$ProviderId='*',[ValidateRange(1,1000)][int]$MaxItems=20,[ValidateRange(1024,8192)][int]$MaxOutputBytes=8192)
    try {
        $items=@(Get-OutputPumpRegistry | Where-Object {$_.Id -like $Id -and $_.ProviderId -like $ProviderId} | Sort-Object Id | ForEach-Object {
            # ArgumentPrefix is routing metadata; calling it Tokens would trigger secret redaction.
            @{Id=$_.Id;ProviderId=$_.ProviderId;Origin=$_.Origin;Function=$_.Function;Executable=$(if($_.Route){$_.Route.Executable}else{$null});ArgumentPrefix=@($(if($_.Route){$_.Route.Tokens}));Overridden=($script:OutputPumpSession.Registry[$_.Id].Count -gt 1)}
        })
        New-PumpResult -Summary 'Active OutputPumps registrations.' -Data @{Items=$items} -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
    }catch{New-PumpResult -Summary 'OutputPumps registry is unavailable.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='RegistryNotInitialized'}}
}
