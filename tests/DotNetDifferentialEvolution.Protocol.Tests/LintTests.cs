using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ProtocolChecks;

/// <summary>
/// Lint (AGENTS.md §13, the language-independent half): the protocol linter, run as a process from the tree root in
/// strict mode, exits 0. The reflection facts and the linter then fail in the same test run, so neither half of the
/// checks can be forgotten.
/// </summary>
public sealed class LintTests
{
    /// <summary>The tree passes the protocol linter with no error and no warning.</summary>
    [Fact]
    public async Task TheTreePassesTheProtocolLinterWithNoErrorAndNoWarning()
    {
        var script = Path.Combine(Tree.Root, ProtocolConfig.LinterPath);
        Assert.True(File.Exists(script), $"found nothing: the protocol linter is not at {ProtocolConfig.LinterPath} under the tree root, so nothing was linted " +
                                         "(copy protocol_lint.py there, or set ProtocolConfig.LinterPath)");
        var start = new ProcessStartInfo(ProtocolConfig.PythonExecutable)
        {
            WorkingDirectory = Tree.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-X", "utf8", script, "."])
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var argument in ProtocolConfig.LinterArguments)
        {
            start.ArgumentList.Add(argument);
        }

        if (ProtocolConfig.LinterExcludes.Count > 0)
        {
            start.ArgumentList.Add("--exclude");
            start.ArgumentList.Add(string.Join(',', ProtocolConfig.LinterExcludes));
        }

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception e)
        {
            Assert.Fail($"{ProtocolConfig.PythonExecutable} was not found on the path ({e.Message}); the lint fact needs Python 3.8+");
            return;
        }

        Assert.NotNull(process);
        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(ProtocolConfig.LinterTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                Assert.Fail($"protocol_lint did not finish within {ProtocolConfig.LinterTimeout}");
            }

            var text = await output.ConfigureAwait(true) + await error.ConfigureAwait(true);
            Assert.True(process.ExitCode == 0, $"protocol_lint exited with {process.ExitCode} (strict: a warning fails too):\n{text}");
        }
    }
}
