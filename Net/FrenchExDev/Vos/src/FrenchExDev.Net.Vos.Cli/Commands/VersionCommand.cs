using System.CommandLine;
using FrenchExDev.Net.Vos.Lib.Abstractions;

namespace FrenchExDev.Net.Vos.Cli.Commands;

public class VersionCommand : Command
{
    public VersionCommand(IVosProjectService projectService) : base("version", "Show vos version")
    {
        SetAction(_ => Console.WriteLine(projectService.GetVersion()));
    }
}
