function Get-VirtualBoxGuestAdditionsIsoChecksum {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Version
    )

    $($(invoke-webrequest "https://download.virtualbox.org/virtualbox/$version/SHA256SUMS").Content | Select-String -Pattern "(.*) \*VBoxGuestAdditions_$version.iso" | Select-CaptureGroup).1
}
