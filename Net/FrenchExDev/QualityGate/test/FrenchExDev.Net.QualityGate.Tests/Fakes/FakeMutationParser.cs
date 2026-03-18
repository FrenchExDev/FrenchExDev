using FrenchExDev.Net.QualityGate.Abstractions;
using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Tests.Fakes;

internal sealed class FakeMutationParser : IMutationReportParser
{
    private readonly MutationReport? _report;

    public FakeMutationParser(MutationReport? report = null) => _report = report;

    public MutationReport? TryParseGlobs(string baseDir, List<string>? globs) => _report;
}
