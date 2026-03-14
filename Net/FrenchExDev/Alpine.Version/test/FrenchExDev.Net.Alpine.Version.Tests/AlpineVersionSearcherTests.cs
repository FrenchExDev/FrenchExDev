using FrenchExDev.Net.Alpine.Version.Testing;
using Shouldly;

namespace FrenchExDev.Net.Alpine.Version.Tests;

/// <summary>
/// Contains unit tests for verifying the behavior of the AlpineVersionSearcher when searching for stable Alpine Linux
/// versions.
/// </summary>
/// <remarks>These tests require internet access to perform live queries against Alpine Linux version
/// repositories. The tests focus on ensuring that the searcher correctly identifies stable versions and matches
/// expected architecture and flavor criteria.</remarks>
public class AlpineVersionSearcherTests
{
    #region Data

    private const string AlpineVersions = $@"
<html>
<head><title>Index of /alpine/</title></head>
<body>
<h1>Index of /alpine/</h1><hr><pre><a href=""../"">../</a>
<a href=""edge/"">edge/</a>                                              30-Sep-2015 07:58       -
<a href=""latest-stable/"">latest-stable/</a>                                     28-May-2025 05:24       -
<a href=""v3.0/"">v3.0/</a>                                              07-May-2014 22:52       -
<a href=""v3.1/"">v3.1/</a>                                              01-Jan-2015 07:25       -
<a href=""v3.10/"">v3.10/</a>                                             31-May-2019 18:14       -
<a href=""v3.11/"">v3.11/</a>                                             06-Dec-2019 18:36       -
<a href=""v3.12/"">v3.12/</a>                                             22-May-2020 09:41       -
<a href=""v3.13/"">v3.13/</a>                                             15-Jun-2021 19:47       -
<a href=""v3.14/"">v3.14/</a>                                             24-Nov-2021 11:46       -
<a href=""v3.15/"">v3.15/</a>                                             12-Nov-2021 13:57       -
<a href=""v3.16/"">v3.16/</a>                                             16-May-2022 19:04       -
<a href=""v3.17/"">v3.17/</a>                                             09-May-2023 19:39       -
<a href=""v3.18/"">v3.18/</a>                                             02-May-2023 13:38       -
<a href=""v3.19/"">v3.19/</a>                                             19-Nov-2023 16:19       -
<a href=""v3.2/"">v3.2/</a>                                              24-Apr-2015 09:24       -
<a href=""v3.20/"">v3.20/</a>                                             17-May-2024 13:28       -
<a href=""v3.21/"">v3.21/</a>                                             29-Nov-2024 20:58       -
<a href=""v3.22/"">v3.22/</a>                                             28-May-2025 05:24       -
<a href=""v3.3/"">v3.3/</a>                                              21-May-2023 14:16       -
<a href=""v3.4/"">v3.4/</a>                                              21-Apr-2016 12:39       -
<a href=""v3.5/"">v3.5/</a>                                              16-Nov-2016 16:01       -
<a href=""v3.6/"">v3.6/</a>                                              20-Apr-2017 10:47       -
<a href=""v3.7/"">v3.7/</a>                                              23-Nov-2017 21:25       -
<a href=""v3.8/"">v3.8/</a>                                              27-Apr-2018 06:06       -
<a href=""v3.9/"">v3.9/</a>                                              15-Nov-2018 16:03       -
<a href=""MIRRORS.txt"">MIRRORS.txt</a>                                        19-Sep-2025 22:00    3587
<a href=""last-updated"">last-updated</a>                                       20-Sep-2025 11:00      11
</pre><hr></body>
</html>

";

    private const string AlpineVersion_3_18_Architectures = $@"
<html>
<head><title>Index of /alpine/v3.18/releases/</title></head>
<body>
<h1>Index of /alpine/v3.18/releases/</h1><hr><pre><a href=""""../"""">../</a>
<a href=""aarch64"">aarch64/</a>                                           14-Feb-2025 03:03       -
<a href=""armhf/"">armhf/</a>                                             14-Feb-2025 00:00       -
<a href=""armv7/"">armv7/</a>                                             14-Feb-2025 01:58       -
<a href=""cloud/"">cloud/</a>                                             09-Jan-2025 12:14       -
<a href=""ppc64le/"">ppc64le/</a>                                           14-Feb-2025 00:00       -
<a href=""s390x/"">s390x/</a>                                             14-Feb-2025 00:00       -
<a href=""x86/"">x86/</a>                                               14-Feb-2025 00:00       -
<a href=""x86_64/"">x86_64/</a>                                            14-Feb-2025 00:00       -
</pre><hr></body>
</html>
";

