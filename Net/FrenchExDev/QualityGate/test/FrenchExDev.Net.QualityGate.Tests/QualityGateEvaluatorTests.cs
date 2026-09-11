using FrenchExDev.Net.QualityGate.Config;
using FrenchExDev.Net.QualityGate.Gates;
using FrenchExDev.Net.QualityGate.Model;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class QualityGateEvaluatorTests
{
    private static GateThresholds DefaultThresholds() => new()
    {
        MaxCyclomaticComplexity = 10,
        MaxCognitiveComplexity = 15,
        MaxClassCoupling = 20,
        MaxInheritanceDepth = 3,
        MinMaintainabilityIndex = 60,
        MaxLcom = 3,
        MaxDistanceFromMainSequence = 0.5,
        MaxDuplicationPercent = 5,
        MinTestQualityScore = 0.80
    };

    private static QualityReport Report(
        List<ProjectMetrics>? projects = null,
        CoverageReport? coverage = null,
        MutationReport? mutation = null,
        DuplicationReport? duplication = null) => new()
    {
        SolutionPath = "test.slnx",
        Timestamp = DateTimeOffset.UtcNow,
        Projects = projects ?? [],
        Coverage = coverage,
        Mutation = mutation,
        Duplication = duplication
    };

    private static MethodMetrics Method(
        int cc = 1, int cog = 0, double? mi = 90, string name = "M") => new()
    {
        Name = name,
        FullName = $"C.{name}",
        Line = 1,
        CyclomaticComplexity = cc,
        CognitiveComplexity = cog,
        LinesOfCode = 5,
        ParameterCount = 0,
        MaintainabilityIndex = mi
    };

    private static TypeMetrics Type(
        List<MethodMetrics>? methods = null,
        int coupling = 5, int lcom = 1, int depth = 0, string name = "MyType") => new()
    {
        Name = name,
        FullName = $"Ns.{name}",
        FilePath = "file.cs",
        Line = 1,
        Kind = TypeKind.Class,
        MethodCount = methods?.Count ?? 0,
        PropertyCount = 0,
        FieldCount = 0,
        InheritanceDepth = depth,
        Lcom4 = lcom,
        EfferentCoupling = coupling,
        CyclomaticComplexity = 1,
        CognitiveComplexity = 0,
        LinesOfCode = 10,
        Methods = methods ?? []
    };

    private static NamespaceMetrics Namespace(
        List<TypeMetrics>? types = null,
        int ca = 5, int ce = 5, int abstractCount = 1, string name = "Ns") => new()
    {
        Name = name,
        TypeCount = types?.Count ?? 1,
        AbstractTypeCount = abstractCount,
        AfferentCoupling = ca,
        EfferentCoupling = ce,
        Types = types ?? []
    };

    private static ProjectMetrics Project(List<NamespaceMetrics>? namespaces = null) => new()
    {
        Name = "TestProject",
        FilePath = "test.csproj",
        Namespaces = namespaces ?? [],
        Interfaces = [],
        Implementations = [],
        OrphanInterfaces = [],
        Dependencies = [],
        PublicApi = new ApiSurface()
    };

    private static QualityReport ReportWith(params NamespaceMetrics[] namespaces)
        => Report(projects: [Project([.. namespaces])]);

    [Fact]
    public void Evaluate_EmptyReport_NoFailures()
    {
        var results = QualityGateEvaluator.Evaluate(Report(), DefaultThresholds());
        results.ShouldAllBe(r => r.Passed);
    }

    [Fact]
    public void Evaluate_CyclomaticComplexityExceeded_Fails()
    {
        var report = ReportWith(Namespace(types: [Type(methods: [Method(cc: 20)])]));
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "CyclomaticComplexity" && !r.Passed);
    }

    [Fact]
    public void Evaluate_CognitiveComplexityExceeded_Fails()
    {
        var report = ReportWith(Namespace(types: [Type(methods: [Method(cog: 25)])]));
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "CognitiveComplexity" && !r.Passed);
    }

    [Fact]
    public void Evaluate_MaintainabilityIndexBelowMin_Fails()
    {
        var report = ReportWith(Namespace(types: [Type(methods: [Method(mi: 40)])]));
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "MaintainabilityIndex" && !r.Passed);
    }

    [Fact]
    public void Evaluate_MaintainabilityIndexNull_NoCrash()
    {
        var report = ReportWith(Namespace(types: [Type(methods: [Method(mi: null)])]));
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldNotContain(r => r.GateName == "MaintainabilityIndex");
    }

    [Fact]
    public void Evaluate_ClassCouplingExceeded_Fails()
    {
        var report = ReportWith(Namespace(types: [Type(coupling: 30)]));
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "ClassCoupling" && !r.Passed);
    }

    [Fact]
    public void Evaluate_LcomExceeded_Fails()
    {
        var report = ReportWith(Namespace(types: [Type(lcom: 5)]));
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "LCOM" && !r.Passed);
    }

    [Fact]
    public void Evaluate_InheritanceDepthExceeded_Fails()
    {
        var report = ReportWith(Namespace(types: [Type(depth: 5)]));
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "InheritanceDepth" && !r.Passed);
    }

    [Fact]
    public void Evaluate_DistanceFromMainSequence_ConcreteLeaf_Fails()
    {
        var report = ReportWith(Namespace(ca: 0, ce: 0, abstractCount: 0));
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "DistanceFromMainSequence" && !r.Passed);
    }

    [Fact]
    public void Evaluate_DistanceFromMainSequence_BalancedNamespace_Passes()
    {
        var report = ReportWith(new NamespaceMetrics
        {
            Name = "Balanced", TypeCount = 2, AbstractTypeCount = 1,
            AfferentCoupling = 5, EfferentCoupling = 5
        });
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldNotContain(r => r.GateName == "DistanceFromMainSequence" && !r.Passed);
    }

    [Fact]
    public void Evaluate_DuplicationExceeded_Fails()
    {
        var report = Report(duplication: new DuplicationReport { DuplicationPercent = 10 });
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "DuplicationPercent" && !r.Passed);
    }

    [Fact]
    public void Evaluate_DuplicationWithinThreshold_Passes()
    {
        var report = Report(duplication: new DuplicationReport { DuplicationPercent = 2 });
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "DuplicationPercent" && r.Passed);
    }

    [Fact]
    public void Evaluate_NoDuplication_NoGate()
    {
        var results = QualityGateEvaluator.Evaluate(Report(), DefaultThresholds());
        results.ShouldNotContain(r => r.GateName == "DuplicationPercent");
    }

    [Fact]
    public void Evaluate_TestQuality_CoverageOnly()
    {
        var report = Report(coverage: new CoverageReport { LineRate = 0.9, BranchRate = 0.85 });
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "TestQualityScore" && r.Passed);
    }

    [Fact]
    public void Evaluate_TestQuality_LowCoverage_Fails()
    {
        var report = Report(coverage: new CoverageReport { LineRate = 0.3, BranchRate = 0.2 });
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "TestQualityScore" && !r.Passed);
    }

    [Fact]
    public void Evaluate_TestQuality_MutationOnly()
    {
        var report = Report(mutation: new MutationReport
        {
            MutationScore = 0.9, TotalMutants = 10, Killed = 9,
            Survived = 1, NoCoverage = 0, Timeout = 0
        });
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldContain(r => r.GateName == "TestQualityScore" && r.Passed);
    }

    [Fact]
    public void Evaluate_TestQuality_BothCoverageAndMutation_Averages()
    {
        var report = Report(
            coverage: new CoverageReport { LineRate = 0.9, BranchRate = 0.6 },
            mutation: new MutationReport
            {
                MutationScore = 1.0, TotalMutants = 10, Killed = 10,
                Survived = 0, NoCoverage = 0, Timeout = 0
            });
        var gate = QualityGateEvaluator.Evaluate(report, DefaultThresholds())
            .First(r => r.GateName == "TestQualityScore");
        gate.Passed.ShouldBeTrue();
        gate.ActualValue.ShouldBe(0.8); // (0.6 + 1.0) / 2
    }

    [Fact]
    public void Evaluate_NoCoverageNoMutation_NoTestQualityGate()
    {
        var results = QualityGateEvaluator.Evaluate(Report(), DefaultThresholds());
        results.ShouldNotContain(r => r.GateName == "TestQualityScore");
    }

    [Fact]
    public void Evaluate_AllMetricsWithinThresholds_AllPass()
    {
        var method = Method(cc: 5, cog: 8, mi: 80);
        var type = Type(methods: [method], coupling: 10, lcom: 1, depth: 1);
        var ns = Namespace(types: [type], ca: 5, ce: 5, abstractCount: 1);
        var report = Report(
            projects: [Project([ns])],
            coverage: new CoverageReport { LineRate = 0.9, BranchRate = 0.85 });
        var results = QualityGateEvaluator.Evaluate(report, DefaultThresholds());
        results.ShouldAllBe(r => r.Passed);
    }
}
