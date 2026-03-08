using FrenchExDev.Net.BinaryWrapper.Design;
using Microsoft.Extensions.Logging;

namespace FrenchExDev.Net.Vagrant.Design;

/// <summary>
/// Wraps an <see cref="IHelpParser"/> with detailed logging of input text,
/// parsed commands, and options discovered.
/// </summary>
public sealed class LoggingHelpParser(IHelpParser inner, ILogger logger, string version) : IHelpParser
{
    public CommandNode? Parse(string helpText, string commandName)
    {
        logger.LogInformation("[{Version}] Parsing help for '{Command}' ({Len} chars)",
            version, commandName, helpText.Length);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            var preview = helpText.Length > 300 ? helpText[..300] + "..." : helpText;
            logger.LogTrace("[{Version}] Help text for '{Command}':\n{Text}", version, commandName, preview);
        }

        var node = inner.Parse(helpText, commandName);

        if (node is null)
        {
            logger.LogWarning("[{Version}] Parser returned null for '{Command}'", version, commandName);
            return null;
        }

        logger.LogInformation(
            "[{Version}] Parsed '{Command}': {SubCount} subcommands, {OptCount} options, {ArgCount} arguments",
            version, commandName, node.SubCommands.Count, node.Options.Count, node.Arguments.Count);

        foreach (var sub in node.SubCommands)
            logger.LogDebug("[{Version}]   subcommand: {Sub}", version, sub.Name);

        foreach (var opt in node.Options)
            logger.LogDebug("[{Version}]   option: --{Opt} ({Kind})", version, opt.LongName, opt.ValueKind);

        return node;
    }
}
