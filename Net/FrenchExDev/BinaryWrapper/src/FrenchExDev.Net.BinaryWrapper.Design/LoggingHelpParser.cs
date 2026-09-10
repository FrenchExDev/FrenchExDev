using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FrenchExDev.Net.BinaryWrapper.Design;

/// <summary>
/// Wraps an <see cref="IHelpParser"/> with detailed logging of input text,
/// parsed commands, and options discovered.
/// </summary>
public sealed class LoggingHelpParser(IHelpParser inner, ILogger logger, string version) : IHelpParser
{
    public CommandNode? Parse(string helpText, string commandName)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            var preview = helpText.Length > 300 ? helpText[..300] + "..." : helpText;
            logger.LogTrace("[{Version}] Help text for '{Command}':\n{Text}", version, commandName, preview);
        }
        
        var swNodeParsing = Stopwatch.StartNew();
        var node = inner.Parse(helpText, commandName);
        swNodeParsing.Stop();

        if (node is null)
        {
            logger.LogWarning("[{Version}] Parser returned null for '{Command}' in {Miliseconds}ms", version, commandName, swNodeParsing.ElapsedMilliseconds);
            return null;
        }

        logger.LogInformation(
            "[{Version}] Parsed '{Command}': in {Miliseconds}ms, {SubCount} subcommands, {OptCount} options, {ArgCount} arguments",
            version, commandName, swNodeParsing.ElapsedMilliseconds, node.SubCommands.Count, node.Options.Count, node.Arguments.Count);

        return node;
    }
}
