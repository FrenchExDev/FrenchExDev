using FrenchExDev.Net.QualityGate.Abstractions;
using FrenchExDev.Net.QualityGate.Analysis;
using FrenchExDev.Net.QualityGate.Config;
using FrenchExDev.Net.QualityGate.Gates;
using FrenchExDev.Net.QualityGate.Model;
using FrenchExDev.Net.QualityGate.Reports;

namespace FrenchExDev.Net.QualityGate;

/// <summary>
/// Orchestrates all quality-gate analyzers, report parsers, and gate evaluation
/// to produce a comprehensive <see cref="QualityReport"/>.
/// </summary>
public class QualityEngine
{
    private readonly QualityGateConfig _config;
    private readonly ISolutionLoader _solutionLoader;
    private readonly ICoverageReportParser _coverageParser;
    private readonly IMutationReportParser _mutationParser;
    private readonly IReportWriter _reportWriter;

    public QualityEngine(
        QualityGateConfig config,
        ISolutionLoader? solutionLoader = null,
        ICoverageReportParser? coverageParser = null,
        IMutationReportParser? mutationParser = null,
        IReportWriter? reportWriter = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _solutionLoader = solutionLoader ?? new MsBuildSolutionLoader();
        _coverageParser = coverageParser ?? new DefaultCoverageReportParser();
        _mutationParser = mutationParser ?? new DefaultMutationReportParser();
        _reportWriter = reportWriter ?? new DefaultReportWriter();
    }

    public async Task<QualityReport> AnalyzeAsync(CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.Solution);

        var solution = await _solutionLoader.LoadAsync(_config.Solution).ConfigureAwait(false);

        var projectMetricsList = new List<ProjectMetrics>();
        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            projectMetricsList.Add(await ProjectAnalyzer.AnalyzeAsync(project, solution, ct).ConfigureAwait(false));
        }

        var solutionDir = Path.GetDirectoryName(Path.GetFullPath(_config.Solution)) ?? ".";

        var report = new QualityReport
        {
            SolutionPath = _config.Solution,
            Timestamp = DateTimeOffset.UtcNow,
            Projects = projectMetricsList,
            Coverage = _coverageParser.TryParseGlobs(solutionDir, _config.CoverageGlobs),
            Mutation = _mutationParser.TryParseGlobs(solutionDir, _config.MutationGlobs)
        };

        return new QualityReport
        {
            SolutionPath = report.SolutionPath,
            Timestamp = report.Timestamp,
            Projects = report.Projects,
            Coverage = report.Coverage,
            Mutation = report.Mutation,
            Duplication = report.Duplication,
            GateResults = QualityGateEvaluator.Evaluate(report, _config.Gates)
        };
    }

    public async Task<string> RunAsync(CancellationToken ct = default)
    {
        var report = await AnalyzeAsync(ct).ConfigureAwait(false);
        var outputRoot = Path.GetFullPath(_config.Output);
        return await _reportWriter.WriteAsync(report, outputRoot, ct).ConfigureAwait(false);
    }
}
