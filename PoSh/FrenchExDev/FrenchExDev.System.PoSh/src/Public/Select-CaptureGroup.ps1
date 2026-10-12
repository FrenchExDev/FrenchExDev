function Select-CaptureGroup {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
        [ValidateNotNullOrEmpty()]
        [Microsoft.PowerShell.Commands.MatchInfo[]]$InputObject
    )
    
    begin {}
    
    process {
        foreach ($input in $InputObject) {
            $input.Matches | ForEach-Object {
                $groupedOutput = New-Object -TypeName "PSCustomObject"
                $_.Groups | Where-Object  Name -ne "0" | ForEach-Object {
                    Add-Member -InputObject $groupedOutput -MemberType NoteProperty -Name $_.Name -Value $_.Value
                }
                $groupedOutput
            }
        }
    }
    
    end {}
}
