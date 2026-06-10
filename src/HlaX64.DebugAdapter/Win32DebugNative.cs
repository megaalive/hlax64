using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace HlaX64.DebugAdapter;

internal static partial class Win32DebugNative
{
    internal const uint DebugOnlyThisProcess = 0x00000001;
    internal const uint CreateNoWindow = 0x08000000;
    internal const uint CreateSuspended = 0x00000004;
    internal const uint ExceptionBreakpoint = 0x80000003;
    internal const uint ExceptionSingleStep = 0x80000004;
    internal const uint DbgContinue = 0x00010002;
    internal const uint ContextAmd64 = 0x00100000;
    internal const uint ContextControl = 0x00100001;
    internal const uint ContextInteger = 0x00100002;
    internal const uint ContextFull = ContextAmd64 | ContextControl | ContextInteger;
    internal const uint ThreadGetContext = 0x0008;
    internal const uint ThreadSetContext = 0x0010;
    internal const uint ThreadSuspendResume = 0x0002;
    internal const uint ThreadQueryInformation = 0x0040;
    internal const uint ThreadAccess = ThreadGetContext | ThreadSetContext | ThreadSuspendResume | ThreadQueryInformation;
    internal const byte Int3Opcode = 0xCC;
    internal const uint EflagTrap = 0x100;

    internal enum DebugEventCode : uint
    {
        Exception = 1,
        CreateThread = 2,
        CreateProcess = 3,
        ExitThread = 4,
        ExitProcess = 5,
        LoadDll = 6,
        UnloadDll = 7,
        OutputDebugString = 8,
        RipEvent = 9
    }

