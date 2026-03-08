using FrenchExDev.Net.BinaryWrapper.Design;

var commands = new IToolCommand[]
{
    new NewCommand(),
    new ScrapeCommand(),
    new ScrapeAllCommand(),
    new GenerateCommand(),
    new VersionDiffCommand(),
};

if (args.Length == 0)
{
    Console.WriteLine("FrenchExDev Binary Wrapper Design Tool");
    Console.WriteLine();
    Console.WriteLine("Usage: binary-wrapper <command> [args...]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    foreach (var cmd in commands)
        Console.WriteLine($"  {cmd.Name,-16} {cmd.Description}");
    return 0;
}

var commandName = args[0];
var command = commands.FirstOrDefault(c =>
    string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));

if (command is null)
{
    Console.Error.WriteLine($"Unknown command: '{commandName}'");
    Console.Error.WriteLine($"Available commands: {string.Join(", ", commands.Select(c => c.Name))}");
    return 1;
}

return await command.ExecuteAsync(args[1..]);
