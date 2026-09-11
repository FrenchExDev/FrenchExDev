namespace FrenchExDev.Net.Builder.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class BuilderAttribute : Attribute
{
    /// <summary>
    /// When set, the generated builder inherits from AbstractBuilder&lt;T, TException&gt;.
    /// When null (default), the generated builder inherits from AbstractBuilder&lt;T&gt;.
    /// </summary>
    public Type? Exception { get; set; }

    /// <summary>
    /// Instantiation strategy for the generated builder's <c>CreateInstance()</c> method.
    /// <list type="bullet">
    /// <item><c>"init"</c> (default) — object initializer: <c>new T { Prop = Prop }</c></item>
    /// <item><c>"ctor"</c> — constructor: <c>new T(Prop1, Prop2)</c></item>
    /// <item><c>"factory:MethodName"</c> — static factory: <c>T.MethodName(Prop1, Prop2)</c></item>
    /// <item><c>"custom"</c> — <c>CreateInstance()</c> is abstract; developer implements in partial class</item>
    /// </list>
    /// </summary>
    public string Instantiation { get; set; } = "init";
}
