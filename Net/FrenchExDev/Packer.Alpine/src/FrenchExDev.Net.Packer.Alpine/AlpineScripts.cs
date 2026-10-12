namespace FrenchExDev.Net.Packer.Alpine;

/// <summary>
/// Alpine Linux provisioning script content.
/// Each script is a shell script executed during the Packer build.
/// Numbered prefix controls execution order.
/// </summary>
public static class AlpineScripts
{
    public const string Base = """
        #!/bin/sh
        set -eux
        echo '*** Alpine Base Setup ***'
        cat > /etc/motd << 'MOTD'
         _    _      _                            _           _    _       _
        | |  | |    | |                          | |         | |  | |     (_)
        | |  | | ___| | ___ ___  _ __ ___   ___  | |_ ___    | |  | |_ __  ___  __
        | |/\| |/ _ \ |/ __/ _ \| '_ ` _ \ / _ \ | __/ _ \   | |/\| | '_ \| \ \/ /
        \  /\  /  __/ | (_| (_) | | | | | |  __/ | || (_) |  \  /\  / |_) | |>  <
         \/  \/ \___|_|\___\___/|_| |_| |_|\___|  \__\___/    \/  \/ .__/|_/_/\_\
                                                                    | |
                                                                    |_|
        MOTD
        """;

    public const string Alpine = """
        #!/bin/sh
        set -eux
        echo '*** Alpine Packages ***'
        apk update
        apk upgrade
        apk add bash curl wget ca-certificates openssl sudo shadow
        """;

    public const string Networking = """
        #!/bin/sh
        set -eux
        echo '*** Networking ***'
        # Placeholder for network configuration
        """;

    public const string Sshd = """
        #!/bin/sh
        set -eux
        echo '*** SSHD Configuration ***'
        sed -i 's/#UseDNS yes/UseDNS no/' /etc/ssh/sshd_config
        sed -i 's/#GSSAPIAuthentication yes/GSSAPIAuthentication no/' /etc/ssh/sshd_config
        sed -i 's/AllowTcpForwarding no/AllowTcpForwarding yes/' /etc/ssh/sshd_config 2>/dev/null || true
        """;

    public const string Vagrant = """
        #!/bin/sh
        set -eux
        echo '*** Vagrant User Setup ***'
        adduser -D -s /bin/bash vagrant
        echo 'vagrant:vagrant' | chpasswd
        mkdir -p /home/vagrant/.ssh
        chmod 700 /home/vagrant/.ssh
        wget -O /home/vagrant/.ssh/authorized_keys 'https://raw.githubusercontent.com/hashicorp/vagrant/main/keys/vagrant.pub'
        chmod 600 /home/vagrant/.ssh/authorized_keys
        chown -R vagrant:vagrant /home/vagrant/.ssh
        """;

    public const string Sudoers = """
        #!/bin/sh
        set -eux
        echo '*** Sudoers ***'
        echo 'vagrant ALL=(ALL) NOPASSWD: ALL' > /etc/sudoers.d/vagrant
        chmod 440 /etc/sudoers.d/vagrant
        """;

    public const string Cron = """
        #!/bin/sh
        set -eux
        echo '*** Cron ***'
        mkdir -p /etc/periodic/1min
        echo '*/1 * * * * run-parts /etc/periodic/1min' >> /etc/crontabs/root
        """;

    public const string VBoxGuestAdditions = """
        #!/bin/sh
        set -eux
        echo '*** VirtualBox Guest Additions ***'
        apk add --no-cache virtualbox-guest-additions virtualbox-guest-modules-virt
        rc-update add virtualbox-guest-additions default 2>/dev/null || true
        """;

    public const string DisableSshRoot = """
        #!/bin/sh
        set -eux
        echo '*** Disable SSH Root Login ***'
        sed -i 's/PermitRootLogin yes/PermitRootLogin no/' /etc/ssh/sshd_config
        """;

    public const string Minimize = """
        #!/bin/sh
        set -eux
        echo '*** Minimize ***'
        rm -rf /var/cache/apk/*
        rm -rf /tmp/*
        dd if=/dev/zero of=/EMPTY bs=1M 2>/dev/null || true
        rm -f /EMPTY
        sync
        """;

    public const string Reboot = """
        #!/bin/sh
        echo '*** Reboot ***'
        reboot
        """;
}
