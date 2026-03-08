using System.CommandLine;
using System.Diagnostics;

var inputArg = new Argument<DirectoryInfo>("input")
{
    Description = "Folder containing document files to convert"
};

var parallelOption = new Option<int>("--parallel", "-p")
{
    Description = "Number of parallel conversions (default: 1)",
    DefaultValueFactory = _ => 1
};

var wordOption = new Option<bool>("--word", "-w")
{
    Description = "Include Word files (.docx, .doc)",
    DefaultValueFactory = _ => false
};

var excelOption = new Option<bool>("--excel", "-e")
{
    Description = "Include Excel files (.xlsx, .xls)",
    DefaultValueFactory = _ => false
};

var powerpointOption = new Option<bool>("--powerpoint", "-pp")
{
    Description = "Include PowerPoint files (.pptx, .ppt)",
    DefaultValueFactory = _ => false
};

var rootCommand = new RootCommand("Convert document files to PDF using LibreOffice")
{
    inputArg,
    parallelOption,
    wordOption,
    excelOption,
    powerpointOption
};

rootCommand.SetAction(async (parseResult, ct) =>
{
    var input = parseResult.GetValue(inputArg)!;
    var parallelism = parseResult.GetValue(parallelOption);
    var includeWord = parseResult.GetValue(wordOption);
    var includeExcel = parseResult.GetValue(excelOption);
    var includePowerpoint = parseResult.GetValue(powerpointOption);

    // If none specified, include all
    if (!includeWord && !includeExcel && !includePowerpoint)
    {
        includeWord = true;
        includeExcel = true;
        includePowerpoint = true;
    }

    if (!input.Exists)
    {
        Console.Error.WriteLine($"Input folder not found: {input.FullName}");
        return;
    }

    var extensions = new List<string>();
    if (includeWord) extensions.AddRange([".docx", ".doc"]);
    if (includeExcel) extensions.AddRange([".xlsx", ".xls"]);
    if (includePowerpoint) extensions.AddRange([".pptx", ".ppt"]);

    var files = input.GetFiles("*", SearchOption.TopDirectoryOnly)
        .Where(f => extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
        .ToArray();

    if (files.Length == 0)
    {
        Console.Error.WriteLine("No matching files found in the input folder.");
        return;
    }

    var soffice = FindLibreOffice();
    if (soffice is null)
    {
        Console.Error.WriteLine("LibreOffice not found. Install it and ensure 'soffice' is in PATH or installed in the default location.");
        return;
    }

    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    var outputDir = Path.Combine(input.FullName, "output", timestamp);
    Directory.CreateDirectory(outputDir);

    Console.WriteLine($"Converting {files.Length} file(s) -> {outputDir}");
    Console.WriteLine($"Parallelism: {parallelism}");

    var sw = Stopwatch.StartNew();
    var succeeded = 0;
    var failed = 0;

    await Parallel.ForEachAsync(
        files,
        new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
        async (file, token) =>
        {
            var ok = await ConvertAsync(soffice, file.FullName, outputDir, token);
            if (ok)
            {
                Interlocked.Increment(ref succeeded);
                Console.WriteLine($"  OK  {file.Name}");
            }
            else
            {
                Interlocked.Increment(ref failed);
                Console.Error.WriteLine($"  FAIL  {file.Name}");
            }
        });

    sw.Stop();
    Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s — {succeeded} succeeded, {failed} failed.");
});

return await rootCommand.Parse(args).InvokeAsync();

static async Task<bool> ConvertAsync(string soffice, string inputPath, string outputDir, CancellationToken ct)
{
    var profileDir = Path.Combine(Path.GetTempPath(), "doc2pdf_" + Guid.NewGuid().ToString("N"));

    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = soffice,
            ArgumentList =
            {
                "--headless",
                "--convert-to", "pdf",
                "--outdir", outputDir,
                $"-env:UserInstallation=file:///{profileDir.Replace('\\', '/')}",
                inputPath
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync(ct);
        return proc.ExitCode == 0;
    }
    finally
    {
        try { Directory.Delete(profileDir, recursive: true); } catch { }
    }
}

static string? FindLibreOffice()
{
    var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
    foreach (var dir in pathDirs)
    {
        var candidate = Path.Combine(dir, "soffice.exe");
        if (File.Exists(candidate)) return candidate;
        candidate = Path.Combine(dir, "soffice");
        if (File.Exists(candidate)) return candidate;
    }

    if (OperatingSystem.IsWindows())
    {
        var programFiles = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (var pf in programFiles)
        {
            var libreDir = Path.Combine(pf, "LibreOffice", "program");
            var candidate = Path.Combine(libreDir, "soffice.exe");
            if (File.Exists(candidate)) return candidate;
        }
    }

    foreach (var path in new[] { "/usr/bin/soffice", "/usr/local/bin/soffice", "/Applications/LibreOffice.app/Contents/MacOS/soffice" })
    {
        if (File.Exists(path)) return path;
    }

    return null;
}
