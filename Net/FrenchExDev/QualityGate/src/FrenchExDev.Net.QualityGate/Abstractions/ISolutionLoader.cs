using Microsoft.CodeAnalysis;

namespace FrenchExDev.Net.QualityGate.Abstractions;

public interface ISolutionLoader
{
    Task<Solution> LoadAsync(string solutionPath);
}
