function Get-PumpLog {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path, [string]$Filter,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
    Get-PumpFile -Path $Path -Tail -Filter $Filter -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -TimeoutSeconds $TimeoutSeconds
}
