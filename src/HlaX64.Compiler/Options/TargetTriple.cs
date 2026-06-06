namespace HlaX64.Compiler.Options;

public sealed record TargetTriple(string Os, string Arch, string Abi)
{
    public static readonly TargetTriple LinuxX64SysV = new("linux", "x64", "sysv");
    public static readonly TargetTriple WindowsX64MsAbi = new("windows", "x64", "msabi");

    public override string ToString() => $"{Os}-{Arch}-{Abi}";

    public static TargetTriple Parse(string s)
    {
        var parts = s.Split('-');
        if (parts.Length == 3)
            return new TargetTriple(parts[0], parts[1], parts[2]);
        return LinuxX64SysV;
    }
}
