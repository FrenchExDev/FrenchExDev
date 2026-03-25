set -ux
sed '/PermitRootLogin yes/d' -i /etc/ssh/sshd_config