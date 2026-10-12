namespace FrenchExDev.Net.Requirements.Testing;

public abstract class SampleEpic : Epic
{
    public override string Title => "Sample Epic";
    public override RequirementPriority Priority => RequirementPriority.High;
    public override string Owner => "test-team";
}

public abstract class SampleFeature : Feature<SampleEpic>
{
    public override string Title => "Sample Feature";
    public override RequirementPriority Priority => RequirementPriority.Medium;
    public override string Owner => "test-team";

    public abstract AcceptanceCriterionResult ThingWorks(UserId user);
    public abstract AcceptanceCriterionResult ThingHandlesErrors(UserId user, string badInput);
}

public abstract class SampleStory : Story<SampleFeature>
{
    public override string Title => "Sample Story";
    public override RequirementPriority Priority => RequirementPriority.Medium;
    public override string Owner => "test-team";

    public abstract AcceptanceCriterionResult SubTaskCompletes(UserId user);
}
