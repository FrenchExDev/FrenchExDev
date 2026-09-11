function Get-VirtualBoxVersion {
    [CmdletBinding()]
    param()

    $version = $($(invoke-expression "push-location 'C:\Program Files\Oracle\VirtualBox\'; ./VBoxManage.exe -version; pop-location") -split "r")[0]

    Write-Debug "Get-VirtualBoxVersion > Found version '$version'"

    $version
}
