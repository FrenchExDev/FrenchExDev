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
        public double SetupMilliseconds, StartMilliseconds, RunMilliseconds,
                      StdoutDrainMilliseconds, StderrDrainMilliseconds, ElapsedMilliseconds;
    }
    // Callbacks run on the PowerShell caller's thread, never on these readers.
    public sealed class StreamRecord {
        public string Stream, Text;
        public long Length;
        public bool Truncated;
    }
    public sealed class StreamingProcess : IDisposable {
        private readonly System.Collections.Concurrent.BlockingCollection<StreamRecord> queue = new(200);
        private readonly CancellationTokenSource cancellation = new();
        private readonly Process process = new();
        private readonly Task completion;
        private readonly int recordLimit;
        private readonly char delimiter;
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        public Capture Result { get; } = new();
        public StreamingProcess(string executable, string[] arguments, string directory,
                                int seconds, int maxRecordCharacters, char recordDelimiter)
            : this(executable, arguments, directory, seconds, maxRecordCharacters, recordDelimiter, null) { }
        public StreamingProcess(string executable, string[] arguments, string directory,
                                int seconds, int maxRecordCharacters, char recordDelimiter,
                                IDictionary<string, string> environment) {
            var setup = Stopwatch.StartNew();
            recordLimit = maxRecordCharacters; delimiter = recordDelimiter;
            process.StartInfo = new ProcessStartInfo(executable) {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            try { BoundedProcess.ApplyEnvironment(process.StartInfo, environment); }
            catch { process.Dispose(); cancellation.Dispose(); queue.Dispose(); throw; }
            Result.SetupMilliseconds = setup.Elapsed.TotalMilliseconds;
            var start = Stopwatch.StartNew();
            try { process.Start(); process.StandardInput.Close(); }
            catch (Exception exception) {
                Result.StartError = exception.GetType().Name;
                queue.CompleteAdding(); completion = Task.CompletedTask; return;
            }
            finally {
                Result.StartMilliseconds = start.Elapsed.TotalMilliseconds;
                Result.ElapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds;
            }
            completion = Task.Run(() => RunAsync(seconds));
        }
        private void Publish(string stream, StringBuilder text, long length) {
            bool truncated = length > recordLimit;
            if (truncated) Interlocked.Increment(ref Result.OmittedRecords);
            var record = new StreamRecord { Stream = stream, Text = truncated ? "" : text.ToString(),
                                            Length = length, Truncated = truncated };
            // Wake when the consumer frees capacity instead of polling a timer per full queue.
            queue.Add(record, cancellation.Token);
        }
        private async Task ReadAsync(StreamReader reader, string stream) {
            var timer = Stopwatch.StartNew();
            try {
            var buffer = new char[4096]; var text = new StringBuilder(); var diagnostic = new StringBuilder(); long length = 0; int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation.Token).ConfigureAwait(false)) > 0) {
                if (stream == "stdout") Result.StdoutCharacters += count; else Result.StderrCharacters += count;
                if (stream == "stderr" && diagnostic.Length < 4096) diagnostic.Append(buffer, 0, Math.Min(count, 4096 - diagnostic.Length));
                for (int index = 0; index < count; index++) {
                    char value = buffer[index];
                    // stderr is always line framed, even when stdout uses NUL records.
                    if (value == (stream == "stderr" ? '\n' : delimiter)) {
                        Publish(stream, text, length);
                        text.Clear(); length = 0;
                    } else { length++; if (text.Length < recordLimit) text.Append(value); }
                }
            }
            if (length > 0) Publish(stream, text, length);
            if (stream == "stderr") Result.Stderr = diagnostic.ToString();
            } finally {
                if (stream == "stdout") Result.StdoutDrainMilliseconds = timer.Elapsed.TotalMilliseconds;
                else Result.StderrDrainMilliseconds = timer.Elapsed.TotalMilliseconds;
            }
        }
        private async Task RunAsync(int seconds) {
            var timer = Stopwatch.StartNew();
            try {
                // Each reader may wait for queue capacity. Start both independently so neither
                // can block setup of the other reader or the process deadline.
                var readers = Task.WhenAll(Task.Run(() => ReadAsync(process.StandardOutput, "stdout")),
                                           Task.Run(() => ReadAsync(process.StandardError, "stderr")));
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
                Result.RunMilliseconds = timer.Elapsed.TotalMilliseconds;
                Result.ElapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds;
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
        internal static void ApplyEnvironment(ProcessStartInfo info, IDictionary<string, string> environment) {
            info.Environment["NO_COLOR"] = "1";
            info.Environment["TERM"] = "dumb";
            info.Environment["GIT_TERMINAL_PROMPT"] = "0";
            if (environment == null) return;
            foreach (var entry in environment) {
                if (string.IsNullOrEmpty(entry.Key) || entry.Key.IndexOfAny(new[] { '=', '\0' }) >= 0 ||
                    (entry.Value != null && entry.Value.IndexOf('\0') >= 0))
                    throw new ArgumentException("Invalid process environment entry.", nameof(environment));
                if (entry.Value == null) info.Environment.Remove(entry.Key);
                else info.Environment[entry.Key] = entry.Value;
            }
        }
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
            public double Milliseconds;
            public async Task Read(StreamReader reader, int limit, CancellationToken token) {
                var timer = Stopwatch.StartNew();
                try {
                var buffer = new char[4096];
                int n;
                while ((n = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0) {
                    Count += n;
                    int keep = Math.Min(n, Math.Max(0, limit - Text.Length));
                    if (keep != 0) Text.Append(buffer, 0, keep);
                }
                } finally { Milliseconds = timer.Elapsed.TotalMilliseconds; }
            }
        }
        public static Capture Run(string executable, string[] arguments, string directory,
                                  int timeoutSeconds, int captureLimit) {
            return Run(executable, arguments, directory, timeoutSeconds, captureLimit, null);
        }
        public static Capture Run(string executable, string[] arguments, string directory,
                                  int timeoutSeconds, int captureLimit, IDictionary<string, string> environment) {
            return RunAsync(executable, arguments, directory, timeoutSeconds, captureLimit, environment).GetAwaiter().GetResult();
        }
        private static async Task<Capture> RunAsync(string executable, string[] arguments,
                                                   string directory, int seconds, int limit, IDictionary<string, string> environment) {
            var elapsed = Stopwatch.StartNew();
            var setup = Stopwatch.StartNew();
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
            ApplyEnvironment(process.StartInfo, environment);
            result.SetupMilliseconds = setup.Elapsed.TotalMilliseconds;
            var start = Stopwatch.StartNew();
            try { process.Start(); process.StandardInput.Close(); }
            catch (Exception ex) { result.StartError = ex.GetType().Name; return result; }
            finally {
                result.StartMilliseconds = start.Elapsed.TotalMilliseconds;
                result.ElapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds;
            }
            var run = Stopwatch.StartNew();
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
            result.StdoutDrainMilliseconds = stdout.Milliseconds;
            result.StderrDrainMilliseconds = stderr.Milliseconds;
            result.RunMilliseconds = run.Elapsed.TotalMilliseconds;
            result.ElapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds;
            return result;
        }
    }
}
