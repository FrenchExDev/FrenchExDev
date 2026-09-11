using System.CommandLine;
using FrenchExDev.Net.Vos.Cli.Exceptions;
using FrenchExDev.Net.Vos.Cli.Output;
using FrenchExDev.Net.Vos.Cli.Symbols;
using FrenchExDev.Net.Vos.Lib.Abstractions;

namespace FrenchExDev.Net.Vos.Cli.Commands;

public class UpCommand : Command
{
    public UpCommand(IVosVmService vmService) : base("up", "Start VM(s)")
    {
        Arguments.Add(VosCliSymbols.Name);
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var name = pr.GetValue(VosCliSymbols.Name)
                ?? throw new RequiredArgumentMissingException("name");
            var config = pr.GetValue(VosCliSymbols.Config)!;

            var result = await vmService.UpAsync(config, name, ct: ct);
            if (result.IsFailure) { Console.Error.WriteLine("Failed to start VM(s)"); return; }
            foreach (var (n, r) in result.Value!) ResultPrinter.Print(n, r);
        });
    }
}
