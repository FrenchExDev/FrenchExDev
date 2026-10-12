using FrenchExDev.Net.QualityGate.Abstractions;
using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Reports;

internal sealed class DefaultMutationReportParser : IMutationReportParser
{
    public MutationReport? TryParseGlobs(string baseDir, List<string>? globs)
        => StrykerReportParser.TryParseGlobs(baseDir, globs);
}
