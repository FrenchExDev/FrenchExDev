using System.CommandLine;
using FrenchExDev.Net.Vos.Lib.Abstractions;

namespace FrenchExDev.Net.Vos.Cli.Commands;

public class InitCommand : Command
{
    public InitCommand(IVosProjectService projectService) : base("init", "Initialize a Vos project")
    {
        SetAction(async (pr, ct) =>
        {
            var result = await projectService.InitAsync(".", ct);
            if (result.IsFailure) { Console.Error.WriteLine("Failed to initialize project"); return; }
            Console.WriteLine("Vos project initialized.");
        });
    }
}
