# credit https://collegeguyuk.blogspot.com/2009/04/powershell-is-powerful-random-mac.html
function New-MACAddress {
    [CmdletBinding()]
    param(
        [int] $len = 12,
        [string] $chars = "0123456789abcdef",
        [switch] $NoColon
    )
    
    $bytes = new-object "System.Byte[]" $len

    $rnd = new-object System.Security.Cryptography.RNGCryptoServiceProvider
    $rnd.GetBytes($bytes)

    #define the fields
    $macraw = ""

    for ( $i = 0; $i -lt $len; $i++ ) {
        $macraw += $chars[ $bytes[$i] % $chars.Length ]
    }

    #add collons to the random macraw so that it is properly formatted
    $macaddress = $macraw[0] + $macraw[1] + ":" + $macraw[2] + $macraw[3] + ":" + $macraw[4] + $macraw[5] + ":" + $macraw[6] + $macraw[7] + ":" + $macraw[8] + $macraw[9] + ":" + $macraw[10] + $macraw[11]

    if($NoColon) {
        $macaddress.replace(':', '')
    } else {
        $macaddress
    }
}
