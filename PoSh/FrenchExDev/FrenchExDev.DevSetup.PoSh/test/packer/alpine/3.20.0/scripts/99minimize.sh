set -ux
echo "Clean up things (tmp, logs, apk cache)"
rm -rf /tmp/* /var/log/* /var/cache/apk/*

echo Whiteout root
count=$(df -kP / | tail -n1  | awk -F ' ' '{print $4}')
count=$(($count-1))
dd if=/dev/zero of=/whitespace bs=1M count=$count || echo "dd exit code $? is suppressed";
rm /whitespace

echo Whiteout /boot
count=$(df -kP /boot | tail -n1 | awk -F ' ' '{print $4}')
count=$(($count-1))
dd if=/dev/zero of=/boot/whitespace bs=1M count=$count || echo "dd exit code $? is suppressed";
rm /boot/whitespace

set +e
swapuuid="`/sbin/blkid -o value -l -s UUID -t TYPE=swap`";
case "$?" in
    2|0) ;;
    *) exit 1 ;;
esac
set -e

if [ "x${swapuuid}" != "x" ]; then
    # Whiteout the swap partition to reduce box size
    # Swap is disabled till reboot
    swappart="`readlink -f /dev/disk/by-uuid/$swapuuid`";
    /sbin/swapoff "$swappart" || true;
    dd if=/dev/zero of="$swappart" bs=1M || echo "dd exit code $? is suppressed";
    /sbin/mkswap -L "$swapuuid" "$swappart";
fi

sync;
sync;
sync;

exit 0