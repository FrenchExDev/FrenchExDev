using System.CommandLine;
using FrenchExDev.Net.Vos.Cli.Output;
using FrenchExDev.Net.Vos.Cli.Symbols;
using FrenchExDev.Net.Vos.Lib.Abstractions;

namespace FrenchExDev.Net.Vos.Cli.Commands;

public class StatusCommand : Command
{
    public StatusCommand(IVosVmService vmService) : base("status", "Show VM status")
    {
        Options.Add(VosCliSymbols.Config);

        SetAction(async (pr, ct) =>
        {
            var config = pr.GetValue(VosCliSymbols.Config)!;
            var result = await vmService.StatusAsync(config, ct);
            if (result.IsFailure) { Console.Error.WriteLine("Failed to get status"); return; }
            foreach (var (n, r) in result.Value!) ResultPrinter.Print(n, r);
        });
    }
}
