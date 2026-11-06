using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Ties child processes to this process with a Windows job object set to kill its members when the last handle closes. The
    /// operating system closes that handle when this process ends, however it ends, so adopted children cannot be orphaned.
    /// </summary>
    /// <remarks>
    /// Only the processes adopted are put in the job, not this whole process. Other children must be able to outlive it: the
    /// update restart helper has to keep running after the service exits.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsJobChildProcessReaper : IChildProcessReaper, IDisposable
    {
        private const uint JobObjectLimitKillOnJobClose = 0x2000;
        private const int JobObjectExtendedLimitInformation = 9;
        private const uint Th32csSnapProcess = 0x2;
        private const uint ProcessTerminate = 0x0001;
        private const uint ProcessSetQuota = 0x0100;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private static readonly IntPtr InvalidHandle = new(-1);

        private readonly ILogger _logger;
        private IntPtr _job;

        /// <summary>Creates the job object. Throws <see cref="Win32Exception"/> when Windows refuses to create or configure it.</summary>
        public WindowsJobChildProcessReaper(ILogger<WindowsJobChildProcessReaper>? logger = null)
        {
            _logger = logger ?? NullLogger<WindowsJobChildProcessReaper>.Instance;

            _job = CreateJobObjectW(IntPtr.Zero, null);
            if (_job == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the job object");
            }

            var info = new JobObjectExtendedLimitInformationStruct();
            info.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
            int size = Marshal.SizeOf<JobObjectExtendedLimitInformationStruct>();
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, buffer, fDeleteOld: false);
                if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation, buffer, (uint)size))
                {
                    int error = Marshal.GetLastWin32Error();
                    _ = CloseHandle(_job);
                    _job = IntPtr.Zero;
                    throw new Win32Exception(error, "Could not configure the job object");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <inheritdoc />
        public int AdoptChildren(string executableName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
            if (_job == IntPtr.Zero)
            {
                return 0;
            }

            uint self = (uint)Environment.ProcessId;
            IntPtr snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == InvalidHandle)
            {
                _logger.LogError("Could not list processes to adopt {Executable} children: {Error}", executableName, new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return 0;
            }

            int adopted = 0;
            try
            {
                var entry = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
                for (bool more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
                {
                    if (entry.th32ParentProcessID == self && string.Equals(entry.szExeFile, executableName, StringComparison.OrdinalIgnoreCase)
                        && TryAdopt(entry.th32ProcessID, executableName))
                    {
                        adopted++;
                    }
                }
            }
            finally
            {
                _ = CloseHandle(snapshot);
            }

            return adopted;
        }

        private bool TryAdopt(uint processId, string executableName)
        {
            IntPtr process = OpenProcess(ProcessSetQuota | ProcessTerminate | ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero)
            {
                _logger.LogError("Could not open {Executable} process {ProcessId} to tie it to this service: {Error}", executableName, processId, new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return false;
            }

            try
            {
                // Already in the job (adopted on an earlier call) counts as adopted.
                if (IsProcessInJob(process, _job, out bool inJob) && inJob)
                {
                    return true;
                }

                if (AssignProcessToJobObject(_job, process))
                {
                    return true;
                }

                _logger.LogError("Could not tie {Executable} process {ProcessId} to this service: {Error}", executableName, processId, new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return false;
            }
            finally
            {
                _ = CloseHandle(process);
            }
        }

        /// <summary>Closes the job, which ends every process still in it.</summary>
        public void Dispose()
        {
            IntPtr job = Interlocked.Exchange(ref _job, IntPtr.Zero);
            if (job != IntPtr.Zero)
            {
                _ = CloseHandle(job);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformationStruct
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCountersStruct
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformationStruct
        {
            public JobObjectBasicLimitInformationStruct BasicLimitInformation;
            public IoCountersStruct IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public UIntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObjectW(IntPtr jobAttributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint infoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsProcessInJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool result);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32 entry);
    }
}
