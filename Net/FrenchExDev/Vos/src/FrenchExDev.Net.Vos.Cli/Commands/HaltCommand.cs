using System.CommandLine;
using FrenchExDev.Net.Vos.Cli.Exceptions;
using FrenchExDev.Net.Vos.Cli.Output;
using FrenchExDev.Net.Vos.Cli.Symbols;
using FrenchExDev.Net.Vos.Lib.Abstractions;

namespace FrenchExDev.Net.Vos.Cli.Commands;

public class HaltCommand : Command
{
    public HaltCommand(IVosVmService vmService) : base("halt", "Stop VM(s)")
    {
        Arguments.Add(VosCliSymbols.Name);
        Options.Add(VosCliSymbols.Config);
        Options.Add(VosCliSymbols.Force);

        SetAction(async (pr, ct) =>
        {
            var name = pr.GetValue(VosCliSymbols.Name)
                ?? throw new RequiredArgumentMissingException("name");
            var config = pr.GetValue(VosCliSymbols.Config)!;
            var force = pr.GetValue(VosCliSymbols.Force);

            var result = await vmService.HaltAsync(config, name, force, ct);
            if (result.IsFailure) { Console.Error.WriteLine("Failed to halt VM(s)"); return; }
            foreach (var (n, r) in result.Value!) ResultPrinter.Print(n, r);
        });
    }
}
