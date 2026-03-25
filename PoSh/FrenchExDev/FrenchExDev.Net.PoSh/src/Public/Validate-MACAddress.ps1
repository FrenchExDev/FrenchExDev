# credit https://github.com/fleschutz/PowerShell/blob/main/docs/check-mac-address.md
function Validate-MACAddress {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    param([string]$MAC = "")

    $RegEx = "^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})|([0-9A-Fa-f]{2}){6}$"
    if ($mac -match $RegEx) {
        $true
    }
    else {
        $false
    }
}