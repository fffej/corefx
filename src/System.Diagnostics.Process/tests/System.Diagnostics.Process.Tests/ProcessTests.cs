// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace System.Diagnostics.ProcessTests
{
    public class ProcessTests : ProcessTestBase
    {
        private void SetAndCheckBasePriority(ProcessPriorityClass exPriorityClass, int priority)
        {
            _process.PriorityClass = exPriorityClass;
            _process.Refresh();
            Assert.Equal(priority, _process.BasePriority);
        }

        private void AssertNonZeroWindowsZeroUnix(long value)
        {
            switch (global::Interop.PlatformDetection.OperatingSystem)
            {
                case global::Interop.OperatingSystem.Windows:
                    Assert.NotEqual(0, value);
                    break;
                default:
                    Assert.Equal(0, value);
                    break;
            }
        }

        [Fact, PlatformSpecific(PlatformID.Windows)]
        public void TestBasePriorityOnWindows()
        {
            ProcessPriorityClass originalPriority = _process.PriorityClass;
            Assert.Equal(ProcessPriorityClass.Normal, originalPriority);

            try
            {
                // We are not checking for RealTime case here, as RealTime priority process can 
                // preempt the threads of all other processes, including operating system processes
                // performing important tasks, which may cause the machine to be unresponsive.

                //SetAndCheckBasePriority(ProcessPriorityClass.RealTime, 24);

                SetAndCheckBasePriority(ProcessPriorityClass.High, 13);
                SetAndCheckBasePriority(ProcessPriorityClass.Idle, 4);
                SetAndCheckBasePriority(ProcessPriorityClass.Normal, 8);
            }
            finally
            {
                _process.PriorityClass = originalPriority;
            }
        }

        [Fact, PlatformSpecific(PlatformID.AnyUnix), OuterLoop] // This test requires admin elevation on Unix
        public void TestBasePriorityOnUnix()
        {
            ProcessPriorityClass originalPriority = _process.PriorityClass;
            Assert.Equal(ProcessPriorityClass.Normal, originalPriority);

            try
            {
                SetAndCheckBasePriority(ProcessPriorityClass.High, -11);
                SetAndCheckBasePriority(ProcessPriorityClass.Idle, 19);
                SetAndCheckBasePriority(ProcessPriorityClass.Normal, 0);
            }
            finally
            {
                _process.PriorityClass = originalPriority;
            }
        }

        [Fact]
        public void TestEnableRaiseEvents()
        {
            {
                bool isExitedInvoked = false;

                // Test behavior when EnableRaisingEvent = true;
                // Ensure event is called.
                Process p = CreateProcessInfinite();
                p.EnableRaisingEvents = true;
                p.Exited += delegate { isExitedInvoked = true; };
                StartAndKillProcessWithDelay(p);
                Assert.True(isExitedInvoked, String.Format("TestCanRaiseEvents0001: {0}", "isExited Event not called when EnableRaisingEvent is set to true."));
            }

            {
                bool isExitedInvoked = false;

                // Check with the default settings (false, events will not be raised)
                Process p = CreateProcessInfinite();
                p.Exited += delegate { isExitedInvoked = true; };
                StartAndKillProcessWithDelay(p);
                Assert.False(isExitedInvoked, String.Format("TestCanRaiseEvents0002: {0}", "isExited Event called with the default settings for EnableRaiseEvents"));
            }

            {
                bool isExitedInvoked = false;

                // Same test, this time explicitly set the property to false
                Process p = CreateProcessInfinite();
                p.EnableRaisingEvents = false;
                p.Exited += delegate { isExitedInvoked = true; }; ;
                StartAndKillProcessWithDelay(p);
                Assert.False(isExitedInvoked, String.Format("TestCanRaiseEvents0003: {0}", "isExited Event called with the EnableRaiseEvents = false"));
            }
        }

        [Fact]
        public void TestExitCode()
        {
            {
                Process p = CreateProcess();
                p.Start();
                Assert.True(p.WaitForExit(WaitInMS));
                Assert.Equal(SuccessExitCode, p.ExitCode);
            }

            {
                Process p = CreateProcessInfinite();
                StartAndKillProcessWithDelay(p);
                Assert.NotEqual(0, p.ExitCode);
            }
        }

        [Fact]
        public void TestExitTime()
        {
            DateTime timeBeforeProcessStart = DateTime.UtcNow;
            Process p = CreateProcessInfinite();
            StartAndKillProcessWithDelay(p);
            Assert.True(p.ExitTime.ToUniversalTime() > timeBeforeProcessStart, "TestExitTime is incorrect.");
        }


        [Fact]
        public void TestId()
        {
            if (global::Interop.IsWindows)
            {
                Assert.Equal(_process.Id, Interop.GetProcessId(_process.SafeHandle));
            }
            else
            {
                IEnumerable<int> testProcessIds = Process.GetProcessesByName(CoreRunName).Select(p => p.Id);
                Assert.Contains(_process.Id, testProcessIds);
            }
        }

        [Fact]
        public void TestHasExited()
        {
            {
                Process p = CreateProcess();
                p.Start();
                Assert.True(p.WaitForExit(WaitInMS));
                Assert.True(p.HasExited, "TestHasExited001 failed");
            }

            {
                Process p = CreateProcessInfinite();
                p.Start();
                try
                {
                    Assert.False(p.HasExited, "TestHasExited002 failed");
                }
                finally
                {
                    p.Kill();
                    Assert.True(p.WaitForExit(WaitInMS));
                }

                Assert.True(p.HasExited, "TestHasExited003 failed");
            }
        }

        [Fact]
        public void TestMachineName()
        {
            // Checking that the MachineName returns some value.
            Assert.NotNull(_process.MachineName);
        }

        [Fact, PlatformSpecific(~PlatformID.OSX)]
        public void TestMainModuleOnNonOSX()
        {
            string fileName = "corerun";
            if (global::Interop.IsWindows)
                fileName = "CoreRun.exe";

            Process p = Process.GetCurrentProcess();
            Assert.True(p.Modules.Count > 0);
            Assert.Equal(fileName, p.MainModule.ModuleName);
            Assert.EndsWith(fileName, p.MainModule.FileName);
            Assert.Equal(string.Format("System.Diagnostics.ProcessModule ({0})", fileName), p.MainModule.ToString());
        }

        [Fact]
        public void TestMaxWorkingSet()
        {
            using (Process p = Process.GetCurrentProcess())
            {
                Assert.True((long)p.MaxWorkingSet > 0);
                Assert.True((long)p.MinWorkingSet >= 0);
            }

            if (global::Interop.IsOSX)
                return; // doesn't support getting/setting working set for other processes

            long curValue = (long)_process.MaxWorkingSet;
            Assert.True(curValue >= 0);

            if (global::Interop.IsWindows)
            {
                try
                {
                    _process.MaxWorkingSet = (IntPtr)((int)curValue + 1024);

                    IntPtr min, max;
                    uint flags;
                    Interop.GetProcessWorkingSetSizeEx(_process.SafeHandle, out min, out max, out flags);
                    curValue = (int)max;
                    _process.Refresh();
                    Assert.Equal(curValue, (int)_process.MaxWorkingSet);
                }
                finally
                {
                    _process.MaxWorkingSet = (IntPtr)curValue;
                }
            }
        }

        [Fact]
        public void TestMinWorkingSet()
        {
            using (Process p = Process.GetCurrentProcess())
            {
                Assert.True((long)p.MaxWorkingSet > 0);
                Assert.True((long)p.MinWorkingSet >= 0);
            }

            if (global::Interop.IsOSX)
                return; // doesn't support getting/setting working set for other processes

            long curValue = (long)_process.MinWorkingSet;
            Assert.True(curValue >= 0);

            if (global::Interop.IsWindows)
            {
                try
                {
                    _process.MinWorkingSet = (IntPtr)((int)curValue - 1024);

                    IntPtr min, max;
                    uint flags;
                    Interop.GetProcessWorkingSetSizeEx(_process.SafeHandle, out min, out max, out flags);
                    curValue = (int)min;
                    _process.Refresh();
                    Assert.Equal(curValue, (int)_process.MinWorkingSet);
                }
                finally
                {
                    _process.MinWorkingSet = (IntPtr)curValue;
                }
            }
        }

        [Fact]
        public void TestModules()
        {
            foreach (ProcessModule pModule in _process.Modules)
            {
                // Validated that we can get a value for each of the following.
                Assert.NotNull(pModule);
                Assert.NotEqual(IntPtr.Zero, pModule.BaseAddress);
                Assert.NotNull(pModule.FileName);
                Assert.NotNull(pModule.ModuleName);

                // Just make sure these don't throw
                IntPtr addr = pModule.EntryPointAddress;
                int memSize = pModule.ModuleMemorySize;
            }
        }

        [Fact]
        public void TestNonpagedSystemMemorySize64()
        {
            AssertNonZeroWindowsZeroUnix(_process.NonpagedSystemMemorySize64);
        }

        [Fact]
        public void TestPagedMemorySize64()
        {
            AssertNonZeroWindowsZeroUnix(_process.PagedMemorySize64);
        }

        [Fact]
        public void TestPagedSystemMemorySize64()
        {
            AssertNonZeroWindowsZeroUnix(_process.PagedSystemMemorySize64);
        }

        [Fact]
        public void TestPeakPagedMemorySize64()
        {
            AssertNonZeroWindowsZeroUnix(_process.PeakPagedMemorySize64);
        }

        [Fact]
        public void TestPeakVirtualMemorySize64()
        {
            AssertNonZeroWindowsZeroUnix(_process.PeakVirtualMemorySize64);
        }

        [Fact]
        public void TestPeakWorkingSet64()
        {
            AssertNonZeroWindowsZeroUnix(_process.PeakWorkingSet64);
        }

        [Fact]
        public void TestPrivateMemorySize64()
        {
            AssertNonZeroWindowsZeroUnix(_process.PrivateMemorySize64);
        }

        [Fact]
        public void TestVirtualMemorySize64()
        {
            Assert.True(_process.VirtualMemorySize64 > 0);
        }

        [Fact]
        public void TestWorkingSet64()
        {
            Assert.True(_process.WorkingSet64 > 0);
        }

        [Fact]
        public void TestProcessorTime()
        {
            Assert.True(_process.UserProcessorTime.TotalSeconds >= 0);
            Assert.True(_process.PrivilegedProcessorTime.TotalSeconds >= 0);

            double processorTimeBeforeSpin = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
            double processorTimeAtHalfSpin = 0;
            // Perform loop to occupy cpu, takes less than a second.
            int i = int.MaxValue / 16;
            while (i > 0)
            {
                i--;
                if (i == int.MaxValue / 32)
                    processorTimeAtHalfSpin = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
            }

            Assert.InRange(processorTimeAtHalfSpin, processorTimeBeforeSpin, Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds);
        }

        [Fact]
        public void TestProcessStartTime()
        {
            DateTime timeBeforeCreatingProcess = DateTime.UtcNow;
            Process p = CreateProcessInfinite();
            try
            {
                p.Start();

                // Time in unix, is measured in jiffies, which is incremented by one for every timer interrupt since the boot time.
                // Thus, because there are HZ timer interrupts in a second, there are HZ jiffies in a second. Hence 1\HZ, will
                // be the resolution of system timer. The lowest value of HZ on unix is 100, hence the timer resolution is 10 ms.
                // Allowing for error in 10 ms.
                long tenMSTicks = new TimeSpan(0, 0, 0, 0, 10).Ticks;
                long beforeTicks = timeBeforeCreatingProcess.Ticks - tenMSTicks;
                long afterTicks = DateTime.UtcNow.Ticks + tenMSTicks;
                Assert.InRange(p.StartTime.ToUniversalTime().Ticks, beforeTicks, afterTicks);
            }
            finally
            {
                if (!p.HasExited)
                    p.Kill();

                Assert.True(p.WaitForExit(WaitInMS));
            }
        }

        [Fact]
        [PlatformSpecific(~PlatformID.OSX)] // getting/setting affinity not supported on OSX
        public void TestProcessorAffinity()
        {
            IntPtr curProcessorAffinity = _process.ProcessorAffinity;
            try
            {
                _process.ProcessorAffinity = new IntPtr(0x1);
                Assert.Equal(new IntPtr(0x1), _process.ProcessorAffinity);
            }
            finally
            {
                _process.ProcessorAffinity = curProcessorAffinity;
                Assert.Equal(curProcessorAffinity, _process.ProcessorAffinity);
            }
        }

        [Fact]
        public void TestPriorityBoostEnabled()
        {
            bool isPriorityBoostEnabled = _process.PriorityBoostEnabled;
            try
            {
                _process.PriorityBoostEnabled = true;
                Assert.True(_process.PriorityBoostEnabled, "TestPriorityBoostEnabled001 failed");

                _process.PriorityBoostEnabled = false;
                Assert.False(_process.PriorityBoostEnabled, "TestPriorityBoostEnabled002 failed");
            }
            finally
            {
                _process.PriorityBoostEnabled = isPriorityBoostEnabled;
            }
        }

        [Fact, PlatformSpecific(PlatformID.AnyUnix), OuterLoop] // This test requires admin elevation on Unix
        public void TestPriorityClassUnix()
        {
            ProcessPriorityClass priorityClass = _process.PriorityClass;
            try
            {
                _process.PriorityClass = ProcessPriorityClass.High;
                Assert.Equal(_process.PriorityClass, ProcessPriorityClass.High);

                _process.PriorityClass = ProcessPriorityClass.Normal;
                Assert.Equal(_process.PriorityClass, ProcessPriorityClass.Normal);
            }
            finally
            {
                _process.PriorityClass = priorityClass;
            }
        }

        [Fact, PlatformSpecific(PlatformID.Windows)]
        public void TestPriorityClassWindows()
        {
            ProcessPriorityClass priorityClass = _process.PriorityClass;
            try
            {
                _process.PriorityClass = ProcessPriorityClass.High;
                Assert.Equal(_process.PriorityClass, ProcessPriorityClass.High);

                _process.PriorityClass = ProcessPriorityClass.Normal;
                Assert.Equal(_process.PriorityClass, ProcessPriorityClass.Normal);
            }
            finally
            {
                _process.PriorityClass = priorityClass;
            }
        }

        [Fact]
        public void TestInvalidPriorityClass()
        {
            Process p = new Process();
            Assert.Throws<ArgumentException>(() => { p.PriorityClass = ProcessPriorityClass.Normal | ProcessPriorityClass.Idle; });
        }

        [Fact]
        public void TestProcessName()
        {
            Assert.Equal(_process.ProcessName, CoreRunName, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void TestSafeHandle()
        {
            Assert.False(_process.SafeHandle.IsInvalid);
        }

        [Fact]
        public void TestSessionId()
        {
            uint sessionId;
            if (global::Interop.IsWindows)
            {
                Interop.ProcessIdToSessionId((uint)_process.Id, out sessionId);
            }
            else
            {
                sessionId = (uint)Interop.getsid(_process.Id);
            }

            Assert.Equal(sessionId, (uint)_process.SessionId);
        }

        [Fact]
        public void TestGetCurrentProcess()
        {
            Process current = Process.GetCurrentProcess();
            Assert.NotNull(current);

            int currentProcessId = global::Interop.IsWindows ?
                Interop.GetCurrentProcessId() :
                Interop.getpid();

            Assert.Equal(currentProcessId, current.Id);
        }

        [Fact]
        public void TestGetProcessById()
        {
            Process p = Process.GetProcessById(_process.Id);
            Assert.Equal(_process.Id, p.Id);
            Assert.Equal(_process.ProcessName, p.ProcessName);
        }

        [Fact]
        public void TestGetProcesses()
        {
            Process currentProcess = Process.GetCurrentProcess();

            // Get all the processes running on the machine, and check if the current process is one of them.
            var foundCurrentProcess = (from p in Process.GetProcesses()
                                       where (p.Id == currentProcess.Id) && (p.ProcessName.Equals(currentProcess.ProcessName))
                                       select p).Any();

            Assert.True(foundCurrentProcess, "TestGetProcesses001 failed");

            foundCurrentProcess = (from p in Process.GetProcesses(currentProcess.MachineName)
                                   where (p.Id == currentProcess.Id) && (p.ProcessName.Equals(currentProcess.ProcessName))
                                   select p).Any();

            Assert.True(foundCurrentProcess, "TestGetProcesses002 failed");
        }

        [Fact]
        public void TestGetProcessesByName()
        {
            // Get the current process using its name
            Process currentProcess = Process.GetCurrentProcess();

            Assert.True(Process.GetProcessesByName(currentProcess.ProcessName).Count() > 0, "TestGetProcessesByName001 failed");
            Assert.True(Process.GetProcessesByName(currentProcess.ProcessName, currentProcess.MachineName).Count() > 0, "TestGetProcessesByName001 failed");
        }

        public static IEnumerable<object[]> GetTestProcess()
        {
            Process currentProcess = Process.GetCurrentProcess();
            yield return new object[] { currentProcess, Process.GetProcessById(currentProcess.Id, "127.0.0.1") };
            yield return new object[] { currentProcess, Process.GetProcessesByName(currentProcess.ProcessName, "127.0.0.1").Where(p => p.Id == currentProcess.Id).Single() };
        }

        [Theory, PlatformSpecific(PlatformID.Windows)]
        [MemberData("GetTestProcess")]
        public void TestProcessOnRemoteMachineWindows(Process currentProcess, Process remoteProcess)
        {
            Assert.Equal(currentProcess.Id, remoteProcess.Id);
            Assert.Equal(currentProcess.BasePriority, remoteProcess.BasePriority);
            Assert.Equal(currentProcess.EnableRaisingEvents, remoteProcess.EnableRaisingEvents);
            Assert.Equal("127.0.0.1", remoteProcess.MachineName);
            // This property throws exception only on remote processes.
            Assert.Throws<NotSupportedException>(() => remoteProcess.MainModule);
        }

        [Fact, PlatformSpecific(PlatformID.AnyUnix)]
        public void TestProcessOnRemoteMachineUnix()
        {
            Process currentProcess = Process.GetCurrentProcess();

            Assert.Throws<PlatformNotSupportedException>(() => Process.GetProcessesByName(currentProcess.ProcessName, "127.0.0.1"));
            Assert.Throws<PlatformNotSupportedException>(() => Process.GetProcessById(currentProcess.Id, "127.0.0.1"));
        }

        [Fact]
        public void TestStartInfo()
        {
            {
                Process process = CreateProcessInfinite();
                process.Start();

                Assert.Equal(CoreRunName, process.StartInfo.FileName);

                process.Kill();
                Assert.True(process.WaitForExit(WaitInMS));
            }

            {
                Process process = CreateProcessInfinite();
                process.Start();

                Assert.Throws<System.InvalidOperationException>(() => (process.StartInfo = new ProcessStartInfo()));

                process.Kill();
                Assert.True(process.WaitForExit(WaitInMS));
            }

            {
                Process process = new Process();
                process.StartInfo = new ProcessStartInfo(TestExeName);
                Assert.Equal(TestExeName, process.StartInfo.FileName);
            }

            {
                Process process = new Process();
                Assert.Throws<ArgumentNullException>(() => process.StartInfo = null);
            }

            {
                Process process = Process.GetCurrentProcess();
                Assert.Throws<System.InvalidOperationException>(() => process.StartInfo);
            }
        }


        // [Fact] // uncomment for diagnostic purposes to list processes to console
        public void TestDiagnosticsWithConsoleWriteLine()
        {
            foreach (var p in Process.GetProcesses().OrderBy(p => p.Id))
            {
                Console.WriteLine("{0} : \"{1}\" (Threads: {2})", p.Id, p.ProcessName, p.Threads.Count);
                p.Dispose();
            }
        }
    }
}
