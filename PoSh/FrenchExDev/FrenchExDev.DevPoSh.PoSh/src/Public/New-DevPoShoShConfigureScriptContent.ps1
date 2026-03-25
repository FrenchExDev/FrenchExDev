function New-DevPoShoShConfigureScriptContent {
    [CmdletBinding()]
    param(

    )
    New-Bash -Parameters "-eux" -Code {
        @(
            'pwsh -Login -Command ''New-Item -ItemType Directory -Force $(split-path $profile -parent)'''
            'sudo pwsh -Login -Command ''New-Item -ItemType Directory -Force $(split-path $profile -parent)'''
            'pwsh -Login -Command '''
            '   Import-Module /posh/FrenchExDev/DevPoSh.PoSh/DevPoSh.PoSh.psd1'
            '   Set-DevPoShPowershellProfile -MyPoShPath /posh -LinuxShellProfile $profile'
            '   '''
            'sudo pwsh -Login -Command '''
            '   Import-Module /posh/FrenchExDev/DevPoSh.PoSh/DevPoSh.PoSh.psd1'
            '   Set-DevPoShPowershellProfile -MyPoShPath /posh -LinuxShellProfile $profile'
            '   '''
        )
    }  
}
