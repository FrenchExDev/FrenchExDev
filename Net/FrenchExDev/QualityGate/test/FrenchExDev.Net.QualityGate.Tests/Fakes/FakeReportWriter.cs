using FrenchExDev.Net.QualityGate.Abstractions;
using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Tests.Fakes;

internal sealed class FakeReportWriter : IReportWriter
{
    public QualityReport? LastReport { get; private set; }
    public string? LastOutputRoot { get; private set; }
    public int WriteCount { get; private set; }

    public Task<string> WriteAsync(QualityReport report, string outputRoot, CancellationToken ct = default)
    {
        LastReport = report;
        LastOutputRoot = outputRoot;
        WriteCount++;
        return Task.FromResult(Path.Combine(outputRoot, "fake-run"));
    }
}
