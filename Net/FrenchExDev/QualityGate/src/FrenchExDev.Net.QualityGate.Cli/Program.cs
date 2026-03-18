using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using FrenchExDev.Net.QualityGate;
using FrenchExDev.Net.QualityGate.Cli;
using FrenchExDev.Net.QualityGate.Config;
using FrenchExDev.Net.QualityGate.Model;

// --- Shared Options ---
var configOption = new Option<string?>("--config")
{
    Description = "Path to quality-gate.yml (default: quality-gate.yml in current directory)"
};

var solutionOption = new Option<string?>("--solution")
{
    Description = "Override the solution path from config"
};

var serveOption = new Option<bool>("--serve")
{
    Description = "After analysis, launch npx serve on the output directory"
};

var portOption = new Option<int>("--port")
{
    Description = "Port for npx serve",
    DefaultValueFactory = _ => 3000
};

var settingsOption = new Option<string?>("--settings")
{
    Description = "Path to coverage.runsettings (default: coverage.runsettings in solution directory)"
};

// --- Loop Options (shared by analyze and test) ---
var loopOption = new Option<bool>("--loop")
{
    Description = "Re-run after completion"
};

var watchOption = new Option<bool>("--watch")
{
    Description = "In loop mode, trigger re-run on file changes"
};

var manualOption = new Option<bool>("--manual")
{
    Description = "In loop mode, trigger re-run on Enter key"
};

var interactiveOption = new Option<bool>("--interactive")
{
    Description = "Alias for --loop --manual --serve"
};

// --- init command ---
var initForceOption = new Option<bool>("--force")
{
    Description = "Overwrite existing files"
};

var initSolutionOption = new Option<string?>("--solution")
{
    Description = "Path to solution file (auto-detects *.slnx/*.sln in cwd if omitted)"
};

var initCommand = new Command("init", "Scaffold quality-gate.yml + coverage.runsettings for a solution");
initCommand.Options.Add(initSolutionOption);
initCommand.Options.Add(initForceOption);

