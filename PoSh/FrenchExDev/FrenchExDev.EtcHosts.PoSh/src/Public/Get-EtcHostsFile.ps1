enum EtcHostsFileAction {
    GetFile
    GetContent
    SetContent
}

function Get-EtcHostsFile {
    [CmdletBinding()]
    param(
        [EtcHostsFileAction] $Action,
        [string] $Content,
        [ValidateNotNullOrWhiteSpace()] [string] $LinuxEtcHostsFile = "/etc/hosts",
        [ValidateNotNullOrWhiteSpace()] [string] $WindowsEtcHostsFile = "$env:windir/system32/drivers/etc/hosts"
    )

    $etcHostsFile = if ($IsLinux -or $IsMacOS) {
        $LinuxEtcHostsFile
    }
    elseif ($IsWindows) {
        $WindowsEtcHostsFile
    }

    switch ([EtcHostsFileAction] $Action) {
        ([EtcHostsFileAction]::GetFile) {
            $etcHostsFile
        }
        ([EtcHostsFileAction]::GetContent) {
            Get-Content $etcHostsFile
        }
        ([EtcHostsFileAction]::SetContent) {
            $content | Out-File $etcHostsFile -Encoding ascii
        }
    }
}