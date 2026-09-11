namespace FrenchExDev.Net.Dsl;

/// <summary>
/// The 5-stage DSL processing pipeline.
/// </summary>
public enum DslStage
{
    MetamodelRegistration = 0,
    Validation = 1,
    CoreGeneration = 2,
    CrossCutting = 3,
    Traceability = 4
}
