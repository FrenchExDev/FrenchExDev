namespace FrenchExDev.Net.Requirements;

public abstract class RequirementMetadata
{
    public abstract string Title { get; }
    public abstract RequirementPriority Priority { get; }
    public abstract string Owner { get; }
}

public abstract class Epic : RequirementMetadata { }

public abstract class Feature<TParent> : RequirementMetadata where TParent : Epic { }

public abstract class Feature : RequirementMetadata { }

public abstract class Story<TParent> : RequirementMetadata where TParent : RequirementMetadata { }

public abstract class RequirementTask<TParent> : RequirementMetadata where TParent : RequirementMetadata
{
    public abstract int EstimatedHours { get; }
}

public abstract class Bug : RequirementMetadata
{
    public abstract BugSeverity Severity { get; }
}
