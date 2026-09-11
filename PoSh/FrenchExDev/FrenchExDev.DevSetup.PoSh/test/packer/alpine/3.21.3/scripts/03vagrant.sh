set -eux
date > /etc/vagrant_box_build_time
echo "vagrant:vagrant" | chpasswd
mkdir -pm 700 /home/vagrant/.ssh
wget -O /home/vagrant/.ssh/authorized_keys https://raw.githubusercontent.com/mitchellh/vagrant/master/keys/vagrant.pub
chown -R vagrant:vagrant /home/vagrant/.ssh
chmod -R go-rwsx /home/vagrant/.ssh
echo "Use the bash shell for vagrant and root"
sed -e 's@/bin/ash@/bin/bash@' -i /etc/passwd
exit 0