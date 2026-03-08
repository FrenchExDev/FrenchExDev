#!/bin/bash
set -e

apt-get update -qq > /dev/null 2>&1
apt-get install -y -qq curl gpg lsb-release > /dev/null 2>&1

curl -fsSL https://apt.releases.hashicorp.com/gpg | gpg --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg 2>/dev/null
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com $(grep -oP '(?<=UBUNTU_CODENAME=).*' /etc/os-release 2>/dev/null || lsb_release -cs) main" > /etc/apt/sources.list.d/hashicorp.list

apt-get update -qq > /dev/null 2>&1
apt-get install -y -qq vagrant > /dev/null 2>&1

vagrant --help