initCommand.SetAction((parseResult, ct) =>
{
    var solutionPath = parseResult.GetValue(initSolutionOption);
    var force = parseResult.GetValue(initForceOption);

    // 1. Find solution file
    if (solutionPath is null)
    {
        var slnxFiles = Directory.GetFiles(".", "*.slnx");
        if (slnxFiles.Length > 0)
        {
            solutionPath = slnxFiles[0];
        }
        else
        {
            var slnFiles = Directory.GetFiles(".", "*.sln");
            if (slnFiles.Length > 0)
                solutionPath = slnFiles[0];
        }
    }

    if (solutionPath is null || !File.Exists(solutionPath))
    {
        Console.Error.WriteLine(solutionPath is null
            ? "No solution file found. Use --solution or run from a directory containing *.slnx/*.sln."
            : $"Solution file not found: {solutionPath}");
        Environment.ExitCode = 1;
        return Task.CompletedTask;
    }

    solutionPath = Path.GetFullPath(solutionPath);
    var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";
    var solutionFileName = Path.GetFileName(solutionPath);

    // 2. Parse solution file to find .csproj paths
    var csprojPaths = new List<string>();

    if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
    {
        var doc = XDocument.Load(solutionPath);
        foreach (var proj in doc.Descendants("Project"))
        {
            var pathAttr = proj.Attribute("Path")?.Value;
            if (pathAttr is not null && pathAttr.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                csprojPaths.Add(Path.Combine(solutionDir, pathAttr.Replace('\\', '/')));
        }
    }
    else
    {
        var slnContent = File.ReadAllText(solutionPath);
        var regex = new Regex(@"Project\(""[^""]*""\)\s*=\s*""[^""]*""\s*,\s*""([^""]+\.csproj)""", RegexOptions.IgnoreCase);
        foreach (Match match in regex.Matches(slnContent))
        {
            csprojPaths.Add(Path.Combine(solutionDir, match.Groups[1].Value.Replace('\\', '/')));
        }
    }

    // 3. Read each .csproj to determine assembly name and test status
    var nonTestAssemblies = new List<string>();

    foreach (var csprojPath in csprojPaths)
    {
        if (!File.Exists(csprojPath))
            continue;

        var csprojContent = File.ReadAllText(csprojPath);
        var csprojDoc = XDocument.Parse(csprojContent);

        var isTestProject = csprojDoc.Descendants("IsTestProject")
            .Any(e => string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));

        if (!isTestProject)
        {
            var assemblyName = csprojDoc.Descendants("AssemblyName").FirstOrDefault()?.Value.Trim();
            if (string.IsNullOrEmpty(assemblyName))
                assemblyName = Path.GetFileNameWithoutExtension(csprojPath);

            nonTestAssemblies.Add(assemblyName);
        }
    }

    // 4. Generate quality-gate.yml
    var qualityGatePath = Path.Combine(solutionDir, "quality-gate.yml");
    if (!force && File.Exists(qualityGatePath))
    {
        Console.Error.WriteLine($"quality-gate.yml already exists at {qualityGatePath}. Use --force to overwrite.");
        Environment.ExitCode = 1;
        return Task.CompletedTask;
    }

    var qualityGateYml = $"""
        solution: {solutionFileName}

        coverage:
          - "**/coverage.cobertura.xml"

        mutations:
          - "**/mutation-report.json"

        output: .quality-gate/

        gates:
          max-cyclomatic-complexity: 15
          max-cognitive-complexity: 20
          max-class-coupling: 20
          max-inheritance-depth: 5
          min-maintainability-index: 60
          max-lcom: 3
          max-distance-from-main-sequence: 0.3
          max-duplication-percent: 5
          min-test-quality-score: 0.80

        exclude:
          - "**/obj/**"
          - "**/bin/**"
        """;

    File.WriteAllText(qualityGatePath, qualityGateYml, Encoding.UTF8);
    Console.WriteLine($"Created: {qualityGatePath}");

    // 5. Generate coverage.runsettings
    var runsettingsPath = Path.Combine(solutionDir, "coverage.runsettings");
    if (!force && File.Exists(runsettingsPath))
    {
        Console.Error.WriteLine($"coverage.runsettings already exists at {runsettingsPath}. Use --force to overwrite.");
        Environment.ExitCode = 1;
        return Task.CompletedTask;
    }

    var includeFilter = string.Join(",", nonTestAssemblies.Select(a => $"[{a}]*"));

    var runsettingsXml = $"""
        <?xml version="1.0" encoding="utf-8" ?>
        <RunSettings>
          <DataCollectionRunSettings>
            <DataCollectors>
              <DataCollector friendlyName="XPlat Code Coverage">
                <Configuration>
                  <Format>cobertura</Format>
                  <Include>{includeFilter}</Include>
                  <ExcludeByAttribute>ExcludeFromCodeCoverageAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
                </Configuration>
              </DataCollector>
            </DataCollectors>
          </DataCollectionRunSettings>
        </RunSettings>
        """;

    File.WriteAllText(runsettingsPath, runsettingsXml, Encoding.UTF8);
    Console.WriteLine($"Created: {runsettingsPath}");

    // 6. Summary
    Console.WriteLine();
    Console.WriteLine($"Scaffolded quality gate for {solutionFileName}:");
    Console.WriteLine($"  - {nonTestAssemblies.Count} non-test assemblies included in coverage");
    Console.WriteLine($"  - {csprojPaths.Count} total projects found");

    return Task.CompletedTask;
});

// --- validate command ---
var validateConfigOption = new Option<string?>("--config")
{
    Description = "Path to quality-gate.yml (default: quality-gate.yml in current directory)"
};

var validateCommand = new Command("validate", "Check config validity without running analysis");
validateCommand.Options.Add(validateConfigOption);

