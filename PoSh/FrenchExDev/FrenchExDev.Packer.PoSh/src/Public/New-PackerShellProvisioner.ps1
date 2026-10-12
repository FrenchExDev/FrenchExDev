function New-PackerShellProvisioner {
    [CmdletBinding()]
    param(
        [scriptblock] $Scripts,
        [object] $Override,
        [object] $PauseBefore
    )

    @{
        type           = "shell"
        scripts        = $(Invoke-Command $Scripts)
        override       = $Override
        "pause_before" = $PauseBefore
    }
}