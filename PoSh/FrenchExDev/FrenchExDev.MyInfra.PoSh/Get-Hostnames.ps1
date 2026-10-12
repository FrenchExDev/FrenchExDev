[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $Prefix = "fexdev-infra"
)

@{
    "jb-00"     = "${Prefix}-jb-00"
    "cp-00"     = "${Prefix}-cp-00"
    "cp-01"     = "${Prefix}-cp-01"
    "cp-02"     = "${Prefix}-cp-02"
    "worker-00" = "${Prefix}-worker-00"
    "worker-01" = "${Prefix}-worker-01"
    "worker-02" = "${Prefix}-worker-02"
}