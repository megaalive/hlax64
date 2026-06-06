using System.ComponentModel;
using HlaX64.Cli.Formatting;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class FormatCommand : Command<FormatCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to a .hla64 file or directory")]
        [CommandArgument(0, "<path>")]
        public string Path { get; set; } = string.Empty;

        [Description("Check formatting without writing (exit 1 if changes needed)")]
        [CommandOption("--check")]
        public bool Check { get; set; }

        [Description("Output result as JSON")]
        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Path) && !Directory.Exists(settings.Path))
        {
            Report(settings, success: false, error: $"Path '{settings.Path}' not found.", changed: 0, checkedFiles: 0);
            return 1;
        }

        var files = CollectFiles(settings.Path);
        int changed = 0;

        foreach (var file in files)
        {
            if (cancellation.IsCancellationRequested) break;

            var original = File.ReadAllText(file);
            var formatted = SourceFormatter.Format(original);
            if (original != formatted)
            {
                changed++;
                if (!settings.Check)
                    File.WriteAllText(file, formatted);
            }
        }

        var success = !settings.Check || changed == 0;
        Report(settings, success, changed, files.Count);
        return success ? 0 : 1;
    }

    private static List<string> CollectFiles(string path)
    {
        if (File.Exists(path))
            return [Path.GetFullPath(path)];

        return Directory.GetFiles(path, "*.hla64", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Report(Settings settings, bool success, int changed, int checkedFiles, string? error = null)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success,
                version = Compilation.GetVersion(),
                checkOnly = settings.Check,
                filesChecked = checkedFiles,
                filesChanged = changed,
                error
            });
            return;
        }

        if (error != null)
        {
            Console.Error.WriteLine($"Error: {error}");
            return;
        }

        if (settings.Check)
        {
            if (changed == 0)
                Console.WriteLine($"All {checkedFiles} file(s) formatted correctly.");
            else
                Console.Error.WriteLine($"{changed} of {checkedFiles} file(s) need formatting.");
        }
        else if (changed == 0)
        {
            Console.WriteLine($"No changes ({checkedFiles} file(s)).");
        }
        else
        {
            Console.WriteLine($"Formatted {changed} of {checkedFiles} file(s).");
        }
    }
}
