using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Abstractions;

public interface IMutationReportParser
{
    MutationReport? TryParseGlobs(string baseDir, List<string>? globs);
}
