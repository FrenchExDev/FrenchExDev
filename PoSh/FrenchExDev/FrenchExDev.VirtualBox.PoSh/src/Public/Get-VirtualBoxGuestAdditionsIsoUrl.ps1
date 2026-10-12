function Get-VirtualBoxGuestAdditionsIsoUrl {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Version
    )

    "https://download.virtualbox.org/virtualbox/$version/VBoxGuestAdditions_$version.iso"
}
