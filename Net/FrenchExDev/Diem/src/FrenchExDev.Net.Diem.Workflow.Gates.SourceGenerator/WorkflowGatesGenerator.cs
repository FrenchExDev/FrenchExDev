namespace FrenchExDev.Net.Diem.Workflow.Gates.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    [Generator]
    public sealed class WorkflowGatesGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Placeholder: will generate IGateEvaluator implementations from [Gate]/[RequiresRole]/[RequiresApproval] attributes.
        }
    }
}