    private const string AlpineVersion_3_18_ArchitecturesData = $@"
<html>
<head><title>Index of /alpine/v3.18/releases/x86_64/</title></head>
<body>
<h1>Index of /alpine/v3.18/releases/x86_64/</h1><hr><pre><a href=""../"">../</a>
<a href=""netboot/"">netboot/</a>                                           13-Feb-2025 23:20       -
<a href=""netboot-3.18.0/"">netboot-3.18.0/</a>                                    09-May-2023 18:41       -
<a href=""netboot-3.18.0_rc1/"">netboot-3.18.0_rc1/</a>                                02-May-2023 13:57       -
<a href=""netboot-3.18.0_rc2/"">netboot-3.18.0_rc2/</a>                                02-May-2023 14:15       -
<a href=""netboot-3.18.0_rc3/"">netboot-3.18.0_rc3/</a>                                04-May-2023 15:03       -
<a href=""netboot-3.18.0_rc4/"">netboot-3.18.0_rc4/</a>                                05-May-2023 13:55       -
<a href=""netboot-3.18.0_rc5/"">netboot-3.18.0_rc5/</a>                                08-May-2023 21:06       -
<a href=""netboot-3.18.0_rc6/"">netboot-3.18.0_rc6/</a>                                09-May-2023 17:30       -
<a href=""netboot-3.18.1/"">netboot-3.18.1/</a>                                    14-Jun-2023 14:29       -
<a href=""netboot-3.18.10/"">netboot-3.18.10/</a>                                   06-Jan-2025 18:09       -
<a href=""netboot-3.18.11/"">netboot-3.18.11/</a>                                   08-Jan-2025 11:03       -
<a href=""netboot-3.18.12/"">netboot-3.18.12/</a>                                   13-Feb-2025 23:20       -
<a href=""netboot-3.18.2/"">netboot-3.18.2/</a>                                    14-Jun-2023 15:05       -
<a href=""netboot-3.18.3/"">netboot-3.18.3/</a>                                    07-Aug-2023 13:11       -
<a href=""netboot-3.18.4/"">netboot-3.18.4/</a>                                    28-Sep-2023 11:20       -
<a href=""netboot-3.18.5/"">netboot-3.18.5/</a>                                    30-Nov-2023 09:34       -
<a href=""netboot-3.18.6/"">netboot-3.18.6/</a>                                    26-Jan-2024 20:45       -
<a href=""netboot-3.18.7/"">netboot-3.18.7/</a>                                    18-Jun-2024 15:15       -
<a href=""netboot-3.18.8/"">netboot-3.18.8/</a>                                    22-Jul-2024 18:18       -
<a href=""netboot-3.18.9/"">netboot-3.18.9/</a>                                    06-Sep-2024 11:37       -
<a href=""alpine-extended-3.18.0-x86_64.iso"">alpine-extended-3.18.0-x86_64.iso</a>                  09-May-2023 18:45    840M
<a href=""alpine-extended-3.18.0-x86_64.iso.asc"">alpine-extended-3.18.0-x86_64.iso.asc</a>              09-May-2023 19:51     833
<a href=""alpine-extended-3.18.0-x86_64.iso.sha256"">alpine-extended-3.18.0-x86_64.iso.sha256</a>           09-May-2023 18:45     100
<a href=""alpine-extended-3.18.0-x86_64.iso.sha512"">alpine-extended-3.18.0-x86_64.iso.sha512</a>           09-May-2023 18:45     164
<a href=""alpine-extended-3.18.0_rc2-x86_64.iso"">alpine-extended-3.18.0_rc2-x86_64.iso</a>              02-May-2023 14:18    840M
<a href=""alpine-extended-3.18.0_rc2-x86_64.iso.sha256"">alpine-extended-3.18.0_rc2-x86_64.iso.sha256</a>       02-May-2023 14:19     104
<a href=""alpine-extended-3.18.0_rc2-x86_64.iso.sha512"">alpine-extended-3.18.0_rc2-x86_64.iso.sha512</a>       02-May-2023 14:19     168
<a href=""alpine-extended-3.18.0_rc3-x86_64.iso"">alpine-extended-3.18.0_rc3-x86_64.iso</a>              04-May-2023 15:06    840M
<a href=""alpine-extended-3.18.0_rc3-x86_64.iso.sha256"">alpine-extended-3.18.0_rc3-x86_64.iso.sha256</a>       04-May-2023 15:07     104
<a href=""alpine-extended-3.18.0_rc3-x86_64.iso.sha512"">alpine-extended-3.18.0_rc3-x86_64.iso.sha512</a>       04-May-2023 15:07     168
<a href=""alpine-extended-3.18.0_rc4-x86_64.iso"">alpine-extended-3.18.0_rc4-x86_64.iso</a>              05-May-2023 13:59    840M
<a href=""alpine-extended-3.18.0_rc4-x86_64.iso.sha256"">alpine-extended-3.18.0_rc4-x86_64.iso.sha256</a>       05-May-2023 13:59     104
<a href=""alpine-extended-3.18.0_rc4-x86_64.iso.sha512"">alpine-extended-3.18.0_rc4-x86_64.iso.sha512</a>       05-May-2023 13:59     168
<a href=""alpine-extended-3.18.0_rc5-x86_64.iso"">alpine-extended-3.18.0_rc5-x86_64.iso</a>              08-May-2023 21:10    840M
<a href=""alpine-extended-3.18.0_rc5-x86_64.iso.sha256"">alpine-extended-3.18.0_rc5-x86_64.iso.sha256</a>       08-May-2023 21:10     104
<a href=""alpine-extended-3.18.0_rc5-x86_64.iso.sha512"">alpine-extended-3.18.0_rc5-x86_64.iso.sha512</a>       08-May-2023 21:10     168
<a href=""alpine-extended-3.18.0_rc6-x86_64.iso"">alpine-extended-3.18.0_rc6-x86_64.iso</a>              09-May-2023 17:34    840M
<a href=""alpine-extended-3.18.0_rc6-x86_64.iso.sha256"">alpine-extended-3.18.0_rc6-x86_64.iso.sha256</a>       09-May-2023 17:34     104
<a href=""alpine-extended-3.18.0_rc6-x86_64.iso.sha512"">alpine-extended-3.18.0_rc6-x86_64.iso.sha512</a>       09-May-2023 17:34     168
<a href=""alpine-extended-3.18.1-x86_64.iso"">alpine-extended-3.18.1-x86_64.iso</a>                  14-Jun-2023 14:35    843M
<a href=""alpine-extended-3.18.1-x86_64.iso.sha256"">alpine-extended-3.18.1-x86_64.iso.sha256</a>           14-Jun-2023 14:35     100
<a href=""alpine-extended-3.18.1-x86_64.iso.sha512"">alpine-extended-3.18.1-x86_64.iso.sha512</a>           14-Jun-2023 14:35     164
<a href=""alpine-extended-3.18.10-x86_64.iso"">alpine-extended-3.18.10-x86_64.iso</a>                 06-Jan-2025 18:12    845M
<a href=""alpine-extended-3.18.10-x86_64.iso.sha256"">alpine-extended-3.18.10-x86_64.iso.sha256</a>          06-Jan-2025 18:12     101
<a href=""alpine-extended-3.18.10-x86_64.iso.sha512"">alpine-extended-3.18.10-x86_64.iso.sha512</a>          06-Jan-2025 18:12     165
<a href=""alpine-extended-3.18.11-x86_64.iso"">alpine-extended-3.18.11-x86_64.iso</a>                 08-Jan-2025 11:06    845M
<a href=""alpine-extended-3.18.11-x86_64.iso.asc"">alpine-extended-3.18.11-x86_64.iso.asc</a>             08-Jan-2025 16:30     833
<a href=""alpine-extended-3.18.11-x86_64.iso.sha256"">alpine-extended-3.18.11-x86_64.iso.sha256</a>          08-Jan-2025 11:06     101
<a href=""alpine-extended-3.18.11-x86_64.iso.sha512"">alpine-extended-3.18.11-x86_64.iso.sha512</a>          08-Jan-2025 11:06     165
<a href=""alpine-extended-3.18.12-x86_64.iso"">alpine-extended-3.18.12-x86_64.iso</a>                 13-Feb-2025 23:26    845M
<a href=""alpine-extended-3.18.12-x86_64.iso.asc"">alpine-extended-3.18.12-x86_64.iso.asc</a>             14-Feb-2025 00:00     833
<a href=""alpine-extended-3.18.12-x86_64.iso.sha256"">alpine-extended-3.18.12-x86_64.iso.sha256</a>          13-Feb-2025 23:27     101
<a href=""alpine-extended-3.18.12-x86_64.iso.sha512"">alpine-extended-3.18.12-x86_64.iso.sha512</a>          13-Feb-2025 23:27     165
<a href=""alpine-extended-3.18.2-x86_64.iso"">alpine-extended-3.18.2-x86_64.iso</a>                  14-Jun-2023 15:09    843M
<a href=""alpine-extended-3.18.2-x86_64.iso.asc"">alpine-extended-3.18.2-x86_64.iso.asc</a>              14-Jun-2023 15:29     833
<a href=""alpine-extended-3.18.2-x86_64.iso.sha256"">alpine-extended-3.18.2-x86_64.iso.sha256</a>           14-Jun-2023 15:09     100
<a href=""alpine-extended-3.18.2-x86_64.iso.sha512"">alpine-extended-3.18.2-x86_64.iso.sha512</a>           14-Jun-2023 15:09     164
<a href=""alpine-extended-3.18.3-x86_64.iso"">alpine-extended-3.18.3-x86_64.iso</a>                  07-Aug-2023 13:16    843M
<a href=""alpine-extended-3.18.3-x86_64.iso.asc"">alpine-extended-3.18.3-x86_64.iso.asc</a>              07-Aug-2023 15:18     833
<a href=""alpine-extended-3.18.3-x86_64.iso.sha256"">alpine-extended-3.18.3-x86_64.iso.sha256</a>           07-Aug-2023 13:16     100
<a href=""alpine-extended-3.18.3-x86_64.iso.sha512"">alpine-extended-3.18.3-x86_64.iso.sha512</a>           07-Aug-2023 13:16     164
<a href=""alpine-extended-3.18.4-x86_64.iso"">alpine-extended-3.18.4-x86_64.iso</a>                  28-Sep-2023 11:24    843M
<a href=""alpine-extended-3.18.4-x86_64.iso.asc"">alpine-extended-3.18.4-x86_64.iso.asc</a>              28-Sep-2023 13:05     833
<a href=""alpine-extended-3.18.4-x86_64.iso.sha256"">alpine-extended-3.18.4-x86_64.iso.sha256</a>           28-Sep-2023 11:24     100
<a href=""alpine-extended-3.18.4-x86_64.iso.sha512"">alpine-extended-3.18.4-x86_64.iso.sha512</a>           28-Sep-2023 11:24     164
<a href=""alpine-extended-3.18.5-x86_64.iso"">alpine-extended-3.18.5-x86_64.iso</a>                  30-Nov-2023 09:38    843M
<a href=""alpine-extended-3.18.5-x86_64.iso.asc"">alpine-extended-3.18.5-x86_64.iso.asc</a>              30-Nov-2023 12:25     833
<a href=""alpine-extended-3.18.5-x86_64.iso.sha256"">alpine-extended-3.18.5-x86_64.iso.sha256</a>           30-Nov-2023 09:38     100
<a href=""alpine-extended-3.18.5-x86_64.iso.sha512"">alpine-extended-3.18.5-x86_64.iso.sha512</a>           30-Nov-2023 09:38     164
<a href=""alpine-extended-3.18.6-x86_64.iso"">alpine-extended-3.18.6-x86_64.iso</a>                  26-Jan-2024 20:50    843M
<a href=""alpine-extended-3.18.6-x86_64.iso.asc"">alpine-extended-3.18.6-x86_64.iso.asc</a>              26-Jan-2024 21:29     833
<a href=""alpine-extended-3.18.6-x86_64.iso.sha256"">alpine-extended-3.18.6-x86_64.iso.sha256</a>           26-Jan-2024 20:50     100
<a href=""alpine-extended-3.18.6-x86_64.iso.sha512"">alpine-extended-3.18.6-x86_64.iso.sha512</a>           26-Jan-2024 20:50     164
<a href=""alpine-extended-3.18.7-x86_64.iso"">alpine-extended-3.18.7-x86_64.iso</a>                  18-Jun-2024 15:21    844M
<a href=""alpine-extended-3.18.7-x86_64.iso.sha256"">alpine-extended-3.18.7-x86_64.iso.sha256</a>           18-Jun-2024 15:21     100
<a href=""alpine-extended-3.18.7-x86_64.iso.sha512"">alpine-extended-3.18.7-x86_64.iso.sha512</a>           18-Jun-2024 15:22     164
<a href=""alpine-extended-3.18.8-x86_64.iso"">alpine-extended-3.18.8-x86_64.iso</a>                  22-Jul-2024 18:23    844M
<a href=""alpine-extended-3.18.8-x86_64.iso.asc"">alpine-extended-3.18.8-x86_64.iso.asc</a>              22-Jul-2024 19:05     833
<a href=""alpine-extended-3.18.8-x86_64.iso.sha256"">alpine-extended-3.18.8-x86_64.iso.sha256</a>           22-Jul-2024 18:23     100
<a href=""alpine-extended-3.18.8-x86_64.iso.sha512"">alpine-extended-3.18.8-x86_64.iso.sha512</a>           22-Jul-2024 18:23     164
<a href=""alpine-extended-3.18.9-x86_64.iso"">alpine-extended-3.18.9-x86_64.iso</a>                  06-Sep-2024 11:40    845M
<a href=""alpine-extended-3.18.9-x86_64.iso.asc"">alpine-extended-3.18.9-x86_64.iso.asc</a>              06-Sep-2024 12:26     833
<a href=""alpine-extended-3.18.9-x86_64.iso.sha256"">alpine-extended-3.18.9-x86_64.iso.sha256</a>           06-Sep-2024 11:41     100
<a href=""alpine-extended-3.18.9-x86_64.iso.sha512"">alpine-extended-3.18.9-x86_64.iso.sha512</a>           06-Sep-2024 11:41     164
<a href=""alpine-minirootfs-3.18.0-x86_64.tar.gz"">alpine-minirootfs-3.18.0-x86_64.tar.gz</a>             09-May-2023 18:39      3M
<a href=""alpine-minirootfs-3.18.0-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.0-x86_64.tar.gz.asc</a>         09-May-2023 19:51     833
<a href=""alpine-minirootfs-3.18.0-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.0-x86_64.tar.gz.sha256</a>      09-May-2023 18:39     105
<a href=""alpine-minirootfs-3.18.0-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.0-x86_64.tar.gz.sha512</a>      09-May-2023 18:39     169
<a href=""alpine-minirootfs-3.18.0_rc1-x86_64.tar.gz"">alpine-minirootfs-3.18.0_rc1-x86_64.tar.gz</a>         02-May-2023 13:37      3M
<a href=""alpine-minirootfs-3.18.0_rc1-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.0_rc1-x86_64.tar.gz.sha256</a>  02-May-2023 13:37     109
<a href=""alpine-minirootfs-3.18.0_rc1-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.0_rc1-x86_64.tar.gz.sha512</a>  02-May-2023 13:37     173
<a href=""alpine-minirootfs-3.18.0_rc2-x86_64.tar.gz"">alpine-minirootfs-3.18.0_rc2-x86_64.tar.gz</a>         02-May-2023 14:13      3M
<a href=""alpine-minirootfs-3.18.0_rc2-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.0_rc2-x86_64.tar.gz.sha256</a>  02-May-2023 14:13     109
<a href=""alpine-minirootfs-3.18.0_rc2-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.0_rc2-x86_64.tar.gz.sha512</a>  02-May-2023 14:13     173
<a href=""alpine-minirootfs-3.18.0_rc3-x86_64.tar.gz"">alpine-minirootfs-3.18.0_rc3-x86_64.tar.gz</a>         04-May-2023 15:01      3M
<a href=""alpine-minirootfs-3.18.0_rc3-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.0_rc3-x86_64.tar.gz.sha256</a>  04-May-2023 15:01     109
<a href=""alpine-minirootfs-3.18.0_rc3-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.0_rc3-x86_64.tar.gz.sha512</a>  04-May-2023 15:01     173
<a href=""alpine-minirootfs-3.18.0_rc4-x86_64.tar.gz"">alpine-minirootfs-3.18.0_rc4-x86_64.tar.gz</a>         05-May-2023 13:53      3M
<a href=""alpine-minirootfs-3.18.0_rc4-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.0_rc4-x86_64.tar.gz.sha256</a>  05-May-2023 13:53     109
<a href=""alpine-minirootfs-3.18.0_rc4-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.0_rc4-x86_64.tar.gz.sha512</a>  05-May-2023 13:53     173
<a href=""alpine-minirootfs-3.18.0_rc5-x86_64.tar.gz"">alpine-minirootfs-3.18.0_rc5-x86_64.tar.gz</a>         08-May-2023 21:04      3M
<a href=""alpine-minirootfs-3.18.0_rc5-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.0_rc5-x86_64.tar.gz.sha256</a>  08-May-2023 21:04     109
<a href=""alpine-minirootfs-3.18.0_rc5-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.0_rc5-x86_64.tar.gz.sha512</a>  08-May-2023 21:04     173
<a href=""alpine-minirootfs-3.18.0_rc6-x86_64.tar.gz"">alpine-minirootfs-3.18.0_rc6-x86_64.tar.gz</a>         09-May-2023 17:28      3M
<a href=""alpine-minirootfs-3.18.0_rc6-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.0_rc6-x86_64.tar.gz.sha256</a>  09-May-2023 17:28     109
<a href=""alpine-minirootfs-3.18.0_rc6-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.0_rc6-x86_64.tar.gz.sha512</a>  09-May-2023 17:28     173
<a href=""alpine-minirootfs-3.18.1-x86_64.tar.gz"">alpine-minirootfs-3.18.1-x86_64.tar.gz</a>             14-Jun-2023 14:27      3M
<a href=""alpine-minirootfs-3.18.1-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.1-x86_64.tar.gz.sha256</a>      14-Jun-2023 14:27     105
<a href=""alpine-minirootfs-3.18.1-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.1-x86_64.tar.gz.sha512</a>      14-Jun-2023 14:27     169
<a href=""alpine-minirootfs-3.18.10-x86_64.tar.gz"">alpine-minirootfs-3.18.10-x86_64.tar.gz</a>            06-Jan-2025 18:08      3M
<a href=""alpine-minirootfs-3.18.10-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.10-x86_64.tar.gz.sha256</a>     06-Jan-2025 18:08     106
<a href=""alpine-minirootfs-3.18.10-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.10-x86_64.tar.gz.sha512</a>     06-Jan-2025 18:08     170
<a href=""alpine-minirootfs-3.18.11-x86_64.tar.gz"">alpine-minirootfs-3.18.11-x86_64.tar.gz</a>            08-Jan-2025 11:02      3M
<a href=""alpine-minirootfs-3.18.11-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.11-x86_64.tar.gz.asc</a>        08-Jan-2025 16:30     833
<a href=""alpine-minirootfs-3.18.11-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.11-x86_64.tar.gz.sha256</a>     08-Jan-2025 11:02      106
<a href=""alpine-minirootfs-3.18.11-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.11-x86_64.tar.gz.sha512</a>     08-Jan-2025 11:02      170
<a href=""alpine-minirootfs-3.18.12-x86_64.tar.gz"">alpine-minirootfs-3.18.12-x86_64.tar.gz</a>            13-Feb-2025 23:17      3M
<a href=""alpine-minirootfs-3.18.12-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.12-x86_64.tar.gz.asc</a>        14-Feb-2025 00:00     833
<a href=""alpine-minirootfs-3.18.12-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.12-x86_64.tar.gz.sha256</a>     13-Feb-2025 23:17      106
<a href=""alpine-minirootfs-3.18.12-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.12-x86_64.tar.gz.sha512</a>     13-Feb-2025 23:17      170
<a href=""alpine-minirootfs-3.18.2-x86_64.tar.gz"">alpine-minirootfs-3.18.2-x86_64.tar.gz</a>             14-Jun-2023 15:03      3M
<a href=""alpine-minirootfs-3.18.2-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.2-x86_64.tar.gz.asc</a>         14-Jun-2023 15:29     833
<a href=""alpine-minirootfs-3.18.2-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.2-x86_64.tar.gz.sha256</a>      14-Jun-2023 15:03      105
<a href=""alpine-minirootfs-3.18.2-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.2-x86_64.tar.gz.sha512</a>      14-Jun-2023 15:03      169
<a href=""alpine-minirootfs-3.18.3-x86_64.tar.gz"">alpine-minirootfs-3.18.3-x86_64.tar.gz</a>             07-Aug-2023 13:09      3M
<a href=""alpine-minirootfs-3.18.3-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.3-x86_64.tar.gz.asc</a>         07-Aug-2023 15:18     833
<a href=""alpine-minirootfs-3.18.3-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.3-x86_64.tar.gz.sha256</a>      07-Aug-2023 13:09      105
<a href=""alpine-minirootfs-3.18.3-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.3-x86_64.tar.gz.sha512</a>      07-Aug-2023 13:09      169
<a href=""alpine-minirootfs-3.18.4-x86_64.tar.gz"">alpine-minirootfs-3.18.4-x86_64.tar.gz</a>             28-Sep-2023 11:18      3M
<a href=""alpine-minirootfs-3.18.4-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.4-x86_64.tar.gz.asc</a>         28-Sep-2023 13:05     833
<a href=""alpine-minirootfs-3.18.4-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.4-x86_64.tar.gz.sha256</a>      28-Sep-2023 11:18      105
<a href=""alpine-minirootfs-3.18.4-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.4-x86_64.tar.gz.sha512</a>      28-Sep-2023 11:18      169
<a href=""alpine-minirootfs-3.18.5-x86_64.tar.gz"">alpine-minirootfs-3.18.5-x86_64.tar.gz</a>             30-Nov-2023 09:32      3M
<a href=""alpine-minirootfs-3.18.5-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.5-x86_64.tar.gz.asc</a>         30-Nov-2023 12:25     833
<a href=""alpine-minirootfs-3.18.5-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.5-x86_64.tar.gz.sha256</a>      30-Nov-2023 09:32      105
<a href=""alpine-minirootfs-3.18.5-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.5-x86_64.tar.gz.sha512</a>      30-Nov-2023 09:32      169
<a href=""alpine-minirootfs-3.18.6-x86_64.tar.gz"">alpine-minirootfs-3.18.6-x86_64.tar.gz</a>             26-Jan-2024 20:43      3M
<a href=""alpine-minirootfs-3.18.6-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.6-x86_64.tar.gz.asc</a>         26-Jan-2024 21:29     833
<a href=""alpine-minirootfs-3.18.6-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.6-x86_64.tar.gz.sha256</a>      26-Jan-2024 20:43      105
<a href=""alpine-minirootfs-3.18.6-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.6-x86_64.tar.gz.sha512</a>      26-Jan-2024 20:43      169
<a href=""alpine-minirootfs-3.18.7-x86_64.tar.gz"">alpine-minirootfs-3.18.7-x86_64.tar.gz</a>             18-Jun-2024 15:12      3M
<a href=""alpine-minirootfs-3.18.7-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.7-x86_64.tar.gz.sha256</a>      18-Jun-2024 15:12      105
<a href=""alpine-minirootfs-3.18.7-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.7-x86_64.tar.gz.sha512</a>      18-Jun-2024 15:12      169
<a href=""alpine-minirootfs-3.18.8-x86_64.tar.gz"">alpine-minirootfs-3.18.8-x86_64.tar.gz</a>             22-Jul-2024 18:16      3M
<a href=""alpine-minirootfs-3.18.8-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.8-x86_64.tar.gz.asc</a>         22-Jul-2024 19:05     833
<a href=""alpine-minirootfs-3.18.8-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.8-x86_64.tar.gz.sha256</a>      22-Jul-2024 18:16      105
<a href=""alpine-minirootfs-3.18.8-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.8-x86_64.tar.gz.sha512</a>      22-Jul-2024 18:16      169
<a href=""alpine-minirootfs-3.18.9-x86_64.tar.gz"">alpine-minirootfs-3.18.9-x86_64.tar.gz</a>             06-Sep-2024 11:36      3M
<a href=""alpine-minirootfs-3.18.9-x86_64.tar.gz.asc"">alpine-minirootfs-3.18.9-x86_64.tar.gz.asc</a>         06-Sep-2024 12:26     833
<a href=""alpine-minirootfs-3.18.9-x86_64.tar.gz.sha256"">alpine-minirootfs-3.18.9-x86_64.tar.gz.sha256</a>      06-Sep-2024 11:36      105
<a href=""alpine-minirootfs-3.18.9-x86_64.tar.gz.sha512"">alpine-minirootfs-3.18.9-x86_64.tar.gz.sha512</a>      06-Sep-2024 11:36      169
<a href=""alpine-netboot-3.18.0-x86_64.tar.gz"">alpine-netboot-3.18.0-x86_64.tar.gz</a>                09-May-2023 18:41    204M
<a href=""alpine-netboot-3.18.0-x86_64.tar.gz.asc"">alpine-netboot-3.18.0-x86_64.tar.gz.asc</a>            09-May-2023 19:51     833
<a href=""alpine-netboot-3.18.0-x86_64.tar.gz.sha256"">alpine-netboot-3.18.0-x86_64.tar.gz.sha256</a>         09-May-2023 18:42     102
<a href=""alpine-netboot-3.18.0-x86_64.tar.gz.sha512"">alpine-netboot-3.18.0-x86_64.tar.gz.sha512</a>         09-May-2023 18:42     166
<a href=""alpine-netboot-3.18.0_rc1-x86_64.tar.gz"">alpine-netboot-3.18.0_rc1-x86_64.tar.gz</a>            02-May-2023 13:57    204M
<a href=""alpine-netboot-3.18.0_rc1-x86_64.tar.gz.sha256"">alpine-netboot-3.18.0_rc1-x86_64.tar.gz.sha256</a>     02-May-2023 13:39     106
<a href=""alpine-netboot-3.18.0_rc1-x86_64.tar.gz.sha512"">alpine-netboot-3.18.0_rc1-x86_64.tar.gz.sha512</a>     02-May-2023 13:39     170
<a href=""alpine-netboot-3.18.0_rc2-x86_64.tar.gz"">alpine-netboot-3.18.0_rc2-x86_64.tar.gz</a>            02-May-2023 14:15    204M
<a href=""alpine-netboot-3.18.0_rc2-x86_64.tar.gz.sha256"">alpine-netboot-3.18.0_rc2-x86_64.tar.gz.sha256</a>     02-May-2023 14:15     106
<a href=""alpine-netboot-3.18.0_rc2-x86_64.tar.gz.sha512"">alpine-netboot-3.18.0_rc2-x86_64.tar.gz.sha512</a>     02-May-2023 14:15     170
<a href=""alpine-netboot-3.18.0_rc3-x86_64.tar.gz"">alpine-netboot-3.18.0_rc3-x86_64.tar.gz</a>            04-May-2023 15:03    204M
<a href=""alpine-netboot-3.18.0_rc3-x86_64.tar.gz.sha256"">alpine-netboot-3.18.0_rc3-x86_64.tar.gz.sha256</a>     04-May-2023 15:03     106
<a href=""alpine-netboot-3.18.0_rc3-x86_64.tar.gz.sha512"">alpine-netboot-3.18.0_rc3-x86_64.tar.gz.sha512</a>     04-May-2023 15:03     170
<a href=""alpine-netboot-3.18.0_rc4-x86_64.tar.gz"">alpine-netboot-3.18.0_rc4-x86_64.tar.gz</a>            05-May-2023 13:56    204M
<a href=""alpine-netboot-3.18.0_rc4-x86_64.tar.gz.sha256"">alpine-netboot-3.18.0_rc4-x86_64.tar.gz.sha256</a>     05-May-2023 13:56     106
<a href=""alpine-netboot-3.18.0_rc4-x86_64.tar.gz.sha512"">alpine-netboot-3.18.0_rc4-x86_64.tar.gz.sha512</a>     05-May-2023 13:56     170
<a href=""alpine-netboot-3.18.0_rc5-x86_64.tar.gz"">alpine-netboot-3.18.0_rc5-x86_64.tar.gz</a>            08-May-2023 21:06    204M
<a href=""alpine-netboot-3.18.0_rc5-x86_64.tar.gz.sha256"">alpine-netboot-3.18.0_rc5-x86_64.tar.gz.sha256</a>     08-May-2023 21:07     106
<a href=""alpine-netboot-3.18.0_rc5-x86_64.tar.gz.sha512"">alpine-netboot-3.18.0_rc5-x86_64.tar.gz.sha512</a>     08-May-2023 21:07     170
<a href=""alpine-netboot-3.18.0_rc6-x86_64.tar.gz"">alpine-netboot-3.18.0_rc6-x86_64.tar.gz</a>            09-May-2023 17:30    204M
<a href=""alpine-netboot-3.18.0_rc6-x86_64.tar.gz.sha256"">alpine-netboot-3.18.0_rc6-x86_64.tar.gz.sha256</a>     09-May-2023 17:31     106
<a href=""alpine-netboot-3.18.0_rc6-x86_64.tar.gz.sha512"">alpine-netboot-3.18.0_rc6-x86_64.tar.gz.sha512</a>     09-May-2023 17:31     170
<a href=""alpine-netboot-3.18.1-x86_64.tar.gz"">alpine-netboot-3.18.1-x86_64.tar.gz</a>                14-Jun-2023 14:30    205M
<a href=""alpine-netboot-3.18.1-x86_64.tar.gz.sha256"">alpine-netboot-3.18.1-x86_64.tar.gz.sha256</a>         14-Jun-2023 14:30     102
<a href=""alpine-netboot-3.18.1-x86_64.tar.gz.sha512"">alpine-netboot-3.18.1-x86_64.tar.gz.sha512</a>         14-Jun-2023 14:30     166
<a href=""alpine-netboot-3.18.10-x86_64.tar.gz"">alpine-netboot-3.18.10-x86_64.tar.gz</a>               06-Jan-2025 18:10    206M
<a href=""alpine-netboot-3.18.10-x86_64.tar.gz.sha256"">alpine-netboot-3.18.10-x86_64.tar.gz.sha256</a>        06-Jan-2025 18:10     103
<a href=""alpine-netboot-3.18.10-x86_64.tar.gz.sha512"">alpine-netboot-3.18.10-x86_64.tar.gz.sha512</a>        06-Jan-2025 18:10     167
<a href=""alpine-netboot-3.18.11-x86_64.tar.gz"">alpine-netboot-3.18.11-x86_64.tar.gz</a>               08-Jan-2025 11:03    206M
<a href=""alpine-netboot-3.18.11-x86_64.tar.gz.asc"">alpine-netboot-3.18.11-x86_64.tar.gz.asc</a>           08-Jan-2025 16:30     833
<a href=""alpine-netboot-3.18.11-x86_64.tar.gz.sha256"">alpine-netboot-3.18.11-x86_64.tar.gz.sha256</a>        08-Jan-2025 11:03     103
<a href=""alpine-netboot-3.18.11-x86_64.tar.gz.sha512"">alpine-netboot-3.18.11-x86_64.tar.gz.sha512</a>        08-Jan-2025 11:03     167
<a href=""alpine-netboot-3.18.12-x86_64.tar.gz"">alpine-netboot-3.18.12-x86_64.tar.gz</a>               13-Feb-2025 23:20    206M
<a href=""alpine-netboot-3.18.12-x86_64.tar.gz.asc"">alpine-netboot-3.18.12-x86_64.tar.gz.asc</a>           14-Feb-2025 00:00     833
<a href=""alpine-netboot-3.18.12-x86_64.tar.gz.sha256"">alpine-netboot-3.18.12-x86_64.tar.gz.sha256</a>        13-Feb-2025 23:20     103
<a href=""alpine-netboot-3.18.12-x86_64.tar.gz.sha512"">alpine-netboot-3.18.12-x86_64.tar.gz.sha512</a>        13-Feb-2025 23:20     167
<a href=""alpine-netboot-3.18.2-x86_64.tar.gz"">alpine-netboot-3.18.2-x86_64.tar.gz</a>                14-Jun-2023 15:05    205M
<a href=""alpine-netboot-3.18.2-x86_64.tar.gz.asc"">alpine-netboot-3.18.2-x86_64.tar.gz.asc</a>            14-Jun-2023 15:29     833
<a href=""alpine-netboot-3.18.2-x86_64.tar.gz.sha256"">alpine-netboot-3.18.2-x86_64.tar.gz.sha256</a>         14-Jun-2023 15:05     102
<a href=""alpine-netboot-3.18.2-x86_64.tar.gz.sha512"">alpine-netboot-3.18.2-x86_64.tar.gz.sha512</a>         14-Jun-2023 15:05     166
<a href=""alpine-netboot-3.18.3-x86_64.tar.gz"">alpine-netboot-3.18.3-x86_64.tar.gz</a>                07-Aug-2023 13:11    205M
<a href=""alpine-netboot-3.18.3-x86_64.tar.gz.asc"">alpine-netboot-3.18.3-x86_64.tar.gz.asc</a>            07-Aug-2023 15:18     833
<a href=""alpine-netboot-3.18.3-x86_64.tar.gz.sha256"">alpine-netboot-3.18.3-x86_64.tar.gz.sha256</a>         07-Aug-2023 13:12     102
<a href=""alpine-netboot-3.18.3-x86_64.tar.gz.sha512"">alpine-netboot-3.18.3-x86_64.tar.gz.sha512</a>         07-Aug-2023 13:12     166
<a href=""alpine-netboot-3.18.4-x86_64.tar.gz"">alpine-netboot-3.18.4-x86_64.tar.gz</a>                28-Sep-2023 11:21    205M
<a href=""alpine-netboot-3.18.4-x86_64.tar.gz.asc"">alpine-netboot-3.18.4-x86_64.tar.gz.asc</a>            28-Sep-2023 13:05     833
<a href=""alpine-netboot-3.18.4-x86_64.tar.gz.sha256"">alpine-netboot-3.18.4-x86_64.tar.gz.sha256</a>         28-Sep-2023 11:21     102
<a href=""alpine-netboot-3.18.4-x86_64.tar.gz.sha512"">alpine-netboot-3.18.4-x86_64.tar.gz.sha512</a>         28-Sep-2023 11:21     166
<a href=""alpine-netboot-3.18.5-x86_64.tar.gz"">alpine-netboot-3.18.5-x86_64.tar.gz</a>                30-Nov-2023 09:34    205M
<a href=""alpine-netboot-3.18.5-x86_64.tar.gz.asc"">alpine-netboot-3.18.5-x86_64.tar.gz.asc</a>            30-Nov-2023 12:25     833
<a href=""alpine-netboot-3.18.5-x86_64.tar.gz.sha256"">alpine-netboot-3.18.5-x86_64.tar.gz.sha256</a>         30-Nov-2023 09:35     102
<a href=""alpine-netboot-3.18.5-x86_64.tar.gz.sha512"">alpine-netboot-3.18.5-x86_64.tar.gz.sha512</a>         30-Nov-2023 09:35     166
<a href=""alpine-netboot-3.18.6-x86_64.tar.gz"">alpine-netboot-3.18.6-x86_64.tar.gz</a>                26-Jan-2024 20:46    205M
<a href=""alpine-netboot-3.18.6-x86_64.tar.gz.asc"">alpine-netboot-3.18.6-x86_64.tar.gz.asc</a>            26-Jan-2024 21:29     833
<a href=""alpine-netboot-3.18.6-x86_64.tar.gz.sha256"">alpine-netboot-3.18.6-x86_64.tar.gz.sha256</a>         26-Jan-2024 20:46     102
<a href=""alpine-netboot-3.18.6-x86_64.tar.gz.sha512"">alpine-netboot-3.18.6-x86_64.tar.gz.sha512</a>         26-Jan-2024 20:46     166
<a href=""alpine-netboot-3.18.7-x86_64.tar.gz"">alpine-netboot-3.18.7-x86_64.tar.gz</a>                18-Jun-2024 15:15    206M
<a href=""alpine-netboot-3.18.7-x86_64.tar.gz.sha256"">alpine-netboot-3.18.7-x86_64.tar.gz.sha256</a>         18-Jun-2024 15:15     102
<a href=""alpine-netboot-3.18.7-x86_64.tar.gz.sha512"">alpine-netboot-3.18.7-x86_64.tar.gz.sha512</a>         18-Jun-2024 15:15     166
<a href=""alpine-netboot-3.18.8-x86_64.tar.gz"">alpine-netboot-3.18.8-x86_64.tar.gz</a>                22-Jul-2024 18:19    206M
<a href=""alpine-netboot-3.18.8-x86_64.tar.gz.asc"">alpine-netboot-3.18.8-x86_64.tar.gz.asc</a>            22-Jul-2024 19:05     833
<a href=""alpine-netboot-3.18.8-x86_64.tar.gz.sha256"">alpine-netboot-3.18.8-x86_64.tar.gz.sha256</a>         22-Jul-2024 18:19     102
<a href=""alpine-netboot-3.18.8-x86_64.tar.gz.sha512"">alpine-netboot-3.18.8-x86_64.tar.gz.sha512</a>         22-Jul-2024 18:19     166
<a href=""alpine-netboot-3.18.9-x86_64.tar.gz"">alpine-netboot-3.18.9-x86_64.tar.gz</a>                06-Sep-2024 11:38    206M
<a href=""alpine-netboot-3.18.9-x86_64.tar.gz.asc"">alpine-netboot-3.18.9-x86_64.tar.gz.asc</a>            06-Sep-2024 12:26     833
<a href=""alpine-netboot-3.18.9-x86_64.tar.gz.sha256"">alpine-netboot-3.18.9-x86_64.tar.gz.sha256</a>         06-Sep-2024 11:38     102
<a href=""alpine-netboot-3.18.9-x86_64.tar.gz.sha512"">alpine-netboot-3.18.9-x86_64.tar.gz.sha512</a>         06-Sep-2024 11:38     166
<a href=""alpine-standard-3.18.0-x86_64.iso"">alpine-standard-3.18.0-x86_64.iso</a>                  09-May-2023 18:43    189M
<a href=""alpine-standard-3.18.0-x86_64.iso.asc"">alpine-standard-3.18.0-x86_64.iso.asc</a>              09-May-2023 19:51     833
<a href=""alpine-standard-3.18.0-x86_64.iso.sha256"">alpine-standard-3.18.0-x86_64.iso.sha256</a>           09-May-2023 18:43     100
<a href=""alpine-standard-3.18.0-x86_64.iso.sha512"">alpine-standard-3.18.0-x86_64.iso.sha512</a>           09-May-2023 18:43     164
<a href=""alpine-standard-3.18.0_rc1-x86_64.iso"">alpine-standard-3.18.0_rc1-x86_64.iso</a>              02-May-2023 13:59    189M
<a href=""alpine-standard-3.18.0_rc1-x86_64.iso.sha256"">alpine-standard-3.18.0_rc1-x86_64.iso.sha256</a>       02-May-2023 13:41     104
<a href=""alpine-standard-3.18.0_rc1-x86_64.iso.sha512"">alpine-standard-3.18.0_rc1-x86_64.iso.sha512</a>       02-May-2023 13:41     168
<a href=""alpine-standard-3.18.0_rc2-x86_64.iso"">alpine-standard-3.18.0_rc2-x86_64.iso</a>              02-May-2023 14:17    189M
<a href=""alpine-standard-3.18.0_rc2-x86_64.iso.sha256"">alpine-standard-3.18.0_rc2-x86_64.iso.sha256</a>       02-May-2023 14:17     104
<a href=""alpine-standard-3.18.0_rc2-x86_64.iso.sha512"">alpine-standard-3.18.0_rc2-x86_64.iso.sha512</a>       02-May-2023 14:17     168
<a href=""alpine-standard-3.18.0_rc3-x86_64.iso"">alpine-standard-3.18.0_rc3-x86_64.iso</a>              04-May-2023 15:05    189M
<a href=""alpine-standard-3.18.0_rc3-x86_64.iso.sha256"">alpine-standard-3.18.0_rc3-x86_64.iso.sha256</a>       04-May-2023 15:05     104
<a href=""alpine-standard-3.18.0_rc3-x86_64.iso.sha512"">alpine-standard-3.18.0_rc3-x86_64.iso.sha512</a>       04-May-2023 15:05     168
<a href=""alpine-standard-3.18.0_rc4-x86_64.iso"">alpine-standard-3.18.0_rc4-x86_64.iso</a>              05-May-2023 13:57    189M
<a href=""alpine-standard-3.18.0_rc4-x86_64.iso.sha256"">alpine-standard-3.18.0_rc4-x86_64.iso.sha256</a>       05-May-2023 13:57     104
<a href=""alpine-standard-3.18.0_rc4-x86_64.iso.sha512"">alpine-standard-3.18.0_rc4-x86_64.iso.sha512</a>       05-May-2023 13:57     168
<a href=""alpine-standard-3.18.0_rc5-x86_64.iso"">alpine-standard-3.18.0_rc5-x86_64.iso</a>              08-May-2023 21:08    189M
<a href=""alpine-standard-3.18.0_rc5-x86_64.iso.sha256"">alpine-standard-3.18.0_rc5-x86_64.iso.sha256</a>       08-May-2023 21:08     104
<a href=""alpine-standard-3.18.0_rc5-x86_64.iso.sha512"">alpine-standard-3.18.0_rc5-x86_64.iso.sha512</a>       08-May-2023 21:08     168
<a href=""alpine-standard-3.18.0_rc6-x86_64.iso"">alpine-standard-3.18.0_rc6-x86_64.iso</a>              09-May-2023 17:32    189M
<a href=""alpine-standard-3.18.0_rc6-x86_64.iso.sha256"">alpine-standard-3.18.0_rc6-x86_64.iso.sha256</a>       09-May-2023 17:32     104
<a href=""alpine-standard-3.18.0_rc6-x86_64.iso.sha512"">alpine-standard-3.18.0_rc6-x86_64.iso.sha512</a>       09-May-2023 17:32     168
<a href=""alpine-standard-3.18.1-x86_64.iso"">alpine-standard-3.18.1-x86_64.iso</a>                  14-Jun-2023 14:32    189M
<a href=""alpine-standard-3.18.1-x86_64.iso.sha256"">alpine-standard-3.18.1-x86_64.iso.sha256</a>           14-Jun-2023 14:32     100
<a href=""alpine-standard-3.18.1-x86_64.iso.sha512"">alpine-standard-3.18.1-x86_64.iso.sha512</a>           14-Jun-2023 14:32     164
<a href=""alpine-standard-3.18.10-x86_64.iso"">alpine-standard-3.18.10-x86_64.iso</a>                 06-Jan-2025 18:11    190M
<a href=""alpine-standard-3.18.10-x86_64.iso.sha256"">alpine-standard-3.18.10-x86_64.iso.sha256</a>          06-Jan-2025 18:11     101
<a href=""alpine-standard-3.18.10-x86_64.iso.sha512"">alpine-standard-3.18.10-x86_64.iso.sha512</a>          06-Jan-2025 18:11     165
<a href=""alpine-standard-3.18.11-x86_64.iso"">alpine-standard-3.18.11-x86_64.iso</a>                 08-Jan-2025 11:05    190M
<a href=""alpine-standard-3.18.11-x86_64.iso.asc"">alpine-standard-3.18.11-x86_64.iso.asc</a>             08-Jan-2025 16:30     833
<a href=""alpine-standard-3.18.11-x86_64.iso.sha256"">alpine-standard-3.18.11-x86_64.iso.sha256</a>          08-Jan-2025 11:05     101
<a href=""alpine-standard-3.18.11-x86_64.iso.sha512"">alpine-standard-3.18.11-x86_64.iso.sha512</a>          08-Jan-2025 11:05     165
<a href=""alpine-standard-3.18.12-x86_64.iso"">alpine-standard-3.18.12-x86_64.iso</a>                 13-Feb-2025 23:23    190M
<a href=""alpine-standard-3.18.12-x86_64.iso.asc"">alpine-standard-3.18.12-x86_64.iso.asc</a>             14-Feb-2025 00:00     833
<a href=""alpine-standard-3.18.12-x86_64.iso.sha256"">alpine-standard-3.18.12-x86_64.iso.sha256</a>          13-Feb-2025 23:23     101
<a href=""alpine-standard-3.18.12-x86_64.iso.sha512"">alpine-standard-3.18.12-x86_64.iso.sha512</a>          13-Feb-2025 23:23     165
<a href=""alpine-standard-3.18.2-x86_64.iso"">alpine-standard-3.18.2-x86_64.iso</a>                  14-Jun-2023 15:07    189M
<a href=""alpine-standard-3.18.2-x86_64.iso.asc"">alpine-standard-3.18.2-x86_64.iso.asc</a>              14-Jun-2023 15:29     833
<a href=""alpine-standard-3.18.2-x86_64.iso.sha256"">alpine-standard-3.18.2-x86_64.iso.sha256</a>           14-Jun-2023 15:07     100
<a href=""alpine-standard-3.18.2-x86_64.iso.sha512"">alpine-standard-3.18.2-x86_64.iso.sha512</a>           14-Jun-2023 15:07     164
<a href=""alpine-standard-3.18.3-x86_64.iso"">alpine-standard-3.18.3-x86_64.iso</a>                  07-Aug-2023 13:13    189M
<a href=""alpine-standard-3.18.3-x86_64.iso.asc"">alpine-standard-3.18.3-x86_64.iso.asc</a>              07-Aug-2023 15:18     833
<a href=""alpine-standard-3.18.3-x86_64.iso.sha256"">alpine-standard-3.18.3-x86_64.iso.sha256</a>           07-Aug-2023 13:13     100
<a href=""alpine-standard-3.18.3-x86_64.iso.sha512"">alpine-standard-3.18.3-x86_64.iso.sha512</a>           07-Aug-2023 13:13     164
<a href=""alpine-standard-3.18.4-x86_64.iso"">alpine-standard-3.18.4-x86_64.iso</a>                  28-Sep-2023 11:22    189M
<a href=""alpine-standard-3.18.4-x86_64.iso.asc"">alpine-standard-3.18.4-x86_64.iso.asc</a>              28-Sep-2023 13:05     833
<a href=""alpine-standard-3.18.4-x86_64.iso.sha256"">alpine-standard-3.18.4-x86_64.iso.sha256</a>           28-Sep-2023 11:22     100
<a href=""alpine-standard-3.18.4-x86_64.iso.sha512"">alpine-standard-3.18.4-x86_64.iso.sha512</a>           28-Sep-2023 11:22     164
<a href=""alpine-standard-3.18.5-x86_64.iso"">alpine-standard-3.18.5-x86_64.iso</a>                  30-Nov-2023 09:36    189M
<a href=""alpine-standard-3.18.5-x86_64.iso.asc"">alpine-standard-3.18.5-x86_64.iso.asc</a>              30-Nov-2023 12:25     833
<a href=""alpine-standard-3.18.5-x86_64.iso.sha256"">alpine-standard-3.18.5-x86_64.iso.sha256</a>           30-Nov-2023 09:36     100
<a href=""alpine-standard-3.18.5-x86_64.iso.sha512"">alpine-standard-3.18.5-x86_64.iso.sha512</a>           30-Nov-2023 09:36     164
<a href=""alpine-standard-3.18.6-x86_64.iso"">alpine-standard-3.18.6-x86_64.iso</a>                  26-Jan-2024 20:48    190M
<a href=""alpine-standard-3.18.6-x86_64.iso.asc"">alpine-standard-3.18.6-x86_64.iso.asc</a>              26-Jan-2024 21:29     833
<a href=""alpine-standard-3.18.6-x86_64.iso.sha256"">alpine-standard-3.18.6-x86_64.iso.sha256</a>           26-Jan-2024 20:48     100
<a href=""alpine-standard-3.18.6-x86_64.iso.sha512"">alpine-standard-3.18.6-x86_64.iso.sha512</a>           26-Jan-2024 20:48     164
<a href=""alpine-standard-3.18.7-x86_64.iso"">alpine-standard-3.18.7-x86_64.iso</a>                  18-Jun-2024 15:18    190M
<a href=""alpine-standard-3.18.7-x86_64.iso.sha256"">alpine-standard-3.18.7-x86_64.iso.sha256</a>           18-Jun-2024 15:18     100
<a href=""alpine-standard-3.18.7-x86_64.iso.sha512"">alpine-standard-3.18.7-x86_64.iso.sha512</a>           18-Jun-2024 15:18     164
<a href=""alpine-standard-3.18.8-x86_64.iso"">alpine-standard-3.18.8-x86_64.iso</a>                  22-Jul-2024 18:21    190M
<a href=""alpine-standard-3.18.8-x86_64.iso.asc"">alpine-standard-3.18.8-x86_64.iso.asc</a>              22-Jul-2024 19:05     833
<a href=""alpine-standard-3.18.8-x86_64.iso.sha256"">alpine-standard-3.18.8-x86_64.iso.sha256</a>           22-Jul-2024 18:21     100
<a href=""alpine-standard-3.18.8-x86_64.iso.sha512"">alpine-standard-3.18.8-x86_64.iso.sha512</a>           22-Jul-2024 18:21     164
<a href=""alpine-standard-3.18.9-x86_64.iso"">alpine-standard-3.18.9-x86_64.iso</a>                  06-Sep-2024 11:39    190M
<a href=""alpine-standard-3.18.9-x86_64.iso.asc"">alpine-standard-3.18.9-x86_64.iso.asc</a>              06-Sep-2024 12:26     833
<a href=""alpine-standard-3.18.9-x86_64.iso.sha256"">alpine-standard-3.18.9-x86_64.iso.sha256</a>           06-Sep-2024 11:39     100
<a href=""alpine-standard-3.18.9-x86_64.iso.sha512"">alpine-standard-3.18.9-x86_64.iso.sha512</a>           06-Sep-2024 11:39     164
<a href=""alpine-virt-3.18.0-x86_64.iso"">alpine-virt-3.18.0-x86_64.iso</a>                      09-May-2023 18:46     55M
<a href=""alpine-virt-3.18.0-x86_64.iso.asc"">alpine-virt-3.18.0-x86_64.iso.asc</a>                  09-May-2023 19:51     833
<a href=""alpine-virt-3.18.0-x86_64.iso.sha256"">alpine-virt-3.18.0-x86_64.iso.sha256</a>               09-May-2023 18:46      96
<a href=""alpine-virt-3.18.0-x86_64.iso.sha512"">alpine-virt-3.18.0-x86_64.iso.sha512</a>               09-May-2023 18:46     160
<a href=""alpine-virt-3.18.0_rc2-x86_64.iso"">alpine-virt-3.18.0_rc2-x86_64.iso</a>                  02-May-2023 14:19     55M
<a href=""alpine-virt-3.18.0_rc2-x86_64.iso.sha256"">alpine-virt-3.18.0_rc2-x86_64.iso.sha256</a>           02-May-2023 14:19     100
<a href=""alpine-virt-3.18.0_rc2-x86_64.iso.sha512"">alpine-virt-3.18.0_rc2-x86_64.iso.sha512</a>           02-May-2023 14:19     164
<a href=""alpine-virt-3.18.0_rc3-x86_64.iso"">alpine-virt-3.18.0_rc3-x86_64.iso</a>                  04-May-2023 15:07     55M
<a href=""alpine-virt-3.18.0_rc3-x86_64.iso.sha256"">alpine-virt-3.18.0_rc3-x86_64.iso.sha256</a>           04-May-2023 15:07     100
<a href=""alpine-virt-3.18.0_rc3-x86_64.iso.sha512"">alpine-virt-3.18.0_rc3-x86_64.iso.sha512</a>           04-May-2023 15:07     164
<a href=""alpine-virt-3.18.0_rc4-x86_64.iso"">alpine-virt-3.18.0_rc4-x86_64.iso</a>                  05-May-2023 14:00     55M
<a href=""alpine-virt-3.18.0_rc4-x86_64.iso.sha256"">alpine-virt-3.18.0_rc4-x86_64.iso.sha256</a>           05-May-2023 14:00     100
<a href=""alpine-virt-3.18.0_rc4-x86_64.iso.sha512"">alpine-virt-3.18.0_rc4-x86_64.iso.sha512</a>           05-May-2023 14:00     164
<a href=""alpine-virt-3.18.0_rc5-x86_64.iso"">alpine-virt-3.18.0_rc5-x86_64.iso</a>                  08-May-2023 21:11     55M
<a href=""alpine-virt-3.18.0_rc5-x86_64.iso.sha256"">alpine-virt-3.18.0_rc5-x86_64.iso.sha256</a>           08-May-2023 21:11     100
<a href=""alpine-virt-3.18.0_rc5-x86_64.iso.sha512"">alpine-virt-3.18.0_rc5-x86_64.iso.sha512</a>           08-May-2023 21:11     164
<a href=""alpine-virt-3.18.0_rc6-x86_64.iso"">alpine-virt-3.18.0_rc6-x86_64.iso</a>                  09-May-2023 17:35     55M
<a href=""alpine-virt-3.18.0_rc6-x86_64.iso.sha256"">alpine-virt-3.18.0_rc6-x86_64.iso.sha256</a>           09-May-2023 17:35     100
<a href=""alpine-virt-3.18.0_rc6-x86_64.iso.sha512"">alpine-virt-3.18.0_rc6-x86_64.iso.sha512</a>           09-May-2023 17:35     164
<a href=""alpine-virt-3.18.1-x86_64.iso"">alpine-virt-3.18.1-x86_64.iso</a>                      14-Jun-2023 14:36     55M
<a href=""alpine-virt-3.18.1-x86_64.iso.sha256"">alpine-virt-3.18.1-x86_64.iso.sha256</a>               14-Jun-2023 14:36      96
<a href=""alpine-virt-3.18.1-x86_64.iso.sha512"">alpine-virt-3.18.1-x86_64.iso.sha512</a>               14-Jun-2023 14:36     160
<a href=""alpine-virt-3.18.10-x86_64.iso"">alpine-virt-3.18.10-x86_64.iso</a>                     06-Jan-2025 18:12     55M
<a href=""alpine-virt-3.18.10-x86_64.iso.sha256"">alpine-virt-3.18.10-x86_64.iso.sha256</a>              06-Jan-2025 18:12      97
<a href=""alpine-virt-3.18.10-x86_64.iso.sha512"">alpine-virt-3.18.10-x86_64.iso.sha512</a>              06-Jan-2025 18:12     161
<a href=""alpine-virt-3.18.11-x86_64.iso"">alpine-virt-3.18.11-x86_64.iso</a>                     08-Jan-2025 11:07     55M
<a href=""alpine-virt-3.18.11-x86_64.iso.asc"">alpine-virt-3.18.11-x86_64.iso.asc</a>                 08-Jan-2025 16:30     833
<a href=""alpine-virt-3.18.11-x86_64.iso.sha256"">alpine-virt-3.18.11-x86_64.iso.sha256</a>              08-Jan-2025 11:07      97
<a href=""alpine-virt-3.18.11-x86_64.iso.sha512"">alpine-virt-3.18.11-x86_64.iso.sha512</a>              08-Jan-2025 11:07     161
<a href=""alpine-virt-3.18.12-x86_64.iso"">alpine-virt-3.18.12-x86_64.iso</a>                     13-Feb-2025 23:27     55M
<a href=""alpine-virt-3.18.12-x86_64.iso.asc"">alpine-virt-3.18.12-x86_64.iso.asc</a>                 14-Feb-2025 00:00     833
<a href=""alpine-virt-3.18.12-x86_64.iso.sha256"">alpine-virt-3.18.12-x86_64.iso.sha256</a>              13-Feb-2025 23:27      97
<a href=""alpine-virt-3.18.12-x86_64.iso.sha512"">alpine-virt-3.18.12-x86_64.iso.sha512</a>              13-Feb-2025 23:27     161
<a href=""alpine-virt-3.18.2-x86_64.iso"">alpine-virt-3.18.2-x86_64.iso</a>                      14-Jun-2023 15:10     55M
<a href=""alpine-virt-3.18.2-x86_64.iso.asc"">alpine-virt-3.18.2-x86_64.iso.asc</a>                  14-Jun-2023 15:29     833
<a href=""alpine-virt-3.18.2-x86_64.iso.sha256"">alpine-virt-3.18.2-x86_64.iso.sha256</a>               14-Jun-2023 15:10      96
<a href=""alpine-virt-3.18.2-x86_64.iso.sha512"">alpine-virt-3.18.2-x86_64.iso.sha512</a>               14-Jun-2023 15:10     160
<a href=""alpine-virt-3.18.3-x86_64.iso"">alpine-virt-3.18.3-x86_64.iso</a>                      07-Aug-2023 13:17     55M
<a href=""alpine-virt-3.18.3-x86_64.iso.asc"">alpine-virt-3.18.3-x86_64.iso.asc</a>                  07-Aug-2023 15:18     833
<a href=""alpine-virt-3.18.3-x86_64.iso.sha256"">alpine-virt-3.18.3-x86_64.iso.sha256</a>               07-Aug-2023 13:17      96
<a href=""alpine-virt-3.18.3-x86_64.iso.sha512"">alpine-virt-3.18.3-x86_64.iso.sha512</a>               07-Aug-2023 13:17     160
<a href=""alpine-virt-3.18.4-x86_64.iso"">alpine-virt-3.18.4-x86_64.iso</a>                      28-Sep-2023 11:25     55M
<a href=""alpine-virt-3.18.4-x86_64.iso.asc"">alpine-virt-3.18.4-x86_64.iso.asc</a>                  28-Sep-2023 13:05     833
<a href=""alpine-virt-3.18.4-x86_64.iso.sha256"">alpine-virt-3.18.4-x86_64.iso.sha256</a>               28-Sep-2023 11:25      96
<a href=""alpine-virt-3.18.4-x86_64.iso.sha512"">alpine-virt-3.18.4-x86_64.iso.sha512</a>               28-Sep-2023 11:25     160
<a href=""alpine-virt-3.18.5-x86_64.iso"">alpine-virt-3.18.5-x86_64.iso</a>                      30-Nov-2023 09:39     55M
<a href=""alpine-virt-3.18.5-x86_64.iso.asc"">alpine-virt-3.18.5-x86_64.iso.asc</a>                  30-Nov-2023 12:25     833
<a href=""alpine-virt-3.18.5-x86_64.iso.sha256"">alpine-virt-3.18.5-x86_64.iso.sha256</a>               30-Nov-2023 09:39      96
<a href=""alpine-virt-3.18.5-x86_64.iso.sha512"">alpine-virt-3.18.5-x86_64.iso.sha512</a>               30-Nov-2023 09:39     160
<a href=""alpine-virt-3.18.6-x86_64.iso"">alpine-virt-3.18.6-x86_64.iso</a>                      26-Jan-2024 20:50     55M
<a href=""alpine-virt-3.18.6-x86_64.iso.asc"">alpine-virt-3.18.6-x86_64.iso.asc</a>                  26-Jan-2024 21:29     833
<a href=""alpine-virt-3.18.6-x86_64.iso.sha256"">alpine-virt-3.18.6-x86_64.iso.sha256</a>               26-Jan-2024 20:50      96
<a href=""alpine-virt-3.18.6-x86_64.iso.sha512"">alpine-virt-3.18.6-x86_64.iso.sha512</a>               26-Jan-2024 20:50     160
<a href=""alpine-virt-3.18.7-x86_64.iso"">alpine-virt-3.18.7-x86_64.iso</a>                      18-Jun-2024 15:22     55M
<a href=""alpine-virt-3.18.7-x86_64.iso.sha256"">alpine-virt-3.18.7-x86_64.iso.sha256</a>               18-Jun-2024 15:22      96
<a href=""alpine-virt-3.18.7-x86_64.iso.sha512"">alpine-virt-3.18.7-x86_64.iso.sha512</a>               18-Jun-2024 15:22     160
<a href=""alpine-virt-3.18.8-x86_64.iso"">alpine-virt-3.18.8-x86_64.iso</a>                      22-Jul-2024 18:24     55M
<a href=""alpine-virt-3.18.8-x86_64.iso.asc"">alpine-virt-3.18.8-x86_64.iso.asc</a>                  22-Jul-2024 19:05     833
<a href=""alpine-virt-3.18.8-x86_64.iso.sha256"">alpine-virt-3.18.8-x86_64.iso.sha256</a>               22-Jul-2024 18:24      96
<a href=""alpine-virt-3.18.8-x86_64.iso.sha512"">alpine-virt-3.18.8-x86_64.iso.sha512</a>               22-Jul-2024 18:24     160
<a href=""alpine-virt-3.18.9-x86_64.iso"">alpine-virt-3.18.9-x86_64.iso</a>                      06-Sep-2024 11:41     55M
<a href=""alpine-virt-3.18.9-x86_64.iso.asc"">alpine-virt-3.18.9-x86_64.iso.asc</a>                  06-Sep-2024 12:26     833
<a href=""alpine-virt-3.18.9-x86_64.iso.sha256"">alpine-virt-3.18.9-x86_64.iso.sha256</a>               06-Sep-2024 11:41      96
<a href=""alpine-virt-3.18.9-x86_64.iso.sha512"">alpine-virt-3.18.9-x86_64.iso.sha512</a>               06-Sep-2024 11:41     160
<a href=""alpine-xen-3.18.0-x86_64.iso"">alpine-xen-3.18.0-x86_64.iso</a>                       09-May-2023 18:46    221M
<a href=""alpine-xen-3.18.0-x86_64.iso.asc"">alpine-xen-3.18.0-x86_64.iso.asc</a>                   09-May-2023 19:51     833
<a href=""alpine-xen-3.18.0-x86_64.iso.sha256"">alpine-xen-3.18.0-x86_64.iso.sha256</a>                09-May-2023 18:46      95
<a href=""alpine-xen-3.18.0-x86_64.iso.sha512"">alpine-xen-3.18.0-x86_64.iso.sha512</a>                09-May-2023 18:46     159
<a href=""alpine-xen-3.18.0_rc2-x86_64.iso"">alpine-xen-3.18.0_rc2-x86_64.iso</a>                   02-May-2023 14:19    221M
<a href=""alpine-xen-3.18.0_rc2-x86_64.iso.sha256"">alpine-xen-3.18.0_rc2-x86_64.iso.sha256</a>            02-May-2023 14:19      99
<a href=""alpine-xen-3.18.0_rc2-x86_64.iso.sha512"">alpine-xen-3.18.0_rc2-x86_64.iso.sha512</a>            02-May-2023 14:19     163
<a href=""alpine-xen-3.18.0_rc3-x86_64.iso"">alpine-xen-3.18.0_rc3-x86_64.iso</a>                   04-May-2023 15:07    221M
<a href=""alpine-xen-3.18.0_rc3-x86_64.iso.sha256"">alpine-xen-3.18.0_rc3-x86_64.iso.sha256</a>            04-May-2023 15:07      99
<a href=""alpine-xen-3.18.0_rc3-x86_64.iso.sha512"">alpine-xen-3.18.0_rc3-x86_64.iso.sha512</a>            04-May-2023 15:07     163
<a href=""alpine-xen-3.18.0_rc4-x86_64.iso"">alpine-xen-3.18.0_rc4-x86_64.iso</a>                   05-May-2023 14:00    221M
<a href=""alpine-xen-3.18.0_rc4-x86_64.iso.sha256"">alpine-xen-3.18.0_rc4-x86_64.iso.sha256</a>            05-May-2023 14:00      99
<a href=""alpine-xen-3.18.0_rc4-x86_64.iso.sha512"">alpine-xen-3.18.0_rc4-x86_64.iso.sha512</a>            05-May-2023 14:00     163
<a href=""alpine-xen-3.18.0_rc5-x86_64.iso"">alpine-xen-3.18.0_rc5-x86_64.iso</a>                   08-May-2023 21:11    221M
<a href=""alpine-xen-3.18.0_rc5-x86_64.iso.sha256"">alpine-xen-3.18.0_rc5-x86_64.iso.sha256</a>            08-May-2023 21:11      99
<a href=""alpine-xen-3.18.0_rc5-x86_64.iso.sha512"">alpine-xen-3.18.0_rc5-x86_64.iso.sha512</a>            08-May-2023 21:11     163
<a href=""alpine-xen-3.18.0_rc6-x86_64.iso"">alpine-xen-3.18.0_rc6-x86_64.iso</a>                   09-May-2023 17:35    221M
<a href=""alpine-xen-3.18.0_rc6-x86_64.iso.sha256"">alpine-xen-3.18.0_rc6-x86_64.iso.sha256</a>            09-May-2023 17:35      99
<a href=""alpine-xen-3.18.0_rc6-x86_64.iso.sha512"">alpine-xen-3.18.0_rc6-x86_64.iso.sha512</a>            09-May-2023 17:35     163
<a href=""alpine-xen-3.18.1-x86_64.iso"">alpine-xen-3.18.1-x86_64.iso</a>                       14-Jun-2023 14:36    221M
<a href=""alpine-xen-3.18.1-x86_64.iso.sha256"">alpine-xen-3.18.1-x86_64.iso.sha256</a>                14-Jun-2023 14:36      95
<a href=""alpine-xen-3.18.1-x86_64.iso.sha512"">alpine-xen-3.18.1-x86_64.iso.sha512</a>                14-Jun-2023 14:36     159
<a href=""alpine-xen-3.18.10-x86_64.iso"">alpine-xen-3.18.10-x86_64.iso</a>                      06-Jan-2025 18:12    223M
<a href=""alpine-xen-3.18.10-x86_64.iso.sha256"">alpine-xen-3.18.10-x86_64.iso.sha256</a>               06-Jan-2025 18:12      96
<a href=""alpine-xen-3.18.10-x86_64.iso.sha512"">alpine-xen-3.18.10-x86_64.iso.sha512</a>               06-Jan-2025 18:12     160
<a href=""alpine-xen-3.18.11-x86_64.iso"">alpine-xen-3.18.11-x86_64.iso</a>                      08-Jan-2025 11:07    223M
<a href=""alpine-xen-3.18.11-x86_64.iso.asc"">alpine-xen-3.18.11-x86_64.iso.asc</a>                  08-Jan-2025 16:30     833
<a href=""alpine-xen-3.18.11-x86_64.iso.sha256"">alpine-xen-3.18.11-x86_64.iso.sha256</a>               08-Jan-2025 11:07      96
<a href=""alpine-xen-3.18.11-x86_64.iso.sha512"">alpine-xen-3.18.11-x86_64.iso.sha512</a>               08-Jan-2025 11:07     160
<a href=""alpine-xen-3.18.12-x86_64.iso"">alpine-xen-3.18.12-x86_64.iso</a>                      13-Feb-2025 23:27    223M
<a href=""alpine-xen-3.18.12-x86_64.iso.asc"">alpine-xen-3.18.12-x86_64.iso.asc</a>                  14-Feb-2025 00:00     833
<a href=""alpine-xen-3.18.12-x86_64.iso.sha256"">alpine-xen-3.18.12-x86_64.iso.sha256</a>               13-Feb-2025 23:27      96
<a href=""alpine-xen-3.18.12-x86_64.iso.sha512"">alpine-xen-3.18.12-x86_64.iso.sha512</a>               13-Feb-2025 23:27     160
<a href=""alpine-xen-3.18.2-x86_64.iso"">alpine-xen-3.18.2-x86_64.iso</a>                       14-Jun-2023 15:10    221M
<a href=""alpine-xen-3.18.2-x86_64.iso.asc"">alpine-xen-3.18.2-x86_64.iso.asc</a>                   14-Jun-2023 15:29     833
<a href=""alpine-xen-3.18.2-x86_64.iso.sha256"">alpine-xen-3.18.2-x86_64.iso.sha256</a>                14-Jun-2023 15:10      95
<a href=""alpine-xen-3.18.2-x86_64.iso.sha512"">alpine-xen-3.18.2-x86_64.iso.sha512</a>                14-Jun-2023 15:10     159
<a href=""alpine-xen-3.18.3-x86_64.iso"">alpine-xen-3.18.3-x86_64.iso</a>                       07-Aug-2023 13:17    222M
<a href=""alpine-xen-3.18.3-x86_64.iso.asc"">alpine-xen-3.18.3-x86_64.iso.asc</a>                   07-Aug-2023 15:18     833
<a href=""alpine-xen-3.18.3-x86_64.iso.sha256"">alpine-xen-3.18.3-x86_64.iso.sha256</a>                07-Aug-2023 13:17      95
<a href=""alpine-xen-3.18.3-x86_64.iso.sha512"">alpine-xen-3.18.3-x86_64.iso.sha512</a>                07-Aug-2023 13:17     159
<a href=""alpine-xen-3.18.4-x86_64.iso"">alpine-xen-3.18.4-x86_64.iso</a>                       28-Sep-2023 11:25    222M
<a href=""alpine-xen-3.18.4-x86_64.iso.asc"">alpine-xen-3.18.4-x86_64.iso.asc</a>                   28-Sep-2023 13:05     833
<a href=""alpine-xen-3.18.4-x86_64.iso.sha256"">alpine-xen-3.18.4-x86_64.iso.sha256</a>                28-Sep-2023 11:25      95
<a href=""alpine-xen-3.18.4-x86_64.iso.sha512"">alpine-xen-3.18.4-x86_64.iso.sha512</a>                28-Sep-2023 11:25     159
<a href=""alpine-xen-3.18.5-x86_64.iso"">alpine-xen-3.18.5-x86_64.iso</a>                       30-Nov-2023 09:39    222M
<a href=""alpine-xen-3.18.5-x86_64.iso.asc"">alpine-xen-3.18.5-x86_64.iso.asc</a>                   30-Nov-2023 12:25     833
<a href=""alpine-xen-3.18.5-x86_64.iso.sha256"">alpine-xen-3.18.5-x86_64.iso.sha256</a>                30-Nov-2023 09:39      95
<a href=""alpine-xen-3.18.5-x86_64.iso.sha512"">alpine-xen-3.18.5-x86_64.iso.sha512</a>                30-Nov-2023 09:39     159
<a href=""alpine-xen-3.18.6-x86_64.iso"">alpine-xen-3.18.6-x86_64.iso</a>                       26-Jan-2024 20:50    222M
<a href=""alpine-xen-3.18.6-x86_64.iso.asc"">alpine-xen-3.18.6-x86_64.iso.asc</a>                   26-Jan-2024 21:29     833
<a href=""alpine-xen-3.18.6-x86_64.iso.sha256"">alpine-xen-3.18.6-x86_64.iso.sha256</a>                26-Jan-2024 20:51      95
<a href=""alpine-xen-3.18.6-x86_64.iso.sha512"">alpine-xen-3.18.6-x86_64.iso.sha512</a>                26-Jan-2024 20:51     159
<a href=""alpine-xen-3.18.7-x86_64.iso"">alpine-xen-3.18.7-x86_64.iso</a>                       18-Jun-2024 15:22    223M
<a href=""alpine-xen-3.18.7-x86_64.iso.sha256"">alpine-xen-3.18.7-x86_64.iso.sha256</a>                18-Jun-2024 15:22      95
<a href=""alpine-xen-3.18.7-x86_64.iso.sha512"">alpine-xen-3.18.7-x86_64.iso.sha512</a>                18-Jun-2024 15:22     159
<a href=""alpine-xen-3.18.8-x86_64.iso"">alpine-xen-3.18.8-x86_64.iso</a>                       22-Jul-2024 18:24    223M
<a href=""alpine-xen-3.18.8-x86_64.iso.asc"">alpine-xen-3.18.8-x86_64.iso.asc</a>                   22-Jul-2024 19:05     833
<a href=""alpine-xen-3.18.8-x86_64.iso.sha256"">alpine-xen-3.18.8-x86_64.iso.sha256</a>                22-Jul-2024 18:24      95
<a href=""alpine-xen-3.18.8-x86_64.iso.sha512"">alpine-xen-3.18.8-x86_64.iso.sha512</a>                22-Jul-2024 18:24     159
<a href=""alpine-xen-3.18.9-x86_64.iso"">alpine-xen-3.18.9-x86_64.iso</a>                       06-Sep-2024 11:41    223M
<a href=""alpine-xen-3.18.9-x86_64.iso.asc"">alpine-xen-3.18.9-x86_64.iso.asc</a>                   06-Sep-2024 12:26     833
<a href=""alpine-xen-3.18.9-x86_64.iso.sha256"">alpine-xen-3.18.9-x86_64.iso.sha256</a>                06-Sep-2024 11:41      95
<a href=""alpine-xen-3.18.9-x86_64.iso.sha512"">alpine-xen-3.18.9-x86_64.iso.sha512</a>                06-Sep-2024 11:41     159
<a href=""latest-releases.yaml"">latest-releases.yaml</a>                               13-Feb-2025 23:27    3336
</pre><hr></body>
</html>
";

