function New-VsCodeWorkspaceFolder {
    param(
        [parameter(mandatory = $true, position = 0)] [string] $Name,
        [parameter(mandatory = $true, position = 1)] [string] $Path,
        [parameter(mandatory = $true, position = 2)] [string] $VsCodeWorkspaceName,
        [object] $VsCodeWorkspace,
        [switch] $NoSave        
    )

    $VsCodeWorkspace = if ($null -eq $VsCodeWorkspace) { Get-VsCodeWorkspace -Name $VsCodeWorkspaceName } else { $VsCodeWorkspace }

    $VsCodeWorkspace.folders += @(@{
            path = $path
            name = $name
        })

    if (!$NoSave) {
        Save-VsCodeWorkspace -VsCodeWorkspace $VsCodeWorkspace -Name $VsCodeWorkspaceName
    }
}
