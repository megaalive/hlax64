using HlaX64.Cli.Project;

namespace HlaX64.Compiler.Tests;

public sealed class Round2BatchBTests
{
    [Fact]
    public void Restore_ResolvesPathDependency()
    {
        var root = CreateFixture();
        var helpersDir = Path.Combine(root, "helpers");
        Directory.CreateDirectory(helpersDir);
        File.WriteAllText(Path.Combine(helpersDir, "helper.hla64"), """
            program helper_lib;
            procedure HelperVal; @returns("rax");
            begin HelperVal;
                mov(99, rax);
            end HelperVal;
            begin helper_lib;
            end helper_lib;
            """);

        File.WriteAllText(Path.Combine(root, "hla64.toml"), """
            name = "consumer"
            version = "0.1.0"
            main = "main.hla64"

            [dependencies]
            helpers = { path = "helpers" }
            """);

        File.WriteAllText(Path.Combine(root, "main.hla64"), """
            program consumer;
            begin consumer;
                call HelperVal();
            end consumer;
            """);

        var manifest = ProjectManifest.Load(Path.Combine(root, "hla64.toml"));
        Assert.Single(manifest.DependencySpecs);
        Assert.Equal("helpers", manifest.DependencySpecs[0].Name);

        var lockDoc = DependencyResolver.Resolve(manifest, root, allowGit: false);
        var lockPath = Path.Combine(root, "hla64.lock");
        DependencyResolver.SaveLock(lockDoc, lockPath);

        Assert.True(File.Exists(lockPath));
        Assert.Single(lockDoc.Dependencies);
        Assert.True(lockDoc.Dependencies[0].Sources.Any(s => s.EndsWith("helper.hla64")));

        var prev = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            var (sourceFile, sourceText, _, error) =
                HlaX64.Cli.Commands.ProjectBuildHelper.ResolveProjectSource(null, requireLock: true);
            Assert.Null(error);
            Assert.Contains("HelperVal", sourceText);
            Assert.NotNull(sourceFile);
        }
        finally
        {
            Directory.SetCurrentDirectory(prev);
        }
    }

    [Fact]
    public void Build_FailsWhenLockMissingForDeps()
    {
        var root = CreateFixture();
        var helpersDir = Path.Combine(root, "helpers");
        Directory.CreateDirectory(helpersDir);
        File.WriteAllText(Path.Combine(helpersDir, "x.hla64"), "program x;\nbegin x;\nend x;\n");
        File.WriteAllText(Path.Combine(root, "hla64.toml"), """
            name = "app"
            main = "main.hla64"
            [dependencies]
            helpers = { path = "helpers" }
            """);
        File.WriteAllText(Path.Combine(root, "main.hla64"), "program app;\nbegin app;\nend app;\n");

        var prev = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            var (_, _, _, error) = HlaX64.Cli.Commands.ProjectBuildHelper.ResolveProjectSource(null, requireLock: true);
            Assert.Contains("restore", error!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(prev);
        }
    }

    private static string CreateFixture()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hlax64-dep-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
