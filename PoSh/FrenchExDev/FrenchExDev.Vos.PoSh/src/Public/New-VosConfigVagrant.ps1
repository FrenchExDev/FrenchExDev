function New-VosConfigVagrant {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory, Position = 0)] [string] $NamingPatternPrefix,
        [switch] $PrefixWithDirBase,
        [scriptblock] $Plugins,
        [scriptblock] $Manage,
        [scriptblock] $StorageAttach,
        [scriptblock] $StorageControl,
        [scriptblock] $SetProperty,
        [switch] $NoLinkedClones,
        [switch] $NoCheckGuestAdditions
    )

    $VosConfigVagrantVirtualBoxConfig = @{
        LinkedClones        = -not $NoLinkedClones
        CheckGuestAdditions = -not $NoCheckGuestAdditions
        Manage              = $Manage
        StorageAttach       = $StorageAttach
        storageControl     = $StorageControl
        SetProperty         = $SetProperty
    }

    @{
        prefix_with_dirbase = [bool] $PrefixWithDirBase.IsPresent ? $true : $false
        "naming-pattern"    = "$NamingPatternPrefix-#MACHINE-NAME#-#MACHINE-INSTANCE#"
        plugins             = if ($null -ne $Plugins) { Invoke-Command $Plugins }
        virtualbox          = New-VosConfigVagrantVirtualBox @VosConfigVagrantVirtualBoxConfig
    }
}