validateCommand.SetAction((parseResult, ct) =>
{
    var configPath = parseResult.GetValue(validateConfigOption) ?? "quality-gate.yml";
    var hasError = false;

    // 1. Config file exists and parses as valid YAML
    if (!File.Exists(configPath))
    {
        Console.WriteLine("[X] Config file exists: not found at " + configPath);
        Environment.ExitCode = 1;
        return Task.CompletedTask;
    }

    QualityGateConfig config;
    try
    {
        config = QualityGateConfig.Load(configPath);
        Console.WriteLine("[OK] Config file parses as valid YAML");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[X] Config file parses as valid YAML: {ex.Message}");
        Environment.ExitCode = 1;
        return Task.CompletedTask;
    }

    // 2. Solution field set and file exists
    if (string.IsNullOrWhiteSpace(config.Solution))
    {
        Console.WriteLine("[X] Solution field is set");
        hasError = true;
    }
    else
    {
        var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";
        var solutionFullPath = Path.IsPathRooted(config.Solution)
            ? config.Solution
            : Path.Combine(configDir, config.Solution);

        if (File.Exists(solutionFullPath))
        {
            Console.WriteLine($"[OK] Solution file exists: {config.Solution}");
        }
        else
        {
            Console.WriteLine($"[X] Solution file exists: not found at {solutionFullPath}");
            hasError = true;
        }
    }

    // 3. Gate thresholds validation
    var gates = config.Gates;
    var gateErrors = new List<string>();

    if (gates.MaxCyclomaticComplexity <= 0) gateErrors.Add("max-cyclomatic-complexity must be a positive integer");
    if (gates.MaxCognitiveComplexity <= 0) gateErrors.Add("max-cognitive-complexity must be a positive integer");
    if (gates.MaxClassCoupling <= 0) gateErrors.Add("max-class-coupling must be a positive integer");
    if (gates.MaxInheritanceDepth <= 0) gateErrors.Add("max-inheritance-depth must be a positive integer");
    if (gates.MinMaintainabilityIndex < 0 || gates.MinMaintainabilityIndex > 100) gateErrors.Add("min-maintainability-index must be between 0 and 100");
    if (gates.MaxLcom <= 0) gateErrors.Add("max-lcom must be a positive integer");
    if (gates.MaxDistanceFromMainSequence < 0 || gates.MaxDistanceFromMainSequence > 1) gateErrors.Add("max-distance-from-main-sequence must be between 0 and 1");
    if (gates.MaxDuplicationPercent < 0 || gates.MaxDuplicationPercent > 100) gateErrors.Add("max-duplication-percent must be between 0 and 100");
    if (gates.MinTestQualityScore < 0 || gates.MinTestQualityScore > 1) gateErrors.Add("min-test-quality-score must be between 0 and 1");

    if (gateErrors.Count == 0)
    {
        Console.WriteLine("[OK] Gate thresholds are valid");
    }
    else
    {
        foreach (var err in gateErrors)
            Console.WriteLine($"[X] Gate threshold: {err}");
        hasError = true;
    }

    // 4. coverage.runsettings exists in solution dir (warn if missing)
    if (!string.IsNullOrWhiteSpace(config.Solution))
    {
        var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";
        var solDir = Path.GetDirectoryName(
            Path.IsPathRooted(config.Solution)
                ? config.Solution
                : Path.Combine(configDir, config.Solution)) ?? configDir;

        var runsettings = Path.Combine(solDir, "coverage.runsettings");
        if (File.Exists(runsettings))
        {
            Console.WriteLine("[OK] coverage.runsettings exists");
        }
        else
        {
            Console.WriteLine("[WARN] coverage.runsettings not found in solution directory (run 'init' to create it)");
        }
    }

    // 5. Output directory is creatable
    try
    {
        var outputDir = Path.GetFullPath(config.Output);
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        Console.WriteLine($"[OK] Output directory is accessible: {config.Output}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[X] Output directory: {ex.Message}");
        hasError = true;
    }

    if (hasError)
        Environment.ExitCode = 1;

    return Task.CompletedTask;
});

