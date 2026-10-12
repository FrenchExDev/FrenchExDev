using System.Text.Json.Serialization;

namespace FrenchExDev.Net.QualityGate.Model;

public class QualityReport
{
    [JsonPropertyName("solutionPath")]
    public required string SolutionPath { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("projects")]
    public List<ProjectMetrics> Projects { get; init; } = [];

    [JsonPropertyName("gateResults")]
    public List<QualityGateResult> GateResults { get; init; } = [];

    [JsonPropertyName("duplication")]
    public DuplicationReport? Duplication { get; init; }

    [JsonPropertyName("coverage")]
    public CoverageReport? Coverage { get; init; }

    [JsonPropertyName("mutation")]
    public MutationReport? Mutation { get; init; }
}
