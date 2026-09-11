using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Abstractions;

public interface ICoverageReportParser
{
    CoverageReport? TryParseGlobs(string baseDir, List<string>? globs);
}
