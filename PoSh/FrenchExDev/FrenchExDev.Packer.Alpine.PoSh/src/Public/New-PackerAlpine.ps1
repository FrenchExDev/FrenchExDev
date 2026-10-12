function New-PackerAlpine {
    param(
        [int] $Cpus = 4,
        [double] $Memory = 256MB,
        [double] $VideoMemory = 64MB,
        [long] $DiskSize = 20GB,
        [ValidateNotNullOrWhiteSpace()] [string] $BootWait = "10s",
        [ValidateNotNullOrWhiteSpace()] [string] $SshTimeout = "10m",
        [ValidateNotNullOrWhiteSpace()] [string] $CdnRepository = "dl-cdn.alpinelinux.org",
        [ValidateNotNullOrWhiteSpace()] [string] $BoxKind,
        [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion = "1.0.0",
        [ValidateNotNullOrWhiteSpace()] [string] $AlpineVersion = $AlpineSymbols.Versions.LatestEdge,
        [ValidateNotNullOrWhiteSpace()] [string] $Arch = $AlpineSymbols.Architectures.x86_64,
        [ValidateNotNullOrWhiteSpace()] [string] $Flavor = $AlpineSymbols.Flavors.Virt,
        [ValidateNotNullOrWhiteSpace()] [string] $OutputVagrant = "output-vagrant",
        [ValidateNotNullOrWhiteSpace()] [string] $OutputVagrantBoxName = "alpine.box",
        [ValidateNotNullOrWhiteSpace()] [string] $RootPasswd = "vagrant",
        [ValidateNotNullOrWhiteSpace()] [string] $SshPasswd = "vagrant",
        [ValidateNotNullOrWhiteSpace()] [string] $SshUsername = "vagrant",
        [ValidateNotNullOrWhiteSpace()] [string] $Author,
        [ValidateNotNullOrWhiteSpace()] [string] $VagrantPostProcessorOutput = '{{user `output_vagrant` }}/{{user `output_vagrant_box_name` }}.box',
        [ValidateNotNullOrWhiteSpace()] [string] $VagrantPostProcessorVagrantfileTemplate = "./vagrant/Vagrantfile",
        [ValidateNotNullOrWhiteSpace()] [string] $Description,
        [ValidateNotNullOrWhiteSpace()] [string] $PackerFile,
        [ValidateNotNullOrWhiteSpace()] [string] $WorkingDirectory,
        [scriptblock] $ProvisioningFilesScript,
        [scriptblock] $ProvisionersScript,
        [scriptblock] $VBoxManageScript,
        [scriptblock] $VariablesScript
    )

    $VerboseAndDebug = @{
        Verbose = $VerbosePreference
        Debug   = $DebugPreference
    }

    $virtualBoxVersion = Get-VirtualBoxVersion @VerboseAndDebug
    $virtualBoxGuestAdditionsIsoSha256 = Get-VirtualBoxGuestAdditionsIsoChecksum -Version $virtualBoxVersion @VerboseAndDebug
    $alpineIsoUrl = Get-AlpineBoxIsoUrl -Version $AlpineVersion -Arch $Arch -Flavor $Flavor @VerboseAndDebug
    $alpineIsoSha256 = Get-AlpineBoxIsoChecksum -Version $AlpineVersion -Arch $Arch -Flavor $Flavor @VerboseAndDebug

    $boxDistro = "alpine"
    $boxDistroVersion = "${AlpineVersion}-$BoxKind"
    $boxVmName = "${boxDistro}-${boxDistroVersion}"

    $boxDistroMainVersion = "$($AlpineVersion.split(".")[0]).$($AlpineVersion.split(".")[1])"

    $InternalVagrantFilesScript = {
        @{
            "metadata.json" = @{
                content = @(@{
                        provider     = "virtualbox"
                        architecture = $VirtualBoxSymbols.Architectures.amd64
                    } | ConvertTo-Json)
            }
            "info.json"     = @{
                content = $(@{
                        Author      = "$Author"
                        Website     = ""
                        Artifacts   = ""
                        Repository  = ""
                        Description = ""
                    } | ConvertTo-Json)
            }
            "Vagrantfile"   = @(
                'Vagrant.configure(''2'') do |config|'
                '    config.vm.provider ''virtualbox'' do |v|'
                '        v.cpus = 2'
                '        v.memory = "256"'
                '    end'
                'end'
            ) -Join " "
        }
    }.GetNewClosure()

    $InternalHttpFilesScript = {
        $Interfaces = @(
            'auto lo'
            'iface lo inet loopback'
            'auto eth0'
            'iface eth0 inet dhcp'
            '    hostname alpine'
            '    domain local'
            ''
        ) -Join [System.Environment]::NewLine

        $InternalAnswersFileContentContent = @{
            KeyMap     = "us us"
            HostName   = "alpine"
            DevDevice  = "mdev"
            Interfaces = $Interfaces
            TimeZone   = "UTC"
            Proxy      = "none"
            ApkRepos   = "-1"
            Sshd       = "-c openssh"
            Ntp        = "-c openntpd"
            Disk       = "-m sys /dev/sda"
            Lbu        = "none"
            ApkCache   = "none"
            EraseDisks = "/dev/sda"
        }

        @{
            "answers"  = @{
                content = New-PackerAlpineAnswerFileContent @InternalAnswersFileContentContent @VerboseAndDebug
            }
            "ssh.keys" = @{
                content = Get-VagrantInsecurePublicKey @VerboseAndDebug
            }
        }
    }.GetNewClosure()


    $filesSymbols = @{
        base           = "00base"
        alpine         = "01alpine"
        networking     = "01networking"
        sshd           = "02sshd"
        vagrant        = "03vagrant"
        sudoers        = "04sudoers"
        cron           = "05cron"
        vba            = "08virtualbox-guest-additions"
        disablesshroot = "99disable-ssh-root"
        minimize       = "99minimize"
        reboot         = "99reboot"
    }

    $InternalProvisioningFilesScript = {
        $WorkingProvisioningFiles = @{
            "$($filesSymbols.base)"           = New-BashFile -Code { New-AlpineMotd }
            "$($filesSymbols.alpine)"         = New-BashFile -NoShebang -Code { @(
                    "set -ux"
                    'cat >/etc/apk/repositories <<EOT'
                    "$(New-AlpineRepository -CdnRepository $CdnRepository -MajorMinorVersion $boxDistroMainVersion -Main -Community -EdgeMain -EdgeCommunity)"
                    'EOT'
                    "$(New-ApkCommand Update)"
                    "$(New-ApkCommand Add -Upgrade -Package "apk-tools")"
                    "$(New-ApkCommand Upgrade -Available)"
                    "$(New-ApkCommand Add -Package "bash","bash-completion","sudo","doas","sed","nano")"
                    'cat >/etc/sysctl.d/00-alpine.conf <<EOT'
                    'net.ipv4.ip_forward = 1'
                    'net.ipv4.tcp_syncookies = 1'
                    'net.ipv4.conf.default.rp_filter = 0'
                    'net.ipv4.conf.all.rp_filter = 0'
                    'net.ipv4.ping_group_range=0 2147483647'
                    'kernel.panic = 120'
                    'EOT'
                    'sed -i ''/swap/s/^/#/'' /etc/fstab'
                    'swapoff -a'
                    'exit 0'
                ) 
            }
            "$($filesSymbols.networking)"     = New-BashFile -Code { @() }
            "$($filesSymbols.sshd)"           = New-BashFile -Code { @(
                    'echo "UseDNS no" >> /etc/ssh/ssh_config.d/00-disallow-dns'
                    'echo "AllowTcpForwarding yes" >> /etc/ssh/ssh_config.d/00-allow-forwarding'
                    'exit 0'
                ) 
            }
            "$($filesSymbols.vagrant)"        = New-BashFile -Code { @(
                    'date > /etc/vagrant_box_build_time'
                    'echo "vagrant:vagrant" | chpasswd'
                    'mkdir -pm 700 /home/vagrant/.ssh'
                    'wget -O /home/vagrant/.ssh/authorized_keys https://raw.githubusercontent.com/mitchellh/vagrant/master/keys/vagrant.pub'
                    'chown -R vagrant:vagrant /home/vagrant/.ssh'
                    'chmod -R go-rwsx /home/vagrant/.ssh'
                    'echo "Use the bash shell for vagrant and root"'
                    'sed -e ''s@/bin/ash@/bin/bash@'' -i /etc/passwd'
                    'exit 0'
                ) 
            }
            "$($filesSymbols.sudoers)"        = New-BashFile -Code { @(
                    'adduser vagrant wheel'
                    'echo "Defaults exempt_group=wheel" > /etc/sudoers'
                    'echo "%wheel ALL=NOPASSWD:ALL" >> /etc/sudoers'
                    'exit 0'
                ) 
            }
            "$($filesSymbols.cron)"           = New-BashFile -Code { @(
                    'echo "Adding a more regular 1min cron category"'
                    'echo "*       *       *       *       *       run-parts /etc/periodic/1min" >>/etc/crontabs/root'
                    'mkdir -p /etc/periodic/1min'
                    'exit 0'
                )
            }
            "$($filesSymbols.vba)"            = New-BashFile -Code { @(
                    "$(New-ApkCommand Add -Package "virtualbox-guest-additions","virtualbox-guest-modules-virt")"
                ) 
            }
            "$($filesSymbols.disablesshroot)" = New-BashFile -Code { @(
                    'sed ''/PermitRootLogin yes/d'' -i /etc/ssh/sshd_config'
                    'echo "UseDNS no" >> /etc/ssh/sshd_config'
                ) 
            } 
            "$($filesSymbols.minimize)"       = New-BashFile -Parameters "" -Code { @(
                    'set -ux'
                    'echo "Clean up things (tmp, logs, apk cache)"'
                    'rm -rf /tmp/* /var/log/* /var/cache/apk/*'
                    'apk cache purge'
                    'echo Whiteout root'
                    'count=$(df -kP / | tail -n1  | awk -F '' '' ''{print $4}'')'
                    'count=$(($count-1))'
                    'dd if=/dev/zero of=/whitespace bs=1M count=$count || echo "dd exit code $? is suppressed";'
                    'rm /whitespace'
                    'echo Whiteout /boot'
                    'count=$(df -kP /boot | tail -n1 | awk -F '' '' ''{print $4}'')'
                    'count=$(($count-1))'
                    'dd if=/dev/zero of=/boot/whitespace bs=1M count=$count || echo "dd exit code $? is suppressed";'
                    'rm /boot/whitespace'
                    'set +e'
                    'swapuuid="`/sbin/blkid -o value -l -s UUID -t TYPE=swap`";'
                    'case "$?" in'
                    '2|0) ;;'
                    '*) exit 1 ;;'
                    'esac'
                    'set -e'
                    'if [ "x${swapuuid}" != "x" ]; then'
                    'swappart="`readlink -f /dev/disk/by-uuid/$swapuuid`";'
                    '/sbin/swapoff "$swappart" || true;'
                    'dd if=/dev/zero of="$swappart" bs=1M || echo "dd exit code $? is suppressed";'
                    '/sbin/mkswap -L "$swapuuid" "$swappart";'
                    'fi'
                    'sync;'
                    'sync;'
                    'sync;'
                    'exit 0'
                ) 
            }  
            "$($filesSymbols.reboot)"         = New-BashFile -Code { @(
                    'echo Rebooting'
                    'reboot'
                    'exit 0'
                ) 
            } 
        }

        if ($null -ne $ProvisioningFilesScript) {
            $WorkingProvisioningFiles = Invoke-Command $ProvisioningFilesScript -ArgumentList @($WorkingProvisioningFiles)
        }

        $WorkingProvisioningFiles
    }.GetNewClosure()

    $InternalBuildersScript = {

        $PackerVirtualBoxIsoBuilderConfig = @{
            BootCommandScript      = {
                @(
                    'root<enter><wait>',
                    'ifconfig eth0 up && udhcpc -i eth0<enter><wait2>',
                    'wget -O $PWD/answers http://{{ .HTTPIP }}:{{ .HTTPPort }}/answers<enter><wait>',
                    "export USEROPTS='-a -u -g audio,video,netdev {{user ``ssh_username``}}'<enter>",
                    "export USERSSHKEY='http://{{ .HTTPIP }}:{{ .HTTPPort }}/ssh.keys'<enter>",
                    "setup-alpine -f `$PWD/answers<enter><wait5>",
                    '{{user `root_password`}}<enter><wait>',
                    '{{user `root_password`}}<enter><wait10>',
                    "mount /dev/sda3 /mnt<enter>",
                    "echo 'PermitRootLogin yes' >> /mnt/etc/ssh/sshd_config<enter>",
                    "umount /mnt; reboot<enter>"
                )
            }
            VBoxManageScript       = $VBoxManageScript
            HardDriveInterface     = $VirtualBoxSymbols.Storage.Sata
            HardDriveDiscard       = $true
            HardDriveNonRotational = $true
            NestedVirt             = $true
        }

        @(, , $(New-PackerVirtualBoxIsoBuilder @PackerVirtualBoxIsoBuilderConfig))

    }.GetNewClosure()

    $InternalProvisionersScript = {

        $scripts = @{
            "before"     = @(
                "scripts/$($filesSymbols.base).sh",
                "scripts/$($filesSymbols.alpine).sh",
                "scripts/$($filesSymbols.networking).sh",
                "scripts/$($filesSymbols.sshd).sh",
                "scripts/$($filesSymbols.vagrant).sh",
                "scripts/$($filesSymbols.sudoers).sh",
                "scripts/$($filesSymbols.cron).sh"
            )
            "additional" = @()
            "after"      = @(
                "scripts/$($filesSymbols.disablesshroot).sh"
                "scripts/$($filesSymbols.minimize).sh"
            )
        }

        if ($null -ne $ProvisionersScript) {
            $scripts = $(Invoke-Command $ProvisionersScript -ArgumentList @($scripts))
        }

        $scripts = $scripts.before + $scripts.additional + $scripts.after

        $WorkingProvisioner = New-PackerShellProvisioner -Scripts { $scripts } -PauseBefore $null -Override @{
            "virtualbox-iso" = @{
                execute_command = "{{.Vars}} /bin/sh {{.Path}}"
            }
        }

        $WorkingProvisioners = @(, , $WorkingProvisioner)

        $WorkingProvisioners
    }.GetNewClosure()

    $InternalPostProcessorsScript = {

        $PackerVagrantPostProcessorConfig = @{
            CompressionLevel    = 9
            Output              = $VagrantPostProcessorOutput
            VagrantFileTemplate = $VagrantPostProcessorVagrantfileTemplate
        }

        @(, , $(New-PackerVagrantPostProcessor @PackerVagrantPostProcessorConfig))
    }.GetNewClosure()

    $InternalVariablesScript = {

        $InternalVariables = @{
            "output_vagrant"                  = "$OutputVagrant"
            "output_vagrant_box_name"         = $OutputVagrantBoxName
            "box_version"                     = "$BoxVersion"
            "vbox_version"                    = "$virtualBoxVersion"
            "vbox_guest_additions_iso_sha256" = "$virtualBoxGuestAdditionsIsoSha256"
            "community_repo"                  = "http://${CdnRepository}/alpine/v${boxDistroMainVersion}/community"
            "cpus"                            = "$Cpus"
            "disk_size"                       = "$($DiskSize / 1MB)"
            "iso_checksum"                    = "$alpineIsoSha256"
            "iso_checksum_type"               = "sha256"
            "iso_download_url"                = "$alpineIsoUrl"
            "iso_local_url"                   = "./cache/iso/alpine-$Flavor-$AlpineVersion-$Arch.iso"
            "memory"                          = "$([int] $($Memory / 1MB))"
            "root_password"                   = "$RootPasswd"
            "ssh_password"                    = "$SshPasswd"
            "ssh_username"                    = "$SshUsername"
            "video_memory"                    = "$([int] $($VideoMemory / 1MB))"
            "vm_name"                         = "$boxVmName"
            "boot_wait"                       = "$BootWait"
            "ssh_timeout"                     = "$SshTimeout"
        }

        if ($null -ne $VariablesScript) {
            $InternalVariables = Invoke-Command $VariablesScript -ArgumentList $InternalVariables
        }

        $InternalVariables
    }.GetNewClosure()

    $PackerAlpineHost = @{
        VagrantFilesScript      = $InternalVagrantFilesScript
        HttpFilesScript         = $InternalHttpFilesScript
        ProvisioningFilesScript = $InternalProvisioningFilesScript
        BuildersScript          = $InternalBuildersScript
        ProvisionersScript      = $InternalProvisionersScript
        PostProcessorsScript    = $InternalPostProcessorsScript
        VariablesScript         = $InternalVariablesScript
    }

    if (![string]::IsNullOrEmpty($PackerFile) -and ![string]::IsNullOrEmpty($WorkingDirectory) -and ![string]::IsNullOrEmpty($Description)) {
        $PackerAlpineConfig = @{
            Description      = $Description
            WorkingDirectory = $WorkingDirectory
            PackerFile       = $PackerFile
        }

        New-Packer @PackerAlpineConfig @PackerAlpineHost

        return
    }

    $PackerAlpineHost
}
