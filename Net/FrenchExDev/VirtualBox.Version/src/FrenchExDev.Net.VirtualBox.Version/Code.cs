using System.Diagnostics;

namespace FrenchExDev.Net.VirtualBox.Version;

/// <summary>
/// Defines a mechanism for asynchronously discovering the installed VirtualBox system version.
/// </summary>
public interface IVirtualBoxSystemVersionDiscoverer
{
    /// <summary>
    /// Asynchronously discovers the installed VirtualBox version on the local system.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the discovery operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see
    /// cref="VirtualBoxVersionRecord"/> describing the detected VirtualBox version, or <c>null</c> if VirtualBox is not
    /// installed.</returns>
    Task<VirtualBoxVersionRecord> DiscoverAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a contract for searching VirtualBox version information based on specified filters.
/// </summary>
/// <remarks>Implementations of this interface provide asynchronous search capabilities for VirtualBox version
/// details. The search operation can be customized using filtering criteria and supports cancellation via a
/// cancellation token.</remarks>
public interface IVirtualBoxVersionInformationSearcher
{
    /// <summary>
    /// Asynchronously searches for VirtualBox version information that matches the specified filters.
    /// </summary>
    /// <param name="filters">The criteria used to filter VirtualBox version information results. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of VirtualBoxVersionInfos
    /// objects matching the specified filters. The list will be empty if no matches are found.</returns>
    Task<List<VirtualBoxVersionInfos>> SearchAsync(
        VirtualBoxVersionInformationSearchingFilters filters, CancellationToken cancellationToken = default);
}


/// <summary>
/// Provides functionality to discover the installed VirtualBox system version by invoking the VBoxManage command-line
/// tool.
/// </summary>
/// <remarks>This class uses the VBoxManage utility to retrieve version information from the local VirtualBox
/// installation. Ensure that VBoxManage is available in the system's PATH before using this class. Typically, this
/// class is used to obtain version details for compatibility checks or diagnostics.</remarks>
public class VirtualBoxSystemVersionDiscoverer : IVirtualBoxSystemVersionDiscoverer
{
    /// <summary>
    /// Retrieves the current version information of VirtualBox by executing the VBoxManage command asynchronously.
    /// </summary>
    /// <remarks>The method requires VBoxManage to be installed and accessible via the system PATH. The
    /// operation may take several seconds to complete, depending on system performance.</remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A VirtualBoxVersionRecord containing the major, minor, patch, and build revision of the installed VirtualBox
    /// version.</returns>
    /// <exception cref="Exception">Thrown if the VBoxManage process cannot be started. This may indicate that VBoxManage is not available in the
    /// system PATH.</exception>
    public async Task<VirtualBoxVersionRecord> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "VBoxManage",
                Arguments = "--version",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory
            }
        };

        var started = process.Start();

        if (!started) throw new Exception("error starting VBoxManage. Check it is in PATH");

        await process.WaitForExitAsync(cancellationToken);

        var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken);

        var split = stdOut.Split(".");

        var patch = split[2];
        var patchSplit = patch.Split("r");

        return new VirtualBoxVersionRecord(split[0], split[1], patchSplit[0], patchSplit[1].Replace("\r\n", ""));
    }
}

/// <summary>
/// Provides functionality to search for VirtualBox version information and retrieve associated checksums from the
/// official VirtualBox download site.
/// </summary>
/// <remarks>This class is intended for use in scenarios where accurate VirtualBox version and checksum data is
/// required, such as validating downloads or automating deployment processes. Instances of this class are thread-safe
/// for concurrent use. Implements the IVirtualBoxVersionInformationSearcher interface.</remarks>
public sealed class VirtualBoxVersionInformationSearcher : IVirtualBoxVersionInformationSearcher
{
    /// <summary>
    /// Represents the URL pattern used to retrieve SHA256 checksum files for VirtualBox releases.
    /// </summary>
    /// <remarks>Replace the #VERSION# placeholder in the pattern with the desired VirtualBox version to
    /// construct the full URL for the corresponding SHA256SUMS file.</remarks>
    private static readonly string ShaChecksumsUrlPattern = "https://download.virtualbox.org/virtualbox/#VERSION#/SHA256SUMS";

