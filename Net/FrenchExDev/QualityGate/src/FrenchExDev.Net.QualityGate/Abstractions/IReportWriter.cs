using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Abstractions;

public interface IReportWriter
{
    Task<string> WriteAsync(QualityReport report, string outputRoot, CancellationToken ct = default);
}
