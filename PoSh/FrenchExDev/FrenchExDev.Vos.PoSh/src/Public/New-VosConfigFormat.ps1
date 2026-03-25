function New-VosConfigFormat {
    [CmdletBinding()]
    param(
        [int] $Zeroes
    )

    [pscustomobject] @{
        vagrant = "%0${Zeroes}d"
        posh    = "{0:d${Zeroes}}"
    }
}
