namespace FrenchExDev.Net.Diem.Workflow.StateMachine.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    [Generator]
    public sealed class WorkflowStateMachineGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Placeholder: will generate WorkflowEngine registrations from [Workflow]/[Stage]/[Transition] attributes.
        }
    }
}
