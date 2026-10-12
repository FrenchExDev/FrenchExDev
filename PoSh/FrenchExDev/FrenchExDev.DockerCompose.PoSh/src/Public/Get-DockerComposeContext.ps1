import-module powershell-yaml

enum DockerComposeContextAction {
    SetActive
    GetActive
    Get
    List
}

function Get-DockerComposeContext {
    param(
        [parameter(position = 0)] [DockerComposeContextAction[]] $Action,
        [parameter(Mandatory = $false)] [string] $Name,
        [ValidateNotNullOrWhiteSpace()] [string] $ContextsFile = "./dc-contexts.yaml"
    )

    foreach ($currentAction in $Action) {
        switch ([DockerComposeContextAction]$currentAction) {
            ([DockerComposeContextAction]::GetActive) {
                yq ".active" $ContextsFile
            }
            ([DockerComposeContextAction]::SetActive) {
                yq e -i ".active = ""$name""" $ContextsFile
            }
            ([DockerComposeContextAction]::Get) {
                yq ".contexts[] | select(.name == ""$Name"")" $ContextsFile | ConvertFrom-Yaml
            }
            ([DockerComposeContextAction]::List) {
                yq ".contexts[] | .name" $ContextsFile | ConvertFrom-Yaml
            }
            default {
                throw "Get-DockerComposeContext > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
