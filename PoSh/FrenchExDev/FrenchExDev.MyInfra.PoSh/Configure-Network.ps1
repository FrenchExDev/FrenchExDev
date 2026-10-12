[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $Configuration = "home",
    [ValidateNotNullOrWhiteSpace()] [string] $VmPrefix = "fexdev-infra",
    [ValidateNotNullOrWhiteSpace()] [string] $OutFile = "./src/config-network.yaml"
)

. ./00-myDevNetwork.ps1 -Configuration $Configuration -VmPrefix $VmPrefix | ConvertTo-Yaml | Out-File $OutFile -encoding ascii
