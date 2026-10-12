function Check-UserIsAdminOrThrow {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '',Scope='Function')]
    [CmdletBinding()]
    param()

    $elevated = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

    if($elevated -ne $true) {
        throw "not admin"
    }
}
