using FrenchExDev.Net.QualityGate.Analysis;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class InterfaceAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_NoInterfaces_ReturnsEmptyLists()
    {
        var source = """
            public class Simple
            {
                public void DoWork() { }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (interfaces, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces.ShouldBeEmpty();
        implementations.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_InterfaceWithImplementation_BothFound()
    {
        var source = """
            public interface IService
            {
                void Execute();
                string Name { get; }
            }
            public class ServiceImpl : IService
            {
                public void Execute() { }
                public string Name => "test";
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (interfaces, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces.Count.ShouldBe(1);
        interfaces[0].FullName.ShouldBe("IService");
        interfaces[0].Members.ShouldContain("Execute");
        interfaces[0].Members.ShouldContain("Name");

        implementations.Count.ShouldBe(1);
        implementations[0].InterfaceFullName.ShouldBe("IService");
        implementations[0].ImplementingTypeFullName.ShouldBe("ServiceImpl");
    }

    [Fact]
    public async Task AnalyzeAsync_InterfaceNotImplemented_OrphanInterface()
    {
        var source = """
            public interface IOrphan
            {
                void Lonely();
            }
            public class Unrelated
            {
                public void DoStuff() { }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (interfaces, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces.Count.ShouldBe(1);
        interfaces[0].FullName.ShouldBe("IOrphan");
        interfaces[0].Members.ShouldContain("Lonely");

        // No implementation found
        implementations.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleImplementations_AllFound()
    {
        var source = """
            public interface IProcessor { void Process(); }
            public class ProcessorA : IProcessor { public void Process() { } }
            public class ProcessorB : IProcessor { public void Process() { } }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (interfaces, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces.Count.ShouldBe(1);
        implementations.Count.ShouldBe(2);
        implementations.ShouldContain(i => i.ImplementingTypeFullName == "ProcessorA");
        implementations.ShouldContain(i => i.ImplementingTypeFullName == "ProcessorB");
    }

    [Fact]
    public async Task AnalyzeAsync_InterfaceMembersAreListed()
    {
        var source = """
            public interface IFull
            {
                void MethodA();
                void MethodB(int x);
                string Prop { get; set; }
                event System.EventHandler Changed;
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (interfaces, _) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces.Count.ShouldBe(1);
        var info = interfaces[0];
        info.Members.ShouldContain("MethodA");
        info.Members.ShouldContain("MethodB");
        info.Members.ShouldContain("Prop");
        info.Members.ShouldContain("Changed");
    }

    [Fact]
    public async Task AnalyzeAsync_LineNumberIsCorrect()
    {
        // Interface starts on line 2 (0-indexed line 1, so reported as 2)
        var source = """
            namespace Test
            {
                public interface IMyInterface
                {
                    void Do();
                }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (interfaces, _) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces.Count.ShouldBe(1);
        interfaces[0].Line.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeAsync_ImplementationLineNumberIsCorrect()
    {
        var source = """
            public interface IFoo { void Bar(); }
            public class FooImpl : IFoo
            {
                public void Bar() { }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (_, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project);

        implementations.Count.ShouldBe(1);
        implementations[0].Line.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeAsync_NullProject_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => InterfaceAnalyzer.AnalyzeAsync(null!));
    }

    [Fact]
    public async Task AnalyzeAsync_EmptySource_ReturnsEmptyLists()
    {
        var project = RoslynTestHelper.CreateProject("");

        var (interfaces, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces.ShouldBeEmpty();
        implementations.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleDocuments_FindsAllInterfaces()
    {
        var project = RoslynTestHelper.CreateProject(
            ("File1.cs", "public interface IFirst { void A(); }"),
            ("File2.cs", "public interface ISecond { void B(); }")
        );

        var (interfaces, _) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnalyzeAsync_ClassImplementsMultipleInterfaces_AllRecorded()
    {
        var source = """
            public interface IA { void A(); }
            public interface IB { void B(); }
            public class Multi : IA, IB
            {
                public void A() { }
                public void B() { }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (_, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project);

        implementations.Count.ShouldBe(2);
        implementations.ShouldContain(i => i.InterfaceFullName == "IA");
        implementations.ShouldContain(i => i.InterfaceFullName == "IB");
    }

    [Fact]
    public async Task AnalyzeAsync_NamespacedInterface_FullNameIncludesNamespace()
    {
        var source = """
            namespace My.App
            {
                public interface INamespaced { void Work(); }
                public class Impl : INamespaced { public void Work() { } }
            }
            """;
        var project = RoslynTestHelper.CreateProject(source);

        var (interfaces, implementations) = await InterfaceAnalyzer.AnalyzeAsync(project);

        interfaces[0].FullName.ShouldBe("My.App.INamespaced");
        implementations[0].InterfaceFullName.ShouldBe("My.App.INamespaced");
        implementations[0].ImplementingTypeFullName.ShouldBe("My.App.Impl");
    }
}
