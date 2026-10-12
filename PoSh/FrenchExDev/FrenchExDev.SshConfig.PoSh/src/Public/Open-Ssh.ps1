function Open-Ssh {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrEmpty()] [parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)] [string] $HostName
    )

    ssh $HostName
}
