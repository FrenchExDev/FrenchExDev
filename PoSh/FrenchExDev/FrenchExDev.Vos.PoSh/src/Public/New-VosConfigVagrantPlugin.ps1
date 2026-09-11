function New-VosConfigVagrantPlugin {
    [CmdletBinding()]
    param(
        [switch] $Enable,
        [scriptblock] $Config
    )

    [pscustomobject] @{
        enabled = $false
        config  = Invoke-Command $Config
    }
}
