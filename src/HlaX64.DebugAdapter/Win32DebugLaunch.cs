using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HlaX64.DebugAdapter;

/// <summary>Windows-only helpers for starting PE targets under MinGW GDB.</summary>
internal static class Win32DebugLaunch
{
    private const uint CreateSuspended = 0x00000004;

    public static bool TryStartWithEntryTrap(
        string executable,
        ulong entryPoint,
        out int processId,
        out int threadId,
        out byte originalOpcode,
        out nint processHandle)
    {
        processId = 0;
        threadId = 0;
        originalOpcode = 0;
        processHandle = 0;

        if (!OperatingSystem.IsWindows())
            return false;

        var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
        if (!CreateProcess(
                null,
                $"\"{executable}\"",
                nint.Zero,
                nint.Zero,
                false,
                CreateSuspended,
                nint.Zero,
                null,
                ref si,
                out var pi))
        {
            return false;
        }

        processId = pi.dwProcessId;
        threadId = pi.dwThreadId;
        processHandle = pi.hProcess;

        try
        {
            var addr = (nint)entryPoint;
            var opcode = new byte[1];
            if (!ReadProcessMemory(pi.hProcess, addr, opcode, 1, out var bytesRead) || bytesRead != 1)
            {
                TerminateProcess(pi.hProcess, 1);
                return false;
            }

            originalOpcode = opcode[0];
            if (!WriteProcessMemory(pi.hProcess, addr, [0xCC], 1, out var bytesWritten) || bytesWritten != 1)
            {
                TerminateProcess(pi.hProcess, 1);
                return false;
            }

            FlushInstructionCache(pi.hProcess, addr, 1);
            return true;
        }
        catch
        {
            TerminateProcess(pi.hProcess, 1);
            return false;
        }
        finally
        {
            CloseHandle(pi.hThread);
        }
    }

    public static bool TryReadRip(int threadId, out ulong rip)
    {
        rip = 0;
        if (threadId <= 0)
            return false;

        var handle = OpenThread(ThreadGetContext | ThreadQueryInformation | ThreadSuspendResume, false, threadId);
        if (handle == 0)
            return false;

        try
        {
            SuspendThread(handle);
            try
            {
                var context = new CONTEXT64 { ContextFlags = ContextControl };
                if (!GetThreadContext(handle, ref context))
                    return false;

                rip = context.Rip;
                return rip != 0;
            }
            finally
            {
                ResumeThread(handle);
            }
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public static bool TryWriteRip(int threadId, ulong rip)
    {
        if (threadId <= 0)
            return false;

        var handle = OpenThread(ThreadGetContext | ThreadSetContext | ThreadQueryInformation | ThreadSuspendResume, false, threadId);
        if (handle == 0)
            return false;

        try
        {
            SuspendThread(handle);
            try
            {
                var context = new CONTEXT64 { ContextFlags = ContextControl };
                if (!GetThreadContext(handle, ref context))
                    return false;

                context.Rip = rip;
                return SetThreadContext(handle, ref context);
            }
            finally
            {
                ResumeThread(handle);
            }
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public static bool TryRestoreOpcode(nint processHandle, ulong entryPoint, byte originalOpcode)
    {
        if (processHandle == 0)
            return false;

        var addr = (nint)entryPoint;
        var restored = WriteProcessMemory(processHandle, addr, [originalOpcode], 1, out var bytesWritten)
                       && bytesWritten == 1;
        if (restored)
            FlushInstructionCache(processHandle, addr, 1);

        return restored;
    }

    public static void Terminate(nint processHandle)
    {
        if (processHandle == 0)
            return;

        try { TerminateProcess(processHandle, 1); } catch { /* ignore */ }
        try { CloseHandle(processHandle); } catch { /* ignore */ }
    }

    public static void TryStopProcess(int processId)
    {
        if (processId <= 0)
            return;

        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // process already exited
        }
    }

    public static void TryStopProcessesForExecutable(string executablePath)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var fullPath = Path.GetFullPath(executablePath);
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        if (string.IsNullOrWhiteSpace(baseName))
            return;

        foreach (var name in new[] { baseName, $"{baseName}-labdbg" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try { TryKillProcess(process); }
                finally { process.Dispose(); }
            }
        }

        TryStopProcessesWithImagePath(fullPath);
    }

    private static void TryStopProcessesWithImagePath(string fullPath)
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.HasExited)
                    continue;

                var image = process.MainModule?.FileName;
                if (image != null
                    && string.Equals(Path.GetFullPath(image), fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    TryKillProcess(process);
                }
            }
            catch
            {
                // MainModule access denied for some processes
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore access denied for unrelated processes
        }
    }

    public static void TryStopDebuggerProcesses()
    {
        if (!OperatingSystem.IsWindows())
            return;

        foreach (var name in new[] { "gdb", "gdborig", "lldb" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try { TryKillProcess(process); }
                finally { process.Dispose(); }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public nint lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        string lpCommandLine,
        nint lpProcessAttributes,
        nint lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        nint lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SuspendThread(nint hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(nint hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenThread(uint dwDesiredAccess, bool bInheritHandle, int dwThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetThreadContext(nint hThread, ref CONTEXT64 lpContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetThreadContext(nint hThread, ref CONTEXT64 lpContext);

    private const uint ThreadQueryInformation = 0x0040;
    private const uint ThreadGetContext = 0x0008;
    private const uint ThreadSetContext = 0x0010;
    private const uint ThreadSuspendResume = 0x0002;
    private const uint ContextControl = 0x00100001;

    [StructLayout(LayoutKind.Sequential)]
    private struct CONTEXT64
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(
        nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushInstructionCache(nint hProcess, nint lpBaseAddress, int dwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(nint hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);
}

/// <summary>Releases OS locks on build outputs held by debug sessions.</summary>
public static class DebugProcessCleanup
{
    public static bool TryEnsureWritable(string? outputPath, int timeoutMs = 4000)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return true;

        ReleaseOutputFile(outputPath);

        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (TryOpenForWrite(outputPath))
                return true;

            Thread.Sleep(150);
            ReleaseOutputFile(outputPath);
        }

        return TryOpenForWrite(outputPath);
    }

    public static void ReleaseDebuggerProcesses()
    {
        if (OperatingSystem.IsWindows())
            Win32DebugLaunch.TryStopDebuggerProcesses();
    }

    public static void ReleaseOutputFile(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        if (OperatingSystem.IsWindows())
        {
            Win32DebugLaunch.TryStopProcessesForExecutable(outputPath);
            Win32DebugLaunch.TryStopDebuggerProcesses();
        }

        Thread.Sleep(100);

        foreach (var sidecar in new[]
                 {
                     Path.ChangeExtension(outputPath, ".pdb"),
                     outputPath + ".pdb",
                 })
        {
            if (!File.Exists(sidecar))
                continue;

            try { File.Delete(sidecar); } catch { /* linker may overwrite */ }
        }
    }

    private static bool TryOpenForWrite(string path)
    {
        if (!File.Exists(path))
            return true;

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
