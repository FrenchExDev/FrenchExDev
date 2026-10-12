using FrenchExDev.Net.QualityGate.Config;
using FrenchExDev.Net.QualityGate.Model;

namespace FrenchExDev.Net.QualityGate.Gates;

public static class QualityGateEvaluator
{
    public static List<QualityGateResult> Evaluate(QualityReport report, GateThresholds thresholds)
    {
        var results = new List<QualityGateResult>();

        foreach (var project in report.Projects)
        {
            foreach (var ns in project.Namespaces)
            {
                EvaluateNamespace(ns, thresholds, results);

                foreach (var type in ns.Types)
                {
                    EvaluateType(type, thresholds, results);

                    foreach (var method in type.Methods)
                        EvaluateMethod(method, thresholds, results);
                }
            }
        }

        EvaluateDuplication(report, thresholds, results);
        EvaluateTestQuality(report, thresholds, results);

        return results;
    }

    private static void EvaluateNamespace(NamespaceMetrics ns, GateThresholds thresholds, List<QualityGateResult> results)
    {
        if (ns.DistanceFromMainSequence > thresholds.MaxDistanceFromMainSequence)
        {
            results.Add(Fail("DistanceFromMainSequence",
                $"Namespace '{ns.Name}' exceeds max distance from main sequence",
                thresholds.MaxDistanceFromMainSequence, ns.DistanceFromMainSequence, ns.Name));
        }
    }

    private static void EvaluateType(TypeMetrics type, GateThresholds thresholds, List<QualityGateResult> results)
    {
        if (type.EfferentCoupling > thresholds.MaxClassCoupling)
        {
            results.Add(Fail("ClassCoupling",
                $"Type '{type.FullName}' exceeds max class coupling",
                thresholds.MaxClassCoupling, type.EfferentCoupling, type.FullName));
        }

        if (type.Lcom4 > thresholds.MaxLcom)
        {
            results.Add(Fail("LCOM",
                $"Type '{type.FullName}' exceeds max LCOM",
                thresholds.MaxLcom, type.Lcom4, type.FullName));
        }

        if (type.InheritanceDepth > thresholds.MaxInheritanceDepth)
        {
            results.Add(Fail("InheritanceDepth",
                $"Type '{type.FullName}' exceeds max inheritance depth",
                thresholds.MaxInheritanceDepth, type.InheritanceDepth, type.FullName));
        }
    }

    private static void EvaluateMethod(MethodMetrics method, GateThresholds thresholds, List<QualityGateResult> results)
    {
        if (method.CyclomaticComplexity > thresholds.MaxCyclomaticComplexity)
        {
            results.Add(Fail("CyclomaticComplexity",
                $"Method '{method.FullName}' exceeds max cyclomatic complexity",
                thresholds.MaxCyclomaticComplexity, method.CyclomaticComplexity, method.FullName));
        }

        if (method.CognitiveComplexity > thresholds.MaxCognitiveComplexity)
        {
            results.Add(Fail("CognitiveComplexity",
                $"Method '{method.FullName}' exceeds max cognitive complexity",
                thresholds.MaxCognitiveComplexity, method.CognitiveComplexity, method.FullName));
        }

        if (method.MaintainabilityIndex.HasValue
            && method.MaintainabilityIndex.Value < thresholds.MinMaintainabilityIndex)
        {
            results.Add(Fail("MaintainabilityIndex",
                $"Method '{method.FullName}' is below min maintainability index",
                thresholds.MinMaintainabilityIndex, method.MaintainabilityIndex.Value, method.FullName));
        }
    }

    private static void EvaluateDuplication(QualityReport report, GateThresholds thresholds, List<QualityGateResult> results)
    {
        if (report.Duplication is null)
            return;

        bool passed = report.Duplication.DuplicationPercent <= thresholds.MaxDuplicationPercent;
        results.Add(new QualityGateResult
        {
            GateName = "DuplicationPercent",
            Description = passed
                ? "Code duplication is within threshold"
                : $"Code duplication {report.Duplication.DuplicationPercent:F1}% exceeds max {thresholds.MaxDuplicationPercent}%",
            Threshold = thresholds.MaxDuplicationPercent,
            ActualValue = report.Duplication.DuplicationPercent,
            Passed = passed
        });
    }

    private static void EvaluateTestQuality(QualityReport report, GateThresholds thresholds, List<QualityGateResult> results)
    {
        double? testQualityScore = ComputeTestQualityScore(report);
        if (!testQualityScore.HasValue)
            return;

        bool passed = testQualityScore.Value >= thresholds.MinTestQualityScore;
        results.Add(new QualityGateResult
        {
            GateName = "TestQualityScore",
            Description = passed
                ? "Test quality score meets threshold"
                : $"Test quality score {testQualityScore.Value:F2} is below min {thresholds.MinTestQualityScore:F2}",
            Threshold = thresholds.MinTestQualityScore,
            ActualValue = testQualityScore.Value,
            Passed = passed
        });
    }

    private static QualityGateResult Fail(string gate, string description, double threshold, double actual, string element)
    {
        return new QualityGateResult
        {
            GateName = gate,
            Description = description,
            Threshold = threshold,
            ActualValue = actual,
            Passed = false,
            ViolatingElement = element
        };
    }

    private static double? ComputeTestQualityScore(QualityReport report)
    {
        double? coverage = report.Coverage?.BranchRate;
        double? mutation = report.Mutation?.MutationScore;

        return (coverage, mutation) switch
        {
            (not null, not null) => (coverage.Value + mutation.Value) / 2.0,
            (not null, null) => coverage.Value,
            (null, not null) => mutation.Value,
            _ => null
        };
    }
}
