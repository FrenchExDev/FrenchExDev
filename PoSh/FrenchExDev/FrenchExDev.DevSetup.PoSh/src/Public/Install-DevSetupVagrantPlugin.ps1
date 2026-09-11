function Install-DevSetupVagrantPlugin {
    [CmdletBinding()]
    param(
        [switch] $Reload
    )

    if ($Reload) {
        vagrant plugin install vagrant-reload vagrant-hostmanager vagrant-env vagrant-vbguest
    }
}