namespace FrenchExDev.Net.Requirements;

public readonly struct Email
{
    public string Value { get; }
    public Email(string value) { Value = value; }
    public override string ToString() => Value;
}

public readonly struct UserId
{
    public Guid Value { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public Email Email { get; }
    public UserId(Guid value, string firstName, string lastName, Email email)
    { Value = value; FirstName = firstName; LastName = lastName; Email = email; }
}

public readonly struct RoleId
{
    public string Value { get; }
    public RoleId(string value) { Value = value; }
    public override string ToString() => Value;
}

public readonly struct ResourceId
{
    public string Value { get; }
    public ResourceId(string value) { Value = value; }
    public override string ToString() => Value;
}

public readonly struct TokenId
{
    public Guid Value { get; }
    public TokenId(Guid value) { Value = value; }
}
