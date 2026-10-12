set -ux
echo "Environment is:-"
env
source /etc/os-release

cat <<EOT> /etc/motd
   _   _       _                __ _                  
  /_\ | |_ __ (_)_ __   ___    / /(_)_ __  _   ___  __
 //_\\| | '_ \| | '_ \ / _ \  / / | | '_ \| | | \ \/ /
/  _  \ | |_) | | | | |  __/ / /__| | | | | |_| |>  < 
\_/ \_/_| .__/|_|_| |_|\___| \____/_|_| |_|\__,_/_/\_\
        |_|                                           

Alpine ${VERSION_ID}
EOT

exit 0