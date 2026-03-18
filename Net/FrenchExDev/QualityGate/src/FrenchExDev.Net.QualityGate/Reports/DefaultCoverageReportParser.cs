using FrenchExDev.Net.QualityGate.Abstractions;
using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Reports;

internal sealed class DefaultCoverageReportParser : ICoverageReportParser
{
    public CoverageReport? TryParseGlobs(string baseDir, List<string>? globs)
        => CoberturaParser.TryParseGlobs(baseDir, globs);
}
