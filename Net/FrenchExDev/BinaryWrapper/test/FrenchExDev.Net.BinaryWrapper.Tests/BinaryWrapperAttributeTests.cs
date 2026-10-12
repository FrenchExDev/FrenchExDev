using FrenchExDev.Net.BinaryWrapper.Attributes;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Tests;

public sealed class BinaryWrapperAttributeTests
{
    [Fact]
    public void Constructor_SetsBinaryName()
    {
        var attr = new BinaryWrapperAttribute("packer");
        attr.BinaryName.ShouldBe("packer");
    }

    [Fact]
    public void Constructor_DifferentBinaryName()
    {
        var attr = new BinaryWrapperAttribute("podman");
        attr.BinaryName.ShouldBe("podman");
    }

    [Fact]
    public void FlagPrefix_DefaultsToDoubleDash()
    {
        var attr = new BinaryWrapperAttribute("test");
        attr.FlagPrefix.ShouldBe("--");
    }

    [Fact]
    public void FlagValueSeparator_DefaultsToSpace()
    {
        var attr = new BinaryWrapperAttribute("test");
        attr.FlagValueSeparator.ShouldBe(" ");
    }

    [Fact]
    public void UseBoolEqualsFormat_DefaultsToFalse()
    {
        var attr = new BinaryWrapperAttribute("test");
        attr.UseBoolEqualsFormat.ShouldBeFalse();
    }

    [Fact]
    public void FlagPrefix_CanBeSet()
    {
        var attr = new BinaryWrapperAttribute("test") { FlagPrefix = "-" };
        attr.FlagPrefix.ShouldBe("-");
    }

    [Fact]
    public void FlagValueSeparator_CanBeSetToEquals()
    {
        var attr = new BinaryWrapperAttribute("test") { FlagValueSeparator = "=" };
        attr.FlagValueSeparator.ShouldBe("=");
    }

    [Fact]
    public void UseBoolEqualsFormat_CanBeSetToTrue()
    {
        var attr = new BinaryWrapperAttribute("test") { UseBoolEqualsFormat = true };
        attr.UseBoolEqualsFormat.ShouldBeTrue();
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        var attr = new BinaryWrapperAttribute("glab")
        {
            FlagPrefix = "-",
            FlagValueSeparator = "=",
            UseBoolEqualsFormat = true
        };

        attr.BinaryName.ShouldBe("glab");
        attr.FlagPrefix.ShouldBe("-");
        attr.FlagValueSeparator.ShouldBe("=");
        attr.UseBoolEqualsFormat.ShouldBeTrue();
    }

    [Fact]
    public void IsAttribute()
    {
        new BinaryWrapperAttribute("test").ShouldBeAssignableTo<Attribute>();
    }

    [Fact]
    public void AttributeUsage_AllowsClassesOnly()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(BinaryWrapperAttribute), typeof(AttributeUsageAttribute))!;
        usage.ValidOn.ShouldBe(AttributeTargets.Class);
    }

    [Fact]
    public void AttributeUsage_NotInherited()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(BinaryWrapperAttribute), typeof(AttributeUsageAttribute))!;
        usage.Inherited.ShouldBeFalse();
    }

    [Fact]
    public void AttributeUsage_NotAllowMultiple()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(BinaryWrapperAttribute), typeof(AttributeUsageAttribute))!;
        usage.AllowMultiple.ShouldBeFalse();
    }
}
