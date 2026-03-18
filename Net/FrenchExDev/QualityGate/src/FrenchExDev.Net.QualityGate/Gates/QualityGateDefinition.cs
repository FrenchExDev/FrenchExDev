namespace FrenchExDev.Net.QualityGate.Gates;

public record QualityGateDefinition(
    string Name,
    string Description,
    double Threshold,
    Func<double, double, bool> Comparator);
