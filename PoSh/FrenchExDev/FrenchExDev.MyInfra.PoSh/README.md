# Infra

This folder contains the main automation scripts and configuration to bootstrap a software factory environment.

## Features

- **Packer Automation**: Build Alpine Linux images using VirtualBox and Packer.
- **Vagrant Integration**: Use Packer-built images with Vagrant to provision Docker Compose hosts or (WIP) Kubernetes clusters.
- **Script Library**: Includes scripts for network, SSL, and environment configuration.
- **Extensible**: With the FrenchExDev.PoSh module ecosystem, you can write your own Packer and Vagrant multi-machine automated setup scripts.

## Structure

- `src/` — Infrastructure configuration and templates.
- `test/` — Test environments and Vagrantfiles.
- Main scripts for building, configuring, and managing your infrastructure.

## Usage

Run the provided PowerShell scripts to automate image creation and environment provisioning for your development

## Networking

This is an example Networking script which provides information for cmdlets to behave as expected.

```powershell
[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $Configuration = "home",
    [ValidateNotNullOrWhiteSpace()] [string] $VmPrefix = "fexdev-infra",
    [ValidateNotNullOrWhiteSpace()] [string] $Domain = "frenchexdev.lab"
)

$hostsNames = @{
    "pihole"    = "pihole"
    "jb-00"     = "${VmPrefix}-jb-00"
    "cp-00"     = "${VmPrefix}-cp-00"
    "cp-01"     = "${VmPrefix}-cp-01"
    "cp-02"     = "${VmPrefix}-cp-02"
    "worker-00" = "${VmPrefix}-worker-00"
    "worker-01" = "${VmPrefix}-worker-01"
    "worker-02" = "${VmPrefix}-worker-02"
}

switch ($Configuration) {
    ("home") {

        $networkLinksConfig = @{
            "$($LocalHostLinksSymbols.Lan)"       = New-DevNetworkLinkConfig -NetMask "192.168.1.0/24" -Labels @($NetworkLinkKindsSymbols.Lan, $($NetworkManagersSymbols.Person.Invoke("serard"))) 
            "$($LocalHostLinksSymbols.HostOnly0)" = New-DevNetworkLinkConfig -NetMask "10.0.2.0/24" -Labels @($NetworkLinkKindsSymbols.VagrantInternal, $($NetworkManagersSymbols.Software.Invoke("virtualbox")))
            "$($LocalHostLinksSymbols.HostOnly1)" = New-DevNetworkLinkConfig -NetMask "10.100.1.0/24" -Labels @($NetworkLinkKindsSymbols.HostOnly, $($NetworkManagersSymbols.Software.Invoke("virtualbox")))
            "$($LocalHostLinksSymbols.HostOnly2)" = New-DevNetworkLinkConfig -NetMask "10.100.2.0/24" -Labels @($NetworkLinkKindsSymbols.HostOnly, $($NetworkManagersSymbols.Software.Invoke("virtualbox")))
        }

        $simplifiedNetworkHostsConfig = @{
            "$($hostsNames.pihole)"      = @{
                "ssh"        = @{
                    "public-key"  = "$env:USERPROFILE/.ssh/id_rsa.pub"
                    "private-key" = "$env:USERPROFILE/.ssh/id_rsa"
                }
                "labels"     = @(
                    "kind=pihole"
                )
                "interfaces" = @{
                    "eth0" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.Lan)"
                        "ip"        = "192.168.1.253"
                        "mac"       = ""
                    }
                }
                "features"   = @{
                    "dynamic-dns" = @{
                        "add"    = "$(Get-PiHoleDnsCommand Add)"
                        "remove" = "$(Get-PiHoleDnsCommand Remove)"
                    }
                }
            }
            "$($hostsNames.'jb-00')"     = @{
                "interfaces" = @{
                    "eth1" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.HostOnly1)"
                        "ip"        = "10.100.1.253"
                        "mac"       = ""
                    }
                    "eth2" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.Lan)"
                        "ip"        = "192.168.1.250"
                        "mac"       = ""
                    }
                }
                "labels"     = @(
                    "kubernetes#kind=$($KubernetesSymbols.MachinesKinds.JumpBox)"
                    "fqdn=jb-OO.$Domain"
                )
            }
            "$($hostsNames.'cp-00')"     = @{
                "interfaces" = @{
                    "eth1" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.HostOnly1)"
                        "ip"        = "10.100.1.2"
                        "mac"       = ""
                    }
                    "eth2" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.Lan)"
                        "ip"        = "192.168.1.2"
                        "mac"       = ""
                    }
                }
                "labels"     = @(
                    "kubernetes#kind=$($KubernetesSymbols.MachinesKinds.ControlPlan)"
                    "fqdn=cp-OO.$Domain"
                )
            }
            "$($hostsNames.'cp-01')"     = @{
                "interfaces" = @{
                    "eth1" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.HostOnly1)"
                        "ip"        = "10.100.1.3"
                        "mac"       = ""
                    }
                    "eth2" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.Lan)"
                        "ip"        = "192.168.1.3"
                        "mac"       = ""
                    }
                }
                "labels"     = @(
                    "kubernetes#kind=$($KubernetesSymbols.MachinesKinds.ControlPlan)"
                    "fqdn=cp-01.$Domain"
                )
            }
            "$($hostsNames.'cp-02')"     = @{
                "interfaces" = @{
                    "eth1" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.HostOnly1)"
                        "ip"        = "10.100.1.4"
                        "mac"       = ""
                    }
                    "eth2" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.Lan)"
                        "ip"        = "192.168.1.4"
                        "mac"       = ""
                    }
                }
                "labels"     = @(
                    "kubernetes#kind=$($KubernetesSymbols.MachinesKinds.ControlPlan)"
                    "fqdn=cp-02.$Domain"
                )
            }
            "$($hostsNames.'worker-00')" = @{
                "interfaces" = @{
                    "eth1" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.HostOnly2)"
                        "ip"        = "10.100.2.2"
                        "mac"       = ""
                    }
                    "eth2" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.Lan)"
                        "ip"        = "192.168.1.5"
                        "mac"       = ""
                    }
                }
                "labels"     = @(
                    "kubernetes#kind=$($KubernetesSymbols.MachinesKinds.Worker)"
                    "kubernetes#cidr=10.10.0.0/24"
                    "fqdn=worker-00.$Domain"
                )
            }
            "$($hostsNames.'worker-01')" = @{
                "interfaces" = @{
                    "eth1" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.HostOnly2)"
                        "ip"        = "10.100.2.3"
                        "mac"       = ""
                    }
                    "eth2" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.Lan)"
                        "ip"        = "192.168.1.6"
                        "mac"       = ""
                    }
                }
                "labels"     = @(
                    "kubernetes#kind=$($KubernetesSymbols.MachinesKinds.Worker)"
                    "kubernetes#cidr=10.10.1.0/24"
                    "fqdn=worker-01.$Domain"
                )
            }
            "$($hostsNames.'worker-02')" = @{
                "interfaces" = @{
                    "eth1" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.HostOnly2)"
                        "ip"        = "10.100.2.4"
                        "mac"       = ""
                    }
                    "eth2" = @{
                        "link_kind" = "$($LocalHostLinksSymbols.Lan)"
                        "ip"        = "192.168.1.7"
                        "mac"       = ""
                    }
                }
                "labels"     = @(
                    "kubernetes#kind=$($KubernetesSymbols.MachinesKinds.Worker)"
                    "kubernetes#cidr=10.10.2.0/24"
                    "fqdn=worker-02.$Domain"
                )
            }
        }

        $NewDevNetworkBuilderConfig = @{
            NetworkHostsConfig = $simplifiedNetworkHostsConfig
            NetworkLinksConfig = $networkLinksConfig
        }

        $networkHostsConfig = New-DevNetworkBuilder @NewDevNetworkBuilderConfig
    }
    default {
        throw "myNetwork.ps1 > Configuration '$Configuration' has not yet been implemented."
    }
}

New-DevNetwork -Config { $networkLinksConfig } -HostConfig { $networkHostsConfig }

```

Copy this content into your own `00-myDevNetwork.ps1`, make sure to ajust MAC addresses.