    internal enum DebugCommand
    {
        None = 0,
        Continue,
        StepOver,
        StepInto,
        StepOut,
        Kill
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct StartupInfo
    {
        public int cb;
        public nint lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInformation
    {
        public nint hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ExceptionRecord
    {
        public uint ExceptionCode;
        public uint ExceptionFlags;
        public nint ExceptionRecordPtr;
        public nint ExceptionAddress;
        public uint NumberParameters;
        public ulong ExceptionInformation0;
        public ulong ExceptionInformation1;
        public ulong ExceptionInformation2;
        public ulong ExceptionInformation3;
        public ulong ExceptionInformation4;
        public ulong ExceptionInformation5;
        public ulong ExceptionInformation6;
        public ulong ExceptionInformation7;
        public ulong ExceptionInformation8;
        public ulong ExceptionInformation9;
        public ulong ExceptionInformation10;
        public ulong ExceptionInformation11;
        public ulong ExceptionInformation12;
        public ulong ExceptionInformation13;
        public ulong ExceptionInformation14;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ExceptionDebugInfo
    {
        public ExceptionRecord ExceptionRecord;
        public uint dwFirstChance;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CreateProcessDebugInfo
    {
        public nint hFile;
        public nint hProcess;
        public nint hThread;
        public nint lpBaseOfImage;
        public uint dwDebugInfoFileOffset;
        public uint nDebugInfoSize;
        public nint lpThreadLocalBase;
        public nint lpStartAddress;
        public nint lpImageName;
        public uint fUnicode;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ExitProcessDebugInfo
    {
        public uint dwExitCode;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DebugEventUnion
    {
        [FieldOffset(0)] public ExceptionDebugInfo Exception;
        [FieldOffset(0)] public CreateProcessDebugInfo CreateProcessInfo;
        [FieldOffset(0)] public ExitProcessDebugInfo ExitProcess;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DebugEvent
    {
        public DebugEventCode dwDebugEventCode;
        public uint dwProcessId;
        public uint dwThreadId;
        public DebugEventUnion u;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 0x4D0)]
    internal struct Context64
    {
        public ulong P1Home, P2Home, P3Home, P4Home, P5Home, P6Home;
        public uint ContextFlags;
        public uint MxCsr;
        public ushort SegCs, SegDs, SegEs, SegFs, SegGs, SegSs;
        public uint EFlags;
        public ulong Dr0, Dr1, Dr2, Dr3, Dr6, Dr7;
        public ulong Rax, Rcx, Rdx, Rbx, Rsp, Rbp, Rsi, Rdi;
        public ulong R8, R9, R10, R11, R12, R13, R14, R15;
        public ulong Rip;
    }

    internal sealed class SoftwareBreakpoint
    {
        public required ulong Address { get; init; }
        public byte OriginalOpcode { get; set; }
        public bool IsPlanted { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsEphemeral { get; set; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool CreateProcess(
        string? lpApplicationName,
        string lpCommandLine,
        nint lpProcessAttributes,
        nint lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        nint lpEnvironment,
        string? lpCurrentDirectory,
        ref StartupInfo lpStartupInfo,
        out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WaitForDebugEvent(out DebugEvent lpDebugEvent, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, uint dwContinueStatus);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool DebugActiveProcessStop(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint SuspendThread(nint hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint ResumeThread(nint hThread);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetThreadContext")]
    private static extern bool GetThreadContextBuffer(nint hThread, nint lpContext);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetThreadContext")]
    private static extern bool SetThreadContextBuffer(nint hThread, nint lpContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetThreadContext(nint hThread, ref Context64 lpContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetThreadContext(nint hThread, ref Context64 lpContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadProcessMemory(
        nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteProcessMemory(
        nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool FlushInstructionCache(nint hProcess, nint lpBaseAddress, int dwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool TerminateProcess(nint hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool VirtualProtectEx(
        nint hProcess,
        nint lpAddress,
        nuint dwSize,
        uint flNewProtect,
        out uint lpflOldProtect);

    internal const uint PageExecuteReadwrite = 0x40;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(nint hObject);

    internal static bool TryReadMemory(nint processHandle, ulong address, byte[] buffer)
        => ReadProcessMemory(processHandle, (nint)address, buffer, buffer.Length, out var read) && read == buffer.Length;

    internal static bool TryWriteMemory(nint processHandle, ulong address, ReadOnlySpan<byte> data)
    {
        var addr = (nint)address;
        VirtualProtectEx(processHandle, addr, (nuint)data.Length, PageExecuteReadwrite, out _);
        var bytes = data.ToArray();
        return WriteProcessMemory(processHandle, addr, bytes, bytes.Length, out var written)
               && written == bytes.Length
               && FlushInstructionCache(processHandle, addr, bytes.Length);
    }

    internal static bool TryGetThreadContextDirect(nint threadHandle, ref Context64 context)
    {
        const int contextSize = 0x4D0;
        var memory = Marshal.AllocHGlobal(contextSize);
        try
        {
            for (var i = 0; i < contextSize; i++)
                Marshal.WriteByte(memory, i, 0);
            Marshal.WriteInt32(memory, 0x30, (int)ContextFull);
            if (!GetThreadContextBuffer(threadHandle, memory))
                return false;

            context = new Context64
            {
                ContextFlags = (uint)Marshal.ReadInt32(memory, 0x30),
                EFlags = (uint)Marshal.ReadInt32(memory, 0x44),
                Rax = (ulong)Marshal.ReadInt64(memory, 0x78),
                Rcx = (ulong)Marshal.ReadInt64(memory, 0x80),
                Rdx = (ulong)Marshal.ReadInt64(memory, 0x88),
                Rbx = (ulong)Marshal.ReadInt64(memory, 0x90),
                Rsp = (ulong)Marshal.ReadInt64(memory, 0x98),
                Rbp = (ulong)Marshal.ReadInt64(memory, 0xA0),
                Rsi = (ulong)Marshal.ReadInt64(memory, 0xA8),
                Rdi = (ulong)Marshal.ReadInt64(memory, 0xB0),
                R8 = (ulong)Marshal.ReadInt64(memory, 0xB8),
                R9 = (ulong)Marshal.ReadInt64(memory, 0xC0),
                R10 = (ulong)Marshal.ReadInt64(memory, 0xC8),
                R11 = (ulong)Marshal.ReadInt64(memory, 0xD0),
                R12 = (ulong)Marshal.ReadInt64(memory, 0xD8),
                R13 = (ulong)Marshal.ReadInt64(memory, 0xE0),
                R14 = (ulong)Marshal.ReadInt64(memory, 0xE8),
                R15 = (ulong)Marshal.ReadInt64(memory, 0xF0),
                Rip = (ulong)Marshal.ReadInt64(memory, 0xF8),
            };
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    internal static bool TrySetThreadContextDirect(nint threadHandle, ref Context64 context)
    {
        const int contextSize = 0x4D0;
        var memory = Marshal.AllocHGlobal(contextSize);
        try
        {
            Marshal.WriteInt32(memory, 0x30, (int)ContextFull);
            if (!GetThreadContextBuffer(threadHandle, memory))
                return false;

            PatchContext64ToBuffer(ref context, memory);
            return SetThreadContextBuffer(threadHandle, memory);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static void PatchContext64ToBuffer(ref Context64 context, nint memory)
    {
        Marshal.WriteInt32(memory, 0x44, (int)context.EFlags);
        Marshal.WriteInt64(memory, 0x78, (long)context.Rax);
        Marshal.WriteInt64(memory, 0x80, (long)context.Rcx);
        Marshal.WriteInt64(memory, 0x88, (long)context.Rdx);
        Marshal.WriteInt64(memory, 0x90, (long)context.Rbx);
        Marshal.WriteInt64(memory, 0x98, (long)context.Rsp);
        Marshal.WriteInt64(memory, 0xA0, (long)context.Rbp);
        Marshal.WriteInt64(memory, 0xA8, (long)context.Rsi);
        Marshal.WriteInt64(memory, 0xB0, (long)context.Rdi);
        Marshal.WriteInt64(memory, 0xB8, (long)context.R8);
        Marshal.WriteInt64(memory, 0xC0, (long)context.R9);
        Marshal.WriteInt64(memory, 0xC8, (long)context.R10);
        Marshal.WriteInt64(memory, 0xD0, (long)context.R11);
        Marshal.WriteInt64(memory, 0xD8, (long)context.R12);
        Marshal.WriteInt64(memory, 0xE0, (long)context.R13);
        Marshal.WriteInt64(memory, 0xE8, (long)context.R14);
        Marshal.WriteInt64(memory, 0xF0, (long)context.R15);
        Marshal.WriteInt64(memory, 0xF8, (long)context.Rip);
    }

    private static void WriteContext64ToBuffer(ref Context64 context, nint memory)
    {
        for (var i = 0; i < 0x4D0; i++)
            Marshal.WriteByte(memory, i, 0);

        context.ContextFlags = ContextFull;
        PatchContext64ToBuffer(ref context, memory);
        Marshal.WriteInt32(memory, 0x30, (int)context.ContextFlags);
    }

    internal static bool TryGetThreadContext(nint threadHandle, ref Context64 context)
    {
        context.ContextFlags = ContextFull;
        Win32DebugNative.SuspendThread(threadHandle);
        try
        {
            return GetThreadContext(threadHandle, ref context);
        }
        finally
        {
            Win32DebugNative.ResumeThread(threadHandle);
        }
    }

    internal static bool TrySetThreadContext(nint threadHandle, ref Context64 context)
    {
        context.ContextFlags = ContextFull;
        Win32DebugNative.SuspendThread(threadHandle);
        try
        {
            return SetThreadContext(threadHandle, ref context);
        }
        finally
        {
            Win32DebugNative.ResumeThread(threadHandle);
        }
    }

    internal static IReadOnlyList<DebugRegister> BuildRegisterList(in Context64 context)
    {
        static string Hex(ulong value) => $"0x{value:x}";
        return
        [
            new DebugRegister("rax", Hex(context.Rax)),
            new DebugRegister("rbx", Hex(context.Rbx)),
            new DebugRegister("rcx", Hex(context.Rcx)),
            new DebugRegister("rdx", Hex(context.Rdx)),
            new DebugRegister("rsi", Hex(context.Rsi)),
            new DebugRegister("rdi", Hex(context.Rdi)),
            new DebugRegister("rbp", Hex(context.Rbp)),
            new DebugRegister("rsp", Hex(context.Rsp)),
            new DebugRegister("r8", Hex(context.R8)),
            new DebugRegister("r9", Hex(context.R9)),
            new DebugRegister("r10", Hex(context.R10)),
            new DebugRegister("r11", Hex(context.R11)),
            new DebugRegister("r12", Hex(context.R12)),
            new DebugRegister("r13", Hex(context.R13)),
            new DebugRegister("r14", Hex(context.R14)),
            new DebugRegister("r15", Hex(context.R15)),
            new DebugRegister("rip", Hex(context.Rip)),
            new DebugRegister("eflags", Hex(context.EFlags)),
        ];
    }
}

internal static partial class NasmLineAddressResolver
{
    private sealed record AddressCacheEntry(long Ticks, List<ulong> Addresses);
    private static readonly Dictionary<string, AddressCacheEntry> AddressCache = new(StringComparer.OrdinalIgnoreCase);

    public static ulong? TryResolve(string executablePath, string nasmPath, int nasmLine)
        => TryResolveUserInstruction(executablePath, nasmPath, nasmLine);

    public static ulong? TryResolveUserInstruction(string executablePath, string nasmPath, int nasmLine)
    {
        if (!File.Exists(executablePath) || !File.Exists(nasmPath) || nasmLine <= 0)
            return null;

        var instructionIndex = MapNasmLineToInstructionIndex(nasmPath, nasmLine);
        if (instructionIndex == null)
            return null;

        if (!NativeBinaryEntryPoint.TryGetEntryPoint(executablePath, out var entry))
            return null;

        var objPath = ResolveCompanionObject(executablePath);
        if (string.IsNullOrEmpty(objPath))
            return null;

        var objAddresses = ReadTextSectionAddresses(objPath);
        if (objAddresses.Count == 0 || instructionIndex.Value >= objAddresses.Count)
            return null;

        var slide = objAddresses[instructionIndex.Value] - objAddresses[0];
        return entry + slide;
    }

    internal static string? ResolveCompanionObject(string executablePath)
    {
        var dir = Path.GetDirectoryName(executablePath);
        var name = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name))
            return null;

        if (name.EndsWith("-labdbg", StringComparison.OrdinalIgnoreCase))
            name = name[..^7];

        var objPath = Path.Combine(dir, name + ".obj");
        return File.Exists(objPath) ? objPath : null;
    }

    private static int? MapNasmLineToInstructionIndex(string nasmPath, int nasmLine)
    {
        var lines = File.ReadAllText(nasmPath).Replace("\r\n", "\n").Split('\n');
        var codeLines = new List<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (NasmLineClassifier.IsInstructionLine(lines[i]))
                codeLines.Add(i + 1);
        }

        var index = codeLines.IndexOf(nasmLine);
        return index >= 0 ? index : null;
    }

    private static List<ulong> ReadTextSectionAddresses(string executablePath)
    {
        if (!File.Exists(executablePath))
            return [];

        var ticks = File.GetLastWriteTimeUtc(executablePath).Ticks;
        if (AddressCache.TryGetValue(executablePath, out var cached) && cached.Ticks == ticks)
            return cached.Addresses;

        var list = new List<ulong>();
        if (!TryObjdump(executablePath, out var output))
            return list;

        var inText = false;
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Disassembly of section", StringComparison.Ordinal))
            {
                inText = line.Contains(".text", StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith("Disassembly of ", StringComparison.Ordinal))
            {
                inText = false;
                continue;
            }

            if (!inText)
                continue;

            var match = InstructionAddress().Match(line);
            if (!match.Success)
                continue;

            if (ulong.TryParse(match.Groups["addr"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var addr))
                list.Add(addr);
        }

        AddressCache[executablePath] = new AddressCacheEntry(ticks, list);
        return list;
    }

    private static bool TryObjdump(string binaryPath, out string output)
    {
        output = "";
        foreach (var tool in new[] { "llvm-objdump", "objdump" })
        {
            var resolved = ResolveToolExecutable(tool);
            if (resolved == null)
                continue;

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = resolved,
                    Arguments = $"-d \"{binaryPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process == null)
                    continue;

                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                if (process.ExitCode == 0 && output.Length > 0)
                    return true;
            }
            catch
            {
                // try next tool
            }
        }

        return false;
    }

    private static string? ResolveToolExecutable(string toolName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), toolName + ".exe");
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir.Trim(), toolName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    [GeneratedRegex(@"^(?<addr>[0-9a-fA-F]+):\s+")]
    private static partial Regex InstructionAddress();
}
