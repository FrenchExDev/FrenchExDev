function Get-LastItemMatchingPattern {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrEmpty()] [parameter(Mandatory = $true, Position = 0)] [string] $Path,
        [ValidateNotNullOrEmpty()] [parameter(mandatory = $true, Position = 1)] [string] $Pattern
    )
    
    $childItems = Get-ChildItem $Path -Depth 0 -Directory

    if ($childItems.count -eq 0) {
        return 0
    }
    
    $captures = $childItems | Sort-Object -Property Name | Select-Object -Last | Select-String -Pattern $Pattern | Select-CaptureGroup

    if($captures.1) {
        $captures.1
        return
    }

    throw "error"
}
