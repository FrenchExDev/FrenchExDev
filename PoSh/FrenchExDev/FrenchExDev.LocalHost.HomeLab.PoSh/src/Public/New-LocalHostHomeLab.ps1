function New-LocalHostDevLab {
[CmdletBinding()]
param(
    [switch] $NoPacker,
    [switch] $NoVos,
    [switch] $NoDos,
    [switch] $NoSsl,
    [scriptblock] $GeneratePacker,
    [scriptblock] $GenerateVos,
    [scriptblock] $GenerateDos,
    [scriptblock] $BuildPacker,
    [scriptblock] $ConfigureSsl,
    [scriptblock] $Setup
)

    if($false -eq $NoPacker) {
        Invoke-Command $GeneratePacker
    }

    if($false -ne $NoVos) {
        Invoke-Command $GenerateVos
    }
    
    
    Invoke-Command $GenerateDos
    Invoke-Command $BuildPacker
    Invoke-Command $ConfigureSsl
    Invoke-Command $Setup


}