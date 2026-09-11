param(
    [ValidateSet("Restart", "Reset")] [string]$Action = "Restart"
)

function Restart-ClaudeVm {
    taskkill /F /IM Claude.exe 2>$null
    taskkill /F /IM claude.exe 2>$null
    net stop CoworkVMService
    net start CoworkVMService
}

function Reset-ClaudeVm {
    net stop CoworkVMService

    rmdir /S /Q "%APPDATA%\Claude\claude-code-vm" 2>$null
    rmdir /S /Q "%APPDATA%\Claude\vm_bundles" 2>$null

    net start CoworkVMService
}

switch ($Action) {
    'Restart' { Restart-ClaudeVm }
    'Reset' { Reset-ClaudeVm }
}
