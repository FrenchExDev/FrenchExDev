using FrenchExDev.Net.QualityGate.Abstractions;
using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Tests.Fakes;

internal sealed class FakeCoverageParser : ICoverageReportParser
{
    private readonly CoverageReport? _report;

    public FakeCoverageParser(CoverageReport? report = null) => _report = report;

    public CoverageReport? TryParseGlobs(string baseDir, List<string>? globs) => _report;
}
