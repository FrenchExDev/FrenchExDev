set -ux
echo "Setting up remote repositories..."
cat >/etc/apk/repositories <<EOT
http://nl.alpinelinux.org/alpine/v3.21/main/
http://nl.alpinelinux.org/alpine/v3.21/community/
EOT

echo "Performing an update/upgrade"
apk update
apk add --upgrade apk-tools
apk upgrade --available
apk add bash bash-completion sudo virtualbox-guest-additions

rc-update add virtualbox-guest-additions boot

echo "Setting systctl kernel settings to relax security"
cat >/etc/sysctl.d/00-alpine.conf <<EOT
net.ipv4.ip_forward = 1
net.ipv4.tcp_syncookies = 1
net.ipv4.conf.default.rp_filter = 0
net.ipv4.conf.all.rp_filter = 0
net.ipv4.ping_group_range=0 2147483647
kernel.panic = 120
EOT
exit 0