    /// <summary>
    /// Stores the HttpClient instance used for making HTTP requests to retrieve version information.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the VirtualBoxVersionInformationSearcher class, optionally using a specified
    /// HttpClient for HTTP requests.
    /// </summary>
    /// <remarks>Providing a custom HttpClient allows for advanced configuration, such as custom handlers,
    /// timeouts, or sharing the client across multiple components. If not specified, the class will manage its own
    /// HttpClient instance.</remarks>
    /// <param name="httpClient">The HttpClient instance to use for sending HTTP requests. If null, a new HttpClient will be created and used
    /// internally.</param>
    public VirtualBoxVersionInformationSearcher(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Asynchronously searches for VirtualBox version information matching the specified filters.
    /// </summary>
    /// <param name="filters">The filters to apply when searching for VirtualBox version information. The <c>ExactVersion</c> property must be
    /// specified and non-empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see
    /// cref="VirtualBoxVersionInfos"/> objects matching the search criteria. The list will be empty if no matching
    /// information is found.</returns>
    /// <exception cref="InvalidDataException">Thrown if <paramref name="filters"/>.ExactVersion is null or empty.</exception>
    public async Task<List<VirtualBoxVersionInfos>> SearchAsync(VirtualBoxVersionInformationSearchingFilters filters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filters.ExactVersion))
            throw new InvalidDataException(nameof(filters.ExactVersion));

        var results = new List<VirtualBoxVersionInfos>();

        var uriVersionChecksums = new Uri(ShaChecksumsUrlPattern.Replace("#VERSION#", filters.ExactVersion));

        var getVersionChecksumsResponseMessage = await _httpClient.GetAsync(uriVersionChecksums, cancellationToken);
        var versionChecksumsInfosHtml = await getVersionChecksumsResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        var versionChecksumsInfosHtmlSplit = versionChecksumsInfosHtml.Split("\n");

        foreach (var versionChecksumInfoHtml in versionChecksumsInfosHtmlSplit)
        {
            var versionChecksumInfosHtmlLineSplit = versionChecksumInfoHtml.Split(" ");
            if (versionChecksumInfosHtmlLineSplit.Length > 0 && !versionChecksumInfosHtmlLineSplit[1].StartsWith("*VBoxGuestAdditions_")) continue;

            var searchResult = new VirtualBoxVersionInfos(
                Version: filters.ExactVersion,
                AdditionsIsoSha256: versionChecksumInfosHtmlLineSplit[0]
            );

            results.Add(searchResult);
            break;
        }

        return results;
    }
}

/// <summary>
/// Represents filters used to search for VirtualBox version information, specifying criteria such as an exact version
/// match.
/// </summary>
/// <param name="ExactVersion">The exact VirtualBox version to match during the search. If specified, only results matching this version will be
/// included.</param>
public sealed record VirtualBoxVersionInformationSearchingFilters(string ExactVersion);

/// <summary>
/// Represents VirtualBox version information, including the version string and the SHA-256 hash of the associated Guest
/// Additions ISO.
/// </summary>
/// <param name="Version">The VirtualBox version string, typically in the format 'major.minor.patch'.</param>
/// <param name="AdditionsIsoSha256">The SHA-256 hash of the VirtualBox Guest Additions ISO file corresponding to the specified version.</param>
public record VirtualBoxVersionInfos(string Version, string AdditionsIsoSha256);

/// <summary>
/// Represents a version identifier for VirtualBox, including major, minor, patch, and release number components.
/// </summary>
/// <param name="Major">The major version number of VirtualBox. Typically indicates significant changes or milestones.</param>
/// <param name="Minor">The minor version number of VirtualBox. Used for incremental feature updates.</param>
/// <param name="Patch">The patch version number of VirtualBox. Specifies maintenance or bug fix releases.</param>
/// <param name="ReleaseNumber">The release number for the VirtualBox version. Distinguishes builds or revisions within the same version.</param>
public record VirtualBoxVersionRecord(string Major, string Minor, string Patch, string ReleaseNumber)
{
    /// <summary>
    /// Returns a string representation of the version in the format 'Major.Minor.Patch', excluding any release or
    /// pre-release information.
    /// </summary>
    /// <returns>A string containing the major, minor, and patch components of the version, separated by periods.</returns>
    public string ToStringWithoutRelease()
    {
        return $"{Major}.{Minor}.{Patch}";
    }
}