    #endregion

    /// <summary>
    /// Verifies that the Alpine version searcher can successfully locate stable releases for a specific version and
    /// flavor using a simulated HTTP response.
    /// </summary>
    /// <remarks>This test ensures that the search functionality correctly identifies available stable Alpine
    /// Linux images for the 'virt' flavor and 'x86_64' architecture when provided with a mock directory listing. It
    /// validates that the searcher returns a non-empty result set for the exact version specified.</remarks>
    /// <returns></returns>
    [Theory]
    [Feature(nameof(IAlpineVersionSearcher), TestKind.Unit, IntegrationKind.Isolated)]
    [Trait(Internet.Test, Internet.Offline)]
    [Trait(Kind.Test, Kind.Unit)]
    [InlineData("3.18.1", AlpineFlavors.Virt, "virt", AlpineArchitectures.cloud, "cloud")]
    [InlineData("3.18.2", AlpineFlavors.Virt, "virt", AlpineArchitectures.cloud, "cloud")]
    [InlineData("3.18.1", AlpineFlavors.Standard, "standard", AlpineArchitectures.x86_64, "x86_64")]
    [InlineData("3.18.1", AlpineFlavors.Extended, "extended", AlpineArchitectures.x86_64, "x86_64")]
    [InlineData("3.18.1", AlpineFlavors.Xen, "xen", AlpineArchitectures.x86_64, "x86_64")]
    public async Task Can_Search_Stable_On_FakeGet(string searchVersion, AlpineFlavors searchFlavor, string foundFlavor, AlpineArchitectures searchArch, string foundArch)
    {
        await AlpineVersionSearcherTester.ValidAsync(
            getHttpClientBuilder: (b) => b
            .Response((r) => r.Ok().Content(AlpineVersions))
            .Response((r) => r.Ok().Content(AlpineVersion_3_18_Architectures))
            .Response((r) => r.Ok().Content(AlpineVersion_3_18_ArchitecturesData))
            .Response((r) => r.Ok().Content("sha"))
            .Response((r) => r.Ok().Content("sha")),
            filterBuilder: (filters) => filters.WithExactVersion(searchVersion).WithFlavor(searchFlavor).WithArch(searchArch),
            assert: (list) =>
            {
                list.ShouldNotBeEmpty();
                list.First().Architecture.ShouldBeEquivalentTo(foundArch);
                list.First().Flavor.ShouldBeEquivalentTo(foundFlavor);
                list.First().Version.ShouldBeEquivalentTo(searchVersion);
                list.First().Url.ShouldStartWith($"https://dl-cdn.alpinelinux.org/alpine/v{AlpineVersion.From(searchVersion).ToMajorMinor()}/releases/");
                list.First().Sha512.ShouldBe("sha");
                list.First().Sha256.ShouldBe("sha");
            });
    }

