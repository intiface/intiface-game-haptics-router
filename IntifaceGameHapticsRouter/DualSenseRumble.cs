using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace IntifaceGameHapticsRouter
{
    // USB-only DualSense rumble writer. Prototype — BT requires the 0x31 report with CRC32 and is not implemented here.
    internal sealed class DualSenseRumble : IDisposable
    {
        private const ushort SonyVid = 0x054C;
        private const ushort DualSensePid = 0x0CE6;
        private const ushort DualSenseEdgePid = 0x0DF2;

        // USB DualSense output report total length including the report ID byte.
        private const int UsbOutputReportSize = 48;

        private readonly object _lock = new object();
        private SafeFileHandle _handle;
        private byte[] _buffer;

        public bool IsOpen
        {
            get
            {
                var h = _handle;
                return h != null && !h.IsInvalid && !h.IsClosed;
            }
        }

        public string DevicePath { get; private set; }

        // Attempt to enumerate and open a USB-mode DualSense. Returns true on success.
        public bool TryOpen()
        {
            Close();
            var path = FindDualSensePath();
            if (path == null)
            {
                return false;
            }

            var handle = NativeMethods.CreateFile(
                path,
                NativeMethods.GenericWrite,
                NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
                IntPtr.Zero,
                NativeMethods.OpenExisting,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                handle.Dispose();
                return false;
            }

            _handle = handle;
            DevicePath = path;
            _buffer = new byte[UsbOutputReportSize];
            return true;
        }

        // Writes a USB output report 0x02 with the given motor levels (0..255).
        // strongMotor = low-frequency rumble (XInput LeftMotor equivalent).
        // weakMotor   = high-frequency rumble (XInput RightMotor equivalent).
        public bool SetRumble(byte strongMotor, byte weakMotor)
        {
            if (!IsOpen)
            {
                return false;
            }

            lock (_lock)
            {
                if (!IsOpen)
                {
                    return false;
                }

                Array.Clear(_buffer, 0, _buffer.Length);
                _buffer[0] = 0x02; // USB output report id
                _buffer[1] = 0xFF; // valid_flag0: update everything (rumble, triggers, etc.)
                _buffer[2] = 0xF7; // valid_flag1: update LEDs / mute LEDs / mic, leave lightbar control untouched
                _buffer[3] = weakMotor;   // rumble_right (small motor)
                _buffer[4] = strongMotor; // rumble_left (large motor)

                if (!NativeMethods.WriteFile(_handle, _buffer, (uint)_buffer.Length, out _, IntPtr.Zero))
                {
                    // Likely a disconnect or another process took exclusive access. Drop the handle so a
                    // future TryOpen can re-enumerate.
                    CloseLocked();
                    return false;
                }

                return true;
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                CloseLocked();
            }
        }

        private void CloseLocked()
        {
            _handle?.Dispose();
            _handle = null;
            DevicePath = null;
            _buffer = null;
        }

        public void Dispose() => Close();

        private static string FindDualSensePath()
        {
            NativeMethods.HidD_GetHidGuid(out var hidGuid);

            var devSet = NativeMethods.SetupDiGetClassDevs(
                ref hidGuid,
                null,
                IntPtr.Zero,
                NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

            if (devSet == new IntPtr(-1))
            {
                return null;
            }

            try
            {
                var iface = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                iface.cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVICE_INTERFACE_DATA));

                for (int idx = 0; NativeMethods.SetupDiEnumDeviceInterfaces(devSet, IntPtr.Zero, ref hidGuid, idx, ref iface); idx++)
                {
                    var path = GetInterfaceDetailPath(devSet, ref iface);
                    if (path == null)
                    {
                        continue;
                    }

                    if (IsDualSense(path, out var isUsb) && isUsb)
                    {
                        return path;
                    }
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(devSet);
            }

            return null;
        }

        private static string GetInterfaceDetailPath(IntPtr devSet, ref NativeMethods.SP_DEVICE_INTERFACE_DATA iface)
        {
            NativeMethods.SetupDiGetDeviceInterfaceDetail(devSet, ref iface, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);
            if (requiredSize <= 0)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal(requiredSize);
            try
            {
                // SP_DEVICE_INTERFACE_DETAIL_DATA: first field is DWORD cbSize.
                // On x64 the struct is 8-byte aligned so cbSize is "5 + 3 padding"; on x86 it's 5.
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : (4 + Marshal.SystemDefaultCharSize));

                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devSet, ref iface, buffer, requiredSize, out _, IntPtr.Zero))
                {
                    return null;
                }

                // Path string starts immediately after the cbSize field (4 bytes).
                var pathPtr = new IntPtr(buffer.ToInt64() + 4);
                return Marshal.PtrToStringAuto(pathPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool IsDualSense(string path, out bool isUsb)
        {
            isUsb = false;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var handle = NativeMethods.CreateFile(
                path,
                0,
                NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
                IntPtr.Zero,
                NativeMethods.OpenExisting,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                handle.Dispose();
                return false;
            }

            try
            {
                var attr = new NativeMethods.HIDD_ATTRIBUTES();
                attr.Size = Marshal.SizeOf(typeof(NativeMethods.HIDD_ATTRIBUTES));
                if (!NativeMethods.HidD_GetAttributes(handle, ref attr))
                {
                    return false;
                }

                if (attr.VendorID != SonyVid)
                {
                    return false;
                }
                if (attr.ProductID != DualSensePid && attr.ProductID != DualSenseEdgePid)
                {
                    return false;
                }

                // BT enumerations expose multiple interface paths; we want the one with the small USB-style
                // output report. Use HidP caps to decide. Path-string heuristics (e.g. "_bthenum") are unreliable.
                if (!NativeMethods.HidD_GetPreparsedData(handle, out var preparsed) || preparsed == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    if (NativeMethods.HidP_GetCaps(preparsed, out var caps) != NativeMethods.HIDP_STATUS_SUCCESS)
                    {
                        return false;
                    }

                    // USB DualSense reports 64-byte output (we only write 48 of it).
                    // BT DualSense reports 547+ byte output and needs the 0x31 report with CRC32 — not handled here.
                    isUsb = caps.OutputReportByteLength <= 64;
                    return true;
                }
                finally
                {
                    NativeMethods.HidD_FreePreparsedData(preparsed);
                }
            }
            finally
            {
                handle.Dispose();
            }
        }

        private static class NativeMethods
        {
            public const uint GenericRead = 0x80000000;
            public const uint GenericWrite = 0x40000000;
            public const uint FileShareRead = 0x00000001;
            public const uint FileShareWrite = 0x00000002;
            public const uint OpenExisting = 3;
            public const int DIGCF_PRESENT = 0x02;
            public const int DIGCF_DEVICEINTERFACE = 0x10;
            public const int HIDP_STATUS_SUCCESS = unchecked((int)0x00110000);

            [StructLayout(LayoutKind.Sequential)]
            public struct SP_DEVICE_INTERFACE_DATA
            {
                public int cbSize;
                public Guid InterfaceClassGuid;
                public int Flags;
                public IntPtr Reserved;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HIDD_ATTRIBUTES
            {
                public int Size;
                public ushort VendorID;
                public ushort ProductID;
                public ushort VersionNumber;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HIDP_CAPS
            {
                public ushort Usage;
                public ushort UsagePage;
                public ushort InputReportByteLength;
                public ushort OutputReportByteLength;
                public ushort FeatureReportByteLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
                public ushort[] Reserved;
                public ushort NumberLinkCollectionNodes;
                public ushort NumberInputButtonCaps;
                public ushort NumberInputValueCaps;
                public ushort NumberInputDataIndices;
                public ushort NumberOutputButtonCaps;
                public ushort NumberOutputValueCaps;
                public ushort NumberOutputDataIndices;
                public ushort NumberFeatureButtonCaps;
                public ushort NumberFeatureValueCaps;
                public ushort NumberFeatureDataIndices;
            }

            [DllImport("hid.dll")]
            public static extern void HidD_GetHidGuid(out Guid HidGuid);

            [DllImport("hid.dll")]
            [return: MarshalAs(UnmanagedType.U1)]
            public static extern bool HidD_GetAttributes(SafeFileHandle HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

            [DllImport("hid.dll")]
            [return: MarshalAs(UnmanagedType.U1)]
            public static extern bool HidD_GetPreparsedData(SafeFileHandle HidDeviceObject, out IntPtr PreparsedData);

            [DllImport("hid.dll")]
            [return: MarshalAs(UnmanagedType.U1)]
            public static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

            [DllImport("hid.dll")]
            public static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetupDiGetClassDevs(
                ref Guid ClassGuid,
                string Enumerator,
                IntPtr hwndParent,
                int Flags);

            [DllImport("setupapi.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

            [DllImport("setupapi.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiEnumDeviceInterfaces(
                IntPtr DeviceInfoSet,
                IntPtr DeviceInfoData,
                ref Guid InterfaceClassGuid,
                int MemberIndex,
                ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiGetDeviceInterfaceDetail(
                IntPtr DeviceInfoSet,
                ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
                IntPtr DeviceInterfaceDetailData,
                int DeviceInterfaceDetailDataSize,
                out int RequiredSize,
                IntPtr DeviceInfoData);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern SafeFileHandle CreateFile(
                string lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                IntPtr lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool WriteFile(
                SafeFileHandle hFile,
                byte[] lpBuffer,
                uint nNumberOfBytesToWrite,
                out uint lpNumberOfBytesWritten,
                IntPtr lpOverlapped);
        }
    }
}
