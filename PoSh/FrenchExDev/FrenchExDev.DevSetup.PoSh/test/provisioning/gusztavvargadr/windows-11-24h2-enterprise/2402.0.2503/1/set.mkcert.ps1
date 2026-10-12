$ahkScript = @"
#Requires AutoHotkey v2.0
Run "pwsh mkcert-install.ps1"
Sleep 2000
Send "{TAB} {Enter}"
"@

$ahkScript | out-file $env:USERPROFILE/.mkcert.install.ahk

$mkcertPs1 = @'
$env:CAROOT = "c:/mkcert/"
mkcert -install
'@

$mkcertPs1 | Out-File $env:USERPROFILE/mkcert-install.ps1

Set-ExecutionPolicy bypass -scope Process

Start-Process "C:\Program Files\AutoHotkey\v2\AutoHotkey.exe" -ArgumentList @("$env:USERPROFILE/.mkcert.install.ahk")
