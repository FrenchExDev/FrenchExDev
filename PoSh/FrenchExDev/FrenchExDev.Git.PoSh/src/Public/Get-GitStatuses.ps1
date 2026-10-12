# Credit https://markb.uk/powershell-git-check-status-multiple-working-copies.html
function Get-GitStatuses {
    [CmdletBinding()]
    param(
        [parameter(Position = 0)] [string] $Dir = './' 
    )
    Get-ChildItem -Path $Dir -Attributes Directory+Hidden -Recurse -Filter '.git' `
    | ForEach-Object { Get-GitStatus -WorkingDirectory $_ } `
    | Format-Table -Wrap -AutoSize -RepeatHeader
}