// --- analyze command (enhanced) ---
var analyzeCommand = new Command("analyze", "Analyze solution quality and produce a report");
analyzeCommand.Options.Add(configOption);
analyzeCommand.Options.Add(solutionOption);
analyzeCommand.Options.Add(serveOption);
analyzeCommand.Options.Add(portOption);
analyzeCommand.Options.Add(loopOption);
analyzeCommand.Options.Add(watchOption);
analyzeCommand.Options.Add(manualOption);
analyzeCommand.Options.Add(interactiveOption);

analyzeCommand.SetAction(async (parseResult, ct) =>
{
    var configPath = parseResult.GetValue(configOption);
    var solution = parseResult.GetValue(solutionOption);
    var serve = parseResult.GetValue(serveOption);
    var port = parseResult.GetValue(portOption);
    var loop = parseResult.GetValue(loopOption);
    var watch = parseResult.GetValue(watchOption);
    var manual = parseResult.GetValue(manualOption);
    var interactive = parseResult.GetValue(interactiveOption);

    if (interactive) { loop = true; manual = true; serve = true; }

    var config = LoadConfig(configPath);
    if (solution is not null)
        config.Solution = solution;

    var solutionDir = config.Solution is not null
        ? Path.GetDirectoryName(Path.GetFullPath(config.Solution)) ?? "."
        : ".";

    await RunLoopAsync(
        async (iterCt) =>
        {
            var engine = new QualityEngine(config);
            var outputDir = await engine.RunAsync(iterCt);
            var report = await engine.AnalyzeAsync(iterCt);
            return (outputDir, report);
        },
        loop, watch, manual, serve, port, solutionDir, ct);
});

// --- check command (enhanced with --no-build) ---
var checkNoBuildOption = new Option<bool>("--no-build")
{
    Description = "Skip dotnet build before analysis"
};

var checkCommand = new Command("check", "Analyze and exit with code 1 if any gate fails");
checkCommand.Options.Add(configOption);
checkCommand.Options.Add(solutionOption);
checkCommand.Options.Add(checkNoBuildOption);

checkCommand.SetAction(async (parseResult, ct) =>
{
    var configPath = parseResult.GetValue(configOption);
    var solution = parseResult.GetValue(solutionOption);
    var noBuild = parseResult.GetValue(checkNoBuildOption);

    var config = LoadConfig(configPath);
    if (solution is not null)
        config.Solution = solution;

    if (!noBuild)
    {
        var solutionDir = config.Solution is not null
            ? Path.GetDirectoryName(Path.GetFullPath(config.Solution)) ?? "."
            : ".";

        if (!RunBuild(solutionDir))
        {
            Environment.ExitCode = 1;
            return;
        }
    }

    var engine = new QualityEngine(config);
    var outputDir = await engine.RunAsync(ct);
    var report = await engine.AnalyzeAsync(ct);

    PrintSummary(report);

    var failed = report.GateResults.Any(g => !g.Passed);
    if (failed)
    {
        Console.Error.WriteLine("Quality gate check FAILED.");
        Environment.ExitCode = 1;
    }
});

// --- serve command (unchanged) ---
var serveCommand = new Command("serve", "Launch npx serve on the output directory (no analysis)");
serveCommand.Options.Add(configOption);
serveCommand.Options.Add(portOption);

