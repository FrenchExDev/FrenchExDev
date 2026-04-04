using FrenchExDev.Net.Vos;

namespace FrenchExDev.Net.Vos.Cli.Output;

public static class ResultPrinter
{
    public static void Print(string name, VosActionResult result)
    {
        if (result.Success)
            Console.WriteLine($"{name}: {result.Output}");
        else
            Console.Error.WriteLine($"{name}: ERROR — {result.Error ?? result.Output}");
    }
}
