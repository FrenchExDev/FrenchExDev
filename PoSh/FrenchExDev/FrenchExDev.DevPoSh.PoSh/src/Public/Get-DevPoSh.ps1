enum PoShAction {
    Pwsh
    Command
    Ide
    Compile
}

function Get-DevPoSh {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, Position = 0, ValueFromPipelineByPropertyName = $true)] [string] $Acme,
        [parameter(Mandatory = $true, Position = 1, ValueFromPipelineByPropertyName = $true)] [string] $Name,
        [parameter(Mandatory = $true, Position = 2, ValueFromPipelineByPropertyName = $true)] [PoShAction[]] $Action,
        [string] $Command,
        [string] $Ide,
        [string] $Path
    )

    $projectDir = "./PoSh/$Acme/$Name/$Path"

    if (!(test-path $projectDir)) {
        throw "Get-DevPoSh > Project Directory $projectDir does not exist"
    }

    foreach ($currentAction in $Action) {
        [PoShAction] $currentActionEnum = $currentAction
        switch ($currentActionEnum) {
            ([PoShAction]::Command) {
                if ([string]::IsNullOrEmpty($Command)) {
                    throw "Get-DevPoSh > -Command is empty"
                }
                pwsh -Login -WorkingDirectory $projectDir -Command "$Command"
            }
            ([PoShAction]::Pwsh) {
                pwsh -Login -WorkingDirectory $projectDir
            }
            ([PoShAction]::Ide) {
                Get-Ide $Ide -open $projectDir -WorkingDirectory $projectDir
            }
            ([PoShAction]::Compile) {
                Write-Debug "Get-DevPoSh > Path dir : $pathdir > Compiling"
                $compilationPath = "$env:USERPROFILE/.posh.posh.compilations/$acme/$Name"
                if (test-path $compilationPath) {
                    Remove-Item -Recurse -Force $compilationPath
                }
                New-Item $compilationPath -ItemType Directory
                Push-Location $projectDir
                $publicFunctions = Get-ChildItem -Path "./src/Public" -Recurse -Filter "*.ps1" | Select-Object -ExpandProperty FullName
                $privateFunctions = Get-ChildItem -Path "./src/Private" -Recurse -Filter "*.ps1" | Select-Object -ExpandProperty FullName
                $classes = Get-ChildItem -Path "./src/Classes" -Recurse -Filter "*.ps1"  | Select-Object -ExpandProperty FullName
                $completers = Get-ChildItem -Path "./src/Completers" -Recurse -Filter "*.ps1" | Select-Object -ExpandProperty FullName
                $moduleData = [pscustomobject] @{
                    public     = $publicFunctions
                    private    = $privateFunctions
                    classes    = $classes
                    completers = $completers
                }
                $moduleData | ConvertTo-Json | Out-File $compilationPath/compiled.json -Encoding ascii
                Pop-Location
            }
        }
    }
}