serveCommand.SetAction((parseResult) =>
{
    var configPath = parseResult.GetValue(configOption);
    var port = parseResult.GetValue(portOption);

    var config = LoadConfig(configPath);
    var outputDir = Path.GetFullPath(config.Output);

    if (!Directory.Exists(outputDir))
    {
        Console.Error.WriteLine($"Output directory not found: {outputDir}");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Serving {outputDir} on port {port}...");
    LaunchServe(outputDir, port, background: false);
});

// --- interfaces command (unchanged) ---
var interfacesCommand = new Command("interfaces", "Analyze and print interface map to console");
interfacesCommand.Options.Add(configOption);
interfacesCommand.Options.Add(solutionOption);

interfacesCommand.SetAction(async (parseResult, ct) =>
{
    var configPath = parseResult.GetValue(configOption);
    var solution = parseResult.GetValue(solutionOption);

    var config = LoadConfig(configPath);
    if (solution is not null)
        config.Solution = solution;

    var engine = new QualityEngine(config);
    var report = await engine.AnalyzeAsync(ct);

    PrintInterfaceMap(report);
});

// --- test command (enhanced) ---
var testBuildOption = new Option<bool>("--build")
{
    Description = "Run dotnet build first (default: true)",
    DefaultValueFactory = _ => true
};

var testNoBuildOption = new Option<bool>("--no-build")
{
    Description = "Skip build and pass --no-build to dotnet test"
};

var testExcludeOption = new Option<string[]>("--exclude")
{
    Description = "Exclude project patterns from test run"
};

var testLoopOption = new Option<bool>("--loop")
{
    Description = "Re-run after completion"
};

var testWatchOption = new Option<bool>("--watch")
{
    Description = "In loop mode, trigger re-run on file changes"
};

var testManualOption = new Option<bool>("--manual")
{
    Description = "In loop mode, trigger re-run on Enter key"
};

var testInteractiveOption = new Option<bool>("--interactive")
{
    Description = "Alias for --loop --manual --serve"
};

var testCommand = new Command("test", "Run tests with coverage collection, then analyze quality gates");
testCommand.Options.Add(configOption);
testCommand.Options.Add(solutionOption);
testCommand.Options.Add(settingsOption);
testCommand.Options.Add(serveOption);
testCommand.Options.Add(portOption);
testCommand.Options.Add(testBuildOption);
testCommand.Options.Add(testNoBuildOption);
testCommand.Options.Add(testExcludeOption);
testCommand.Options.Add(testLoopOption);
testCommand.Options.Add(testWatchOption);
testCommand.Options.Add(testManualOption);
testCommand.Options.Add(testInteractiveOption);

testCommand.SetAction(async (parseResult, ct) =>
{
    var configPath = parseResult.GetValue(configOption);
    var solution = parseResult.GetValue(solutionOption);
    var settingsPath = parseResult.GetValue(settingsOption);
    var serve = parseResult.GetValue(serveOption);
    var port = parseResult.GetValue(portOption);
    var build = parseResult.GetValue(testBuildOption);
    var noBuild = parseResult.GetValue(testNoBuildOption);
    var exclude = parseResult.GetValue(testExcludeOption) ?? [];
    var loop = parseResult.GetValue(testLoopOption);
    var watch = parseResult.GetValue(testWatchOption);
    var manual = parseResult.GetValue(testManualOption);
    var interactive = parseResult.GetValue(testInteractiveOption);

    if (interactive) { loop = true; manual = true; serve = true; }
    if (noBuild) build = false;

    var config = LoadConfig(configPath);
    if (solution is not null)
        config.Solution = solution;

    var solutionDir = config.Solution is not null
        ? Path.GetDirectoryName(Path.GetFullPath(config.Solution)) ?? "."
        : ".";

    await RunLoopAsync(
        async (iterCt) =>
        {
            if (build)
            {
                if (!RunBuild(solutionDir))
                {
                    Environment.ExitCode = 1;
                    return (config.Output, EmptyReport());
                }
            }

            if (!RunCoverage(solutionDir, settingsPath, noBuild, exclude))
            {
                Environment.ExitCode = 1;
                return (config.Output, EmptyReport());
            }

            var engine = new QualityEngine(config);
            var outputDir = await engine.RunAsync(iterCt);
            var report = await engine.AnalyzeAsync(iterCt);
            return (outputDir, report);
        },
        loop, watch, manual, serve, port, solutionDir, ct);
});

// --- coverage command (unchanged) ---
var coverageCommand = new Command("coverage", "Run tests with XPlat Code Coverage collection (no analysis)");
coverageCommand.Options.Add(configOption);
coverageCommand.Options.Add(solutionOption);
coverageCommand.Options.Add(settingsOption);

coverageCommand.SetAction((parseResult) =>
{
    var configPath = parseResult.GetValue(configOption);
    var solution = parseResult.GetValue(solutionOption);
    var settingsPath = parseResult.GetValue(settingsOption);

    var config = LoadConfig(configPath);
    if (solution is not null)
        config.Solution = solution;

    var solutionDir = config.Solution is not null
        ? Path.GetDirectoryName(Path.GetFullPath(config.Solution)) ?? "."
        : ".";

    if (!RunCoverage(solutionDir, settingsPath, noBuild: false, exclude: []))
    {
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine("Coverage collection complete.");
});

// --- Root ---
var rootCommand = new RootCommand("FrenchExDev Quality Gate CLI");
rootCommand.Subcommands.Add(initCommand);
rootCommand.Subcommands.Add(validateCommand);
rootCommand.Subcommands.Add(analyzeCommand);
rootCommand.Subcommands.Add(checkCommand);
rootCommand.Subcommands.Add(testCommand);
rootCommand.Subcommands.Add(coverageCommand);
rootCommand.Subcommands.Add(serveCommand);
rootCommand.Subcommands.Add(interfacesCommand);

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

// ===========================================================================
// Helpers
// ===========================================================================

static QualityGateConfig LoadConfig(string? configPath)
{
    var path = configPath ?? "quality-gate.yml";
    if (File.Exists(path))
        return QualityGateConfig.Load(path);
    return QualityGateConfig.Default();
}

static void PrintSummary(QualityReport report)
{
    Console.WriteLine();
    Console.WriteLine("===== Quality Gate Report =====");
    Console.WriteLine($"  Solution : {report.SolutionPath}");
    Console.WriteLine($"  Timestamp: {report.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine($"  Projects : {report.Projects.Count}");
    Console.WriteLine();

    // Metrics overview per project.
    foreach (var project in report.Projects)
    {
        var totalTypes = project.Namespaces.Sum(ns => ns.Types.Count);
        var totalMethods = project.Namespaces.Sum(ns => ns.Types.Sum(t => t.Methods.Count));
        Console.WriteLine($"  [{project.Name}]");
        Console.WriteLine($"    Namespaces: {project.Namespaces.Count}  Types: {totalTypes}  Methods: {totalMethods}");
        Console.WriteLine($"    Interfaces: {project.Interfaces.Count}  Implementations: {project.Implementations.Count}  Orphans: {project.OrphanInterfaces.Count}");
    }

    Console.WriteLine();

    // Coverage.
    if (report.Coverage is not null)
        Console.WriteLine($"  Coverage: line={report.Coverage.LineRate:P1}, branch={report.Coverage.BranchRate:P1}");

    // Mutation.
    if (report.Mutation is not null)
        Console.WriteLine($"  Mutation: score={report.Mutation.MutationScore:P1}, killed={report.Mutation.Killed}/{report.Mutation.TotalMutants}");

    // Gate results.
    Console.WriteLine();
    Console.WriteLine("  --- Gate Results ---");
    if (report.GateResults.Count == 0)
    {
        Console.WriteLine("  No gates evaluated.");
    }
    else
    {
        var passed = report.GateResults.Count(g => g.Passed);
        var failed = report.GateResults.Count(g => !g.Passed);
        Console.WriteLine($"  Passed: {passed}  Failed: {failed}  Total: {report.GateResults.Count}");
        Console.WriteLine();

        foreach (var gate in report.GateResults)
        {
            var indicator = gate.Passed ? "[PASS]" : "[FAIL]";
            Console.WriteLine($"  {indicator} {gate.GateName}: {gate.Description}");
            Console.WriteLine($"         Threshold: {gate.Threshold}  Actual: {gate.ActualValue}");
            if (gate.ViolatingElement is not null)
                Console.WriteLine($"         Element: {gate.ViolatingElement}");
        }
    }

    Console.WriteLine();
    var allPassed = report.GateResults.All(g => g.Passed);
    Console.WriteLine(allPassed
        ? "  All quality gates PASSED."
        : "  Quality gates FAILED.");
    Console.WriteLine();
}

static void PrintInterfaceMap(QualityReport report)
{
    Console.WriteLine();
    Console.WriteLine("===== Interface Map =====");
    Console.WriteLine();

    foreach (var project in report.Projects)
    {
        if (project.Interfaces.Count == 0)
            continue;

        Console.WriteLine($"  [{project.Name}]");

        foreach (var iface in project.Interfaces)
        {
            Console.WriteLine($"    {iface.FullName}");

            var impls = project.Implementations
                .Where(i => i.InterfaceFullName == iface.FullName)
                .ToList();

            if (impls.Count == 0)
            {
                Console.WriteLine("      (no implementations)");
            }
            else
            {
                foreach (var impl in impls)
                    Console.WriteLine($"      -> {impl.ImplementingTypeFullName}");
            }
        }

        Console.WriteLine();
    }
}

// ===========================================================================
// Infrastructure helpers
// ===========================================================================

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
static bool RunCoverage(string solutionDir, string? settingsPath, bool noBuild, string[] exclude)
{
    var runsettings = settingsPath ?? Path.Combine(solutionDir, "coverage.runsettings");
    var resultsDir = Path.Combine(solutionDir, "coverage-results");

    // Clean previous results.
    if (Directory.Exists(resultsDir))
    {
        Console.WriteLine($"Cleaning {resultsDir}...");
        try { Directory.Delete(resultsDir, true); }
        catch (IOException ex) { Console.Error.WriteLine($"Warning: could not clean coverage-results: {ex.Message}"); }
    }

    // Build dotnet test arguments.
    var testArgs = $"test --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDir}\"";
    if (File.Exists(runsettings))
    {
        testArgs += $" --settings \"{runsettings}\"";
        Console.WriteLine($"Using runsettings: {runsettings}");
    }

    if (noBuild)
        testArgs += " --no-build";

    foreach (var pattern in exclude)
        testArgs += $" --filter \"FullyQualifiedName!~{pattern}\"";

    Console.WriteLine($"Running: dotnet {testArgs}");
    Console.WriteLine();

    var isWindows = OperatingSystem.IsWindows();
    var psi = new ProcessStartInfo
    {
        FileName = isWindows ? "cmd.exe" : "dotnet",
        Arguments = isWindows ? $"/c dotnet {testArgs}" : testArgs,
        WorkingDirectory = solutionDir,
        UseShellExecute = false
    };

    using var process = Process.Start(psi);
    if (process is null)
    {
        Console.Error.WriteLine("Failed to start dotnet test.");
        return false;
    }

    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        Console.Error.WriteLine($"dotnet test failed with exit code {process.ExitCode}.");
        return false;
    }

    Console.WriteLine();
    Console.WriteLine("Tests passed. Coverage collected.");
    Console.WriteLine();
    return true;
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
static bool RunBuild(string solutionDir)
{
    Console.WriteLine("Running: dotnet build");
    Console.WriteLine();

    var isWindows = OperatingSystem.IsWindows();
    var psi = new ProcessStartInfo
    {
        FileName = isWindows ? "cmd.exe" : "dotnet",
        Arguments = isWindows ? "/c dotnet build" : "build",
        WorkingDirectory = solutionDir,
        UseShellExecute = false
    };

    using var process = Process.Start(psi);
    if (process is null)
    {
        Console.Error.WriteLine("Failed to start dotnet build.");
        return false;
    }

    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        Console.Error.WriteLine($"dotnet build failed with exit code {process.ExitCode}.");
        return false;
    }

    Console.WriteLine();
    Console.WriteLine("Build succeeded.");
    Console.WriteLine();
    return true;
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
static void LaunchServe(string outputDir, int port, bool background)
{
    Console.WriteLine($"Starting npx serve on {outputDir} (port {port})...");
    if (!background)
    {
        Console.WriteLine("Press Ctrl+C to stop.");
    }
    Console.WriteLine();

    var isWindows = OperatingSystem.IsWindows();
    var psi = new ProcessStartInfo
    {
        FileName = isWindows ? "cmd.exe" : "npx",
        Arguments = isWindows
            ? $"/c npx serve \"{outputDir}\" -l {port}"
            : $"serve \"{outputDir}\" -l {port}",
        UseShellExecute = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false
    };

    var process = Process.Start(psi);
    if (process is null)
    {
        Console.Error.WriteLine("Failed to start npx serve.");
        Environment.ExitCode = 1;
        return;
    }

    if (!background)
    {
        process.WaitForExit();
        process.Dispose();
    }
    // In background mode, the process runs detached and is not awaited.
}

// ===========================================================================
// Loop controller
// ===========================================================================

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
static async Task RunLoopAsync(
    Func<CancellationToken, Task<(string outputDir, QualityReport report)>> runIteration,
    bool loop, bool watch, bool manual, bool serve, int port,
    string solutionDir, CancellationToken ct)
{
    WebSocketReloadServer? wsServer = null;
    bool firstIteration = true;

    do
    {
        var (outputDir, report) = await runIteration(ct);
        PrintSummary(report);

        if (serve && firstIteration)
        {
            // Launch npx serve in background (don't block!)
            _ = Task.Run(() => LaunchServe(outputDir, port, background: true), ct);
            wsServer = new WebSocketReloadServer(port + 1);
            _ = wsServer.StartAsync(ct);
            firstIteration = false;
        }
        else if (wsServer is not null)
        {
            await wsServer.NotifyReloadAsync();
        }

        if (!loop) break;

        if (watch && manual) await WaitForFileChangeOrEnter(solutionDir, ct);
        else if (watch) await WaitForFileChange(solutionDir, ct);
        else if (manual) await WaitForEnter(ct);

    } while (!ct.IsCancellationRequested);

    wsServer?.Dispose();
}

// ===========================================================================
// File watcher & manual wait
// ===========================================================================

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
static Task WaitForFileChange(string dir, CancellationToken ct)
{
    var tcs = new TaskCompletionSource();
    Timer? debounceTimer = null;
    string? lastFile = null;

    var watcher = new FileSystemWatcher(dir)
    {
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
    };

    watcher.Filters.Add("*.cs");
    watcher.Filters.Add("*.csproj");
    watcher.Filters.Add("*.props");

    void OnChanged(object sender, FileSystemEventArgs e)
    {
        lastFile = e.Name;
        debounceTimer?.Dispose();
        debounceTimer = new Timer(_ =>
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            Console.WriteLine($"Change detected in {lastFile}, re-running...");
            tcs.TrySetResult();
        }, null, 500, Timeout.Infinite);
    }

    watcher.Changed += OnChanged;
    watcher.Created += OnChanged;
    watcher.Renamed += (s, e) => OnChanged(s, e);

    ct.Register(() =>
    {
        debounceTimer?.Dispose();
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        tcs.TrySetCanceled();
    });

    Console.WriteLine("Watching for file changes...");
    return tcs.Task;
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
static Task WaitForEnter(CancellationToken ct)
{
    Console.WriteLine("Press Enter to re-run (Ctrl+C to exit)...");

    var tcs = new TaskCompletionSource();

    ct.Register(() => tcs.TrySetCanceled());

    _ = Task.Run(() =>
    {
        Console.In.ReadLine();
        tcs.TrySetResult();
    }, ct);

    return tcs.Task;
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
static Task WaitForFileChangeOrEnter(string dir, CancellationToken ct)
{
    Console.WriteLine("Waiting for file change or Enter key...");

    var fileTask = WaitForFileChange(dir, ct);
    var enterTask = WaitForEnter(ct);

    return Task.WhenAny(fileTask, enterTask);
}

// ===========================================================================
// Misc helpers
// ===========================================================================

static QualityReport EmptyReport() => new()
{
    SolutionPath = "(none)",
    Timestamp = DateTimeOffset.UtcNow
};
