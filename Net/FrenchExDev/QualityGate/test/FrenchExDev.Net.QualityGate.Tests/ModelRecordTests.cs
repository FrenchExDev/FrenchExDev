using FrenchExDev.Net.QualityGate.Model;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class ModelRecordTests
{
    [Fact]
    public void InterfaceInfo_Properties_SetCorrectly()
    {
        var members = new List<string> { "MethodA", "PropertyB" };
        var info = new InterfaceInfo("MyNamespace.IFoo", "IFoo.cs", 10, members);

        info.FullName.ShouldBe("MyNamespace.IFoo");
        info.FilePath.ShouldBe("IFoo.cs");
        info.Line.ShouldBe(10);
        info.Members.ShouldBe(members);
    }

    [Fact]
    public void InterfaceImplementation_Properties_SetCorrectly()
    {
        var impl = new InterfaceImplementation("IFoo", "FooImpl", "FooImpl.cs", 25);

        impl.InterfaceFullName.ShouldBe("IFoo");
        impl.ImplementingTypeFullName.ShouldBe("FooImpl");
        impl.FilePath.ShouldBe("FooImpl.cs");
        impl.Line.ShouldBe(25);
    }

    [Fact]
    public void ProjectDependency_Properties_SetCorrectly()
    {
        var dep = new ProjectDependency("ProjectA", "ProjectB");

        dep.FromProject.ShouldBe("ProjectA");
        dep.ToProject.ShouldBe("ProjectB");
    }

    [Fact]
    public void CloneGroup_Properties_SetCorrectly()
    {
        var instances = new List<CloneInstance>
        {
            new("File1.cs", 1, 10),
            new("File2.cs", 5, 15)
        };
        var group = new CloneGroup(instances, 42);

        group.Instances.Count.ShouldBe(2);
        group.TokenCount.ShouldBe(42);
    }

    [Fact]
    public void CloneInstance_Properties_SetCorrectly()
    {
        var instance = new CloneInstance("MyFile.cs", 10, 20);

        instance.FilePath.ShouldBe("MyFile.cs");
        instance.StartLine.ShouldBe(10);
        instance.EndLine.ShouldBe(20);
    }

    [Fact]
    public void DuplicationReport_Properties_SetCorrectly()
    {
        var report = new DuplicationReport
        {
            DuplicationPercent = 12.5,
            Clones =
            [
                new CloneGroup([new CloneInstance("a.cs", 1, 5)], 10)
            ]
        };

        report.DuplicationPercent.ShouldBe(12.5);
        report.Clones.Count.ShouldBe(1);
    }

    [Fact]
    public void DuplicationReport_DefaultClones_IsEmptyList()
    {
        var report = new DuplicationReport { DuplicationPercent = 0.0 };

        report.Clones.ShouldNotBeNull();
        report.Clones.ShouldBeEmpty();
    }

    [Fact]
    public void CoverageClass_Properties_SetCorrectly()
    {
        var cc = new CoverageClass("MyClass", "MyClass.cs", 0.95, 0.80);

        cc.Name.ShouldBe("MyClass");
        cc.FileName.ShouldBe("MyClass.cs");
        cc.LineRate.ShouldBe(0.95);
        cc.BranchRate.ShouldBe(0.80);
    }

    [Fact]
    public void MutationFileReport_Properties_SetCorrectly()
    {
        var fr = new MutationFileReport("Foo.cs", 0.75, 3, 1, 0);

        fr.Path.ShouldBe("Foo.cs");
        fr.MutationScore.ShouldBe(0.75);
        fr.Killed.ShouldBe(3);
        fr.Survived.ShouldBe(1);
        fr.NoCoverage.ShouldBe(0);
    }

    [Fact]
    public void QualityGateResult_Properties_SetCorrectly()
    {
        var result = new QualityGateResult
        {
            GateName = "Coverage",
            Description = "Line coverage",
            Threshold = 80.0,
            ActualValue = 85.0,
            Passed = true,
            ViolatingElement = null
        };

        result.GateName.ShouldBe("Coverage");
        result.Passed.ShouldBeTrue();
        result.ViolatingElement.ShouldBeNull();
    }

    [Fact]
    public void QualityGateResult_WithViolatingElement()
    {
        var result = new QualityGateResult
        {
            GateName = "Complexity",
            Description = "Max CC",
            Threshold = 10.0,
            ActualValue = 15.0,
            Passed = false,
            ViolatingElement = "MyClass.ComplexMethod"
        };

        result.ViolatingElement.ShouldBe("MyClass.ComplexMethod");
        result.Passed.ShouldBeFalse();
    }
}
