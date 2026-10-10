using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FrenchExDev.OutputPumps {
    public sealed class Capture {
        public string Stdout = "", Stderr = "", StartError = "";
        public int? ExitCode;
        public bool TimedOut, Truncated;
        public long StdoutCharacters, StderrCharacters, OmittedRecords;
    }
    // Callbacks run on the PowerShell caller's thread, never on these readers.
    public sealed class StreamRecord {
        public string Stream, Text;
        public long Length;
        public bool Truncated;
    }
    public sealed class StreamingProcess : IDisposable {
        private readonly System.Collections.Concurrent.BlockingCollection<StreamRecord> queue = new(16);
        private readonly CancellationTokenSource cancellation = new();
        private readonly Process process = new();
        private readonly Task completion;
        private readonly int recordLimit;
        private readonly char delimiter;
        public Capture Result { get; } = new();
        public StreamingProcess(string executable, string[] arguments, string directory,
                                int seconds, int maxRecordCharacters, char recordDelimiter) {
            recordLimit = maxRecordCharacters; delimiter = recordDelimiter;
            process.StartInfo = new ProcessStartInfo(executable) {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            process.StartInfo.Environment["NO_COLOR"] = "1";
            process.StartInfo.Environment["TERM"] = "dumb";
            process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            try { process.Start(); process.StandardInput.Close(); }
            catch (Exception exception) {
                Result.StartError = exception.GetType().Name;
                queue.CompleteAdding(); completion = Task.CompletedTask; return;
            }
            completion = Task.Run(() => RunAsync(seconds));
        }
        private async Task PublishAsync(string stream, StringBuilder text, long length) {
            bool truncated = length > recordLimit;
            if (truncated) Interlocked.Increment(ref Result.OmittedRecords);
            var record = new StreamRecord { Stream = stream, Text = truncated ? "" : text.ToString(),
                                            Length = length, Truncated = truncated };
            while (!queue.TryAdd(record)) await Task.Delay(1, cancellation.Token).ConfigureAwait(false);
        }
        private async Task ReadAsync(StreamReader reader, string stream) {
            var buffer = new char[4096]; var text = new StringBuilder(); var diagnostic = new StringBuilder(); long length = 0; int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation.Token).ConfigureAwait(false)) > 0) {
                if (stream == "stdout") Result.StdoutCharacters += count; else Result.StderrCharacters += count;
                if (stream == "stderr" && diagnostic.Length < 4096) diagnostic.Append(buffer, 0, Math.Min(count, 4096 - diagnostic.Length));
                for (int index = 0; index < count; index++) {
                    char value = buffer[index];
                    // stderr is always line framed, even when stdout uses NUL records.
                    if (value == (stream == "stderr" ? '\n' : delimiter)) {
                        await PublishAsync(stream, text, length).ConfigureAwait(false);
                        text.Clear(); length = 0;
                    } else { length++; if (text.Length < recordLimit) text.Append(value); }
                }
            }
            if (length > 0) await PublishAsync(stream, text, length).ConfigureAwait(false);
            if (stream == "stderr") Result.Stderr = diagnostic.ToString();
        }
        private async Task RunAsync(int seconds) {
            try {
                var readers = Task.WhenAll(ReadAsync(process.StandardOutput, "stdout"), ReadAsync(process.StandardError, "stderr"));
                var finished = Task.WhenAll(process.WaitForExitAsync(cancellation.Token), readers);
                if (await Task.WhenAny(finished, Task.Delay(TimeSpan.FromSeconds(seconds))).ConfigureAwait(false) != finished) {
                    Result.TimedOut = true;
                    try { process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                    if (await Task.WhenAny(finished, Task.Delay(2000)).ConfigureAwait(false) != finished) cancellation.Cancel();
                }
                await finished.ConfigureAwait(false);
            } catch (OperationCanceledException) { }
              catch (Exception exception) { Result.StartError = exception.GetType().Name; }
            finally {
                try { if (process.HasExited) Result.ExitCode = process.ExitCode; } catch (InvalidOperationException) { }
                queue.CompleteAdding();
            }
        }
        public StreamRecord Next() {
            while (!queue.IsCompleted) if (queue.TryTake(out var record, 100)) return record;
            completion.GetAwaiter().GetResult();
            return null;
        }
        public void Dispose() {
            // Even if a callback failed, finish draining; preview limits never kill the command.
            while (Next() != null) { }
            process.Dispose(); cancellation.Dispose(); queue.Dispose();
        }
    }
    public static class BoundedProcess {
        public sealed class LineCursor : IDisposable {
            private readonly IEnumerator<Line> enumerator;
            public LineCursor(string path) { enumerator = ReadLines(path).GetEnumerator(); }
            public Line Current => enumerator.Current;
            public bool MoveNext() => enumerator.MoveNext();
            public void Dispose() => enumerator.Dispose();
        }
        public static LineCursor OpenLines(string path) => new LineCursor(path);
        public sealed class Line {
            public long Number, Length;
            public string Text = "";
        }
        public static IEnumerable<Line> ReadLines(string path) {
            using var reader = new StreamReader(path, Encoding.UTF8, true);
            var buffer = new char[4096]; var text = new StringBuilder();
            long number = 1, length = 0; int n; bool privateKey = false;
            while ((n = reader.Read(buffer, 0, buffer.Length)) > 0) {
                for (int i = 0; i < n; i++) {
                    if (buffer[i] == '\n') {
                        string value = text.ToString().TrimEnd('\r');
                        if (value.Contains("-----BEGIN ") && value.Contains("PRIVATE KEY")) privateKey = true;
                        yield return new Line { Number = number++, Length = length, Text = privateKey ? "[REDACTED: private key]" : value };
                        if (value.Contains("-----END ") && value.Contains("PRIVATE KEY")) privateKey = false;
                        text.Clear(); length = 0;
                    } else { length++; if (text.Length < 512) text.Append(buffer[i]); }
                }
            }
            if (length > 0) {
                string value = text.ToString().TrimEnd('\r');
                if (value.Contains("-----BEGIN ") && value.Contains("PRIVATE KEY")) privateKey = true;
                yield return new Line { Number = number, Length = length, Text = privateKey ? "[REDACTED: private key]" : value };
            }
        }
        private sealed class Drain {
            public readonly StringBuilder Text = new StringBuilder();
            public long Count;
            public async Task Read(StreamReader reader, int limit, CancellationToken token) {
                var buffer = new char[4096];
                int n;
                while ((n = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0) {
                    Count += n;
                    int keep = Math.Min(n, Math.Max(0, limit - Text.Length));
                    if (keep != 0) Text.Append(buffer, 0, keep);
                }
            }
        }
        public static Capture Run(string executable, string[] arguments, string directory,
                                  int timeoutSeconds, int captureLimit) {
            return RunAsync(executable, arguments, directory, timeoutSeconds, captureLimit).GetAwaiter().GetResult();
        }
        private static async Task<Capture> RunAsync(string executable, string[] arguments,
                                                   string directory, int seconds, int limit) {
            var result = new Capture();
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(executable) {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };
            foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);
            process.StartInfo.Environment["NO_COLOR"] = "1";
            process.StartInfo.Environment["TERM"] = "dumb";
            process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            try { process.Start(); process.StandardInput.Close(); }
            catch (Exception ex) { result.StartError = ex.GetType().Name; return result; }
            using var cancellation = new CancellationTokenSource();
            var stdout = new Drain(); var stderr = new Drain();
            var outTask = stdout.Read(process.StandardOutput, limit, cancellation.Token);
            var errTask = stderr.Read(process.StandardError, Math.Min(limit, 262144), cancellation.Token);
            var completion = Task.WhenAll(process.WaitForExitAsync(cancellation.Token), outTask, errTask);
            if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(seconds))).ConfigureAwait(false) != completion) {
                result.TimedOut = true;
                try { process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                // Descendants may retain a pipe even after their parent exits. Never wait indefinitely.
                if (await Task.WhenAny(completion, Task.Delay(2000)).ConfigureAwait(false) != completion)
                    cancellation.Cancel();
            }
            try { await completion.ConfigureAwait(false); } catch (OperationCanceledException) { }
            catch (IOException) { result.Truncated = true; }
            if (process.HasExited) result.ExitCode = process.ExitCode;
            result.Stdout = stdout.Text.ToString(); result.Stderr = stderr.Text.ToString();
            result.StdoutCharacters = stdout.Count; result.StderrCharacters = stderr.Count;
            result.Truncated |= stdout.Count > result.Stdout.Length || stderr.Count > result.Stderr.Length;
            return result;
        }
    }
}
