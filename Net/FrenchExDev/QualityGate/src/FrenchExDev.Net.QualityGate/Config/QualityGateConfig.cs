using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrenchExDev.Net.QualityGate.Config;

public class QualityGateConfig
{
    [YamlMember(Alias = "solution")]
    public string? Solution { get; set; }

    [YamlMember(Alias = "coverage")]
    public List<string>? CoverageGlobs { get; set; }

    [YamlMember(Alias = "mutations")]
    public List<string>? MutationGlobs { get; set; }

    [YamlMember(Alias = "output")]
    public string Output { get; set; } = ".quality-gate/";

    [YamlMember(Alias = "gates")]
    public GateThresholds Gates { get; set; } = new();

    [YamlMember(Alias = "exclude")]
    public List<string>? ExcludePatterns { get; set; }

    public static QualityGateConfig Load(string path)
    {
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<QualityGateConfig>(yaml) ?? new QualityGateConfig();
    }

    public static QualityGateConfig Default() => new();
}

public class GateThresholds
{
    [YamlMember(Alias = "max-cyclomatic-complexity")]
    public int MaxCyclomaticComplexity { get; set; } = 15;

    [YamlMember(Alias = "max-cognitive-complexity")]
    public int MaxCognitiveComplexity { get; set; } = 20;

    [YamlMember(Alias = "max-class-coupling")]
    public int MaxClassCoupling { get; set; } = 20;

    [YamlMember(Alias = "max-inheritance-depth")]
    public int MaxInheritanceDepth { get; set; } = 5;

    [YamlMember(Alias = "min-maintainability-index")]
    public double MinMaintainabilityIndex { get; set; } = 60;

    [YamlMember(Alias = "max-lcom")]
    public int MaxLcom { get; set; } = 3;

    [YamlMember(Alias = "max-distance-from-main-sequence")]
    public double MaxDistanceFromMainSequence { get; set; } = 0.3;

    [YamlMember(Alias = "max-duplication-percent")]
    public double MaxDuplicationPercent { get; set; } = 5;

    [YamlMember(Alias = "min-test-quality-score")]
    public double MinTestQualityScore { get; set; } = 0.80;
}
