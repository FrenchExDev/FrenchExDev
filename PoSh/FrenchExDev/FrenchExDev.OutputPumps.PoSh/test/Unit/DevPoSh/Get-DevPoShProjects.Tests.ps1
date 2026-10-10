BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Get-DevPoShProjects' {
    It 'deduplicates compatibility manifests and avoids cache dependencies' {
        foreach($name in @('Example','.cache/Ignored')){
            $path=Join-Path $TestDrive "$name/module.psd1";New-Item (Split-Path $path) -ItemType Directory -Force | Out-Null
            [IO.File]::WriteAllText($path,"@{RootModule='module.psm1';ModuleVersion='1.0'}")
        }
        [IO.File]::WriteAllText((Join-Path $TestDrive 'Example/compat.psd1'),"@{RootModule='module.psm1';ModuleVersion='1.0';PrivateData=@{DevPoSh=@{Primary=`$false}}}")
        InModuleScope 'DevPoSh.PoSh' -Parameters @{Path=$TestDrive} { @(Get-DevPoShProjects $Path).Count | Should -Be 1 }
    }
}
