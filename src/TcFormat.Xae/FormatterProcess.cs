using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;

namespace TcFormat.Xae;

internal sealed class FormatterProcess
{
    private const string RegistryKeyPath = @"Software\tc_format";
    private const string RegistryValueName = "InstallPath";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task<FormatterProcessResult> FormatAsync(
        string source,
        string backingFilePath,
        CancellationToken cancellationToken)
    {
        var arguments = $"--stdin-filepath {QuoteArgument(backingFilePath)}";
        var result = await RunAsync(arguments, source, cancellationToken);
        return result.Succeeded
            ? FormatterProcessResult.Success(PreserveSectionFinalNewline(source, result.FormattedText))
            : result;
    }

    public async Task<FormatterProcessResult> FormatPathsAsync(
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken)
    {
        if (paths.Count == 0)
        {
            return FormatterProcessResult.Failure("No files or directories were selected.");
        }

        var arguments = string.Join(" ", paths.Select(QuoteArgument));
        return await RunAsync(arguments, null, cancellationToken);
    }

    private static async Task<FormatterProcessResult> RunAsync(
        string arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var executablePath = ResolveExecutablePath();
        if (executablePath is null)
        {
            return FormatterProcessResult.Failure(
                "tc_format.exe was not found. Repair or reinstall tc_format with the CLI component enabled.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return FormatterProcessResult.Failure("Unable to start tc_format.exe.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            IOException? inputException = null;
            if (standardInput is not null)
            {
                try
                {
                    await process.StandardInput.WriteAsync(standardInput);
                }
                catch (IOException exception)
                {
                    inputException = exception;
                }
            }

            process.StandardInput.Close();

            var processTask = Task.Run(() => process.WaitForExit(), cancellationToken);
            var timeoutTask = Task.Delay(Timeout, cancellationToken);
            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            if (completedTask != processTask)
            {
                TryKill(process);
                cancellationToken.ThrowIfCancellationRequested();
                return FormatterProcessResult.Failure("tc_format timed out after 30 seconds.");
            }

            await processTask;
            var output = await outputTask;
            var diagnostics = await errorTask;
            if (process.ExitCode != 0)
            {
                return FormatterProcessResult.Failure(
                    string.IsNullOrWhiteSpace(diagnostics)
                        ? $"tc_format exited with code {process.ExitCode}."
                        : diagnostics.Trim());
            }

            return inputException is null
                ? FormatterProcessResult.Success(output)
                : FormatterProcessResult.Failure(
                    $"tc_format exited while receiving editor text: {inputException.Message}");
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            TryKill(process);
            return FormatterProcessResult.Failure(exception.Message);
        }
    }

    private static string QuoteArgument(string value)
    {
        var quoted = new StringBuilder(value.Length + 2);
        quoted.Append('"');
        var backslashCount = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', (backslashCount * 2) + 1);
                quoted.Append(character);
                backslashCount = 0;
                continue;
            }

            quoted.Append('\\', backslashCount);
            quoted.Append(character);
            backslashCount = 0;
        }

        quoted.Append('\\', backslashCount * 2);
        quoted.Append('"');
        return quoted.ToString();
    }

    internal static string PreserveSectionFinalNewline(string source, string formattedText)
    {
        var sourceLineEnding = DetectLineEnding(source);
        if (sourceLineEnding is not null)
        {
            formattedText = formattedText
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", sourceLineEnding);
        }

        var sourceHasFinalNewline = EndsWithNewline(source);
        var formattedHasFinalNewline = EndsWithNewline(formattedText);
        if (sourceHasFinalNewline == formattedHasFinalNewline)
        {
            return formattedText;
        }

        if (!sourceHasFinalNewline)
        {
            if (formattedText.EndsWith("\r\n", StringComparison.Ordinal))
            {
                return formattedText.Substring(0, formattedText.Length - 2);
            }

            return formattedText.Substring(0, formattedText.Length - 1);
        }

        return formattedText + DetectFinalNewline(source);
    }

    private static bool EndsWithNewline(string value) =>
        value.EndsWith("\r", StringComparison.Ordinal) ||
        value.EndsWith("\n", StringComparison.Ordinal);

    private static string DetectFinalNewline(string value) =>
        value.EndsWith("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : value.Substring(value.Length - 1);

    private static string? DetectLineEnding(string value)
    {
        var lineFeedIndex = value.IndexOf('\n');
        if (lineFeedIndex >= 0)
        {
            return lineFeedIndex > 0 && value[lineFeedIndex - 1] == '\r'
                ? "\r\n"
                : "\n";
        }

        return value.IndexOf('\r') >= 0 ? "\r" : null;
    }

    private static string? ResolveExecutablePath()
    {
        using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                   .OpenSubKey(RegistryKeyPath))
        {
            if (key?.GetValue(RegistryValueName) is string configuredPath && File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
        {
            if (key?.GetValue(RegistryValueName) is string configuredPath && File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        var machineWidePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "tc_format",
            "tc_format.exe");
        if (File.Exists(machineWidePath))
        {
            return machineWidePath;
        }

        var conventionalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "tc_format",
            "tc_format.exe");
        return File.Exists(conventionalPath) ? conventionalPath : null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal sealed class FormatterProcessResult
{
    private FormatterProcessResult(bool succeeded, string formattedText, string error)
    {
        Succeeded = succeeded;
        FormattedText = formattedText;
        Error = error;
    }

    public bool Succeeded { get; }

    public string FormattedText { get; }

    public string Error { get; }

    public static FormatterProcessResult Success(string formattedText) =>
        new FormatterProcessResult(true, formattedText, string.Empty);

    public static FormatterProcessResult Failure(string error) =>
        new FormatterProcessResult(false, string.Empty, error);
}