    /// <summary>
    /// Verifies that the AlpineVersionSearcher can successfully find stable Alpine Linux versions matching the
    /// specified version, flavor, and architecture.
    /// </summary>
    /// <remarks>This test ensures that the searcher returns at least one result matching the specified
    /// criteria and that the result's properties correspond to the provided version, flavor, and
    /// architecture.</remarks>
    /// <param name="searchingVersion">The exact version string of Alpine Linux to search for. Must correspond to a stable release version.</param>
    /// <param name="flavor">The flavor of Alpine Linux to filter the search results by.</param>
    /// <param name="architecture">The architecture to filter the search results by.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [Trait(Internet.Test, Internet.Online)]
    [Trait(Kind.Test, Kind.Integration)]
    [Trait(Integration.Test, Integration.InfrastructureIntegration)]
    [InlineData("3.18.2", AlpineFlavors.Virt, AlpineArchitectures.x86_64)]
    [InlineData("3.19.0", AlpineFlavors.Virt, AlpineArchitectures.x86_64)]
    [InlineData("3.20.0", AlpineFlavors.Virt, AlpineArchitectures.x86_64)]
    [InlineData("3.21.0", AlpineFlavors.Virt, AlpineArchitectures.x86_64)]
    [InlineData("3.22.0", AlpineFlavors.Virt, AlpineArchitectures.x86_64)]
    public async Task Can_Search_ExactVersion_And_Flavor_And_Arch_On_Internet(string searchingVersion, AlpineFlavors flavor, AlpineArchitectures architecture)
    {
        await AlpineVersionSearcherTester.ValidAsync(
            searcher: new AlpineVersionSearcher(new HttpClient.HttpClient(new System.Net.Http.HttpClient())),
            filterBuilder: (filtersBuilder) =>
                filtersBuilder
                    .WithExactVersion(searchingVersion)
                    .WithFlavors([flavor])
                    .WithArchs([architecture]),
            assert: (result) =>
            {
                result.ShouldNotBeNull();
                result.Count.ShouldBeGreaterThan(0);
                result.ElementAt(0).Version.ToString().ShouldBe(searchingVersion);
                result.ElementAt(0).Flavor.ShouldBeEquivalentTo(flavor.ToString().ToLowerInvariant());
                result.ElementAt(0).Architecture.ShouldBeEquivalentTo(architecture.ToString().ToLowerInvariant());
            });
    }
}
