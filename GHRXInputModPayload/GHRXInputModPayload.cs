using EasyHook;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using GHRXInputModInterface;

namespace GHRXInputModPayload
{
    public class GHRXInputModPayload : IEntryPoint
    {
        private readonly GHRXInputModInterface.GHRXInputModInterface _interface;
        private Vibration _lastMessage = new Vibration {LeftMotorSpeed = 65535, RightMotorSpeed = 65535};
        private static bool _shouldPassthru = true;
        private readonly Queue<Vibration> _messageQueue = new Queue<Vibration>();
        private static Exception _ex;
        private static GHRXInputModPayload _instance;

        // Use 1_3 first, seems to be most popular. It seems that some games
        // link both xinput1_4 and xinput9_1_0, but seem to prefer xinput9_1_0?
        // Not quite sure about difference yet. 
        private enum XInputVersion
        {
            xinput1_3,
            xinput9_1_0,
            xinput1_4
        };

        private static Dictionary<XInputVersion, XInputSetStateDelegate> _delegateMap = new Dictionary<XInputVersion, XInputSetStateDelegate>()
        {
            { XInputVersion.xinput1_3, XInputSetStateHookFunc1_3 },
            { XInputVersion.xinput1_4, XInputSetStateHookFunc1_4 },
            { XInputVersion.xinput9_1_0, XInputSetStateHookFunc9_1_0 },
        };

        public GHRXInputModPayload(
            RemoteHooking.IContext aInContext,
            String aInChannelName)
        {
            _interface = RemoteHooking.IpcConnectClient<GHRXInputModInterface.GHRXInputModInterface>(aInChannelName);
            _instance = this;
        }

        public void Run(
            RemoteHooking.IContext aInContext,
            String aInArg1)
        {
            _interface.Ping(RemoteHooking.GetCurrentProcessId(), "Payload installed. Running payload loop.");
            var isHooked = false;
            foreach (var xinputType in _delegateMap)
            {
                try
                {
                    _interface.Ping(RemoteHooking.GetCurrentProcessId(), $"Trying to hook {xinputType.Key}.dll");
                    var hookObj = LocalHook.Create(
                        LocalHook.GetProcAddress($"{xinputType.Key}.dll", "XInputSetState"),
                        new XInputSetStateDelegate(xinputType.Value),
                        null);
                    hookObj.ThreadACL.SetExclusiveACL(new Int32[1]);
                    _interface.Ping(RemoteHooking.GetCurrentProcessId(), $"Hooked {xinputType.Key}.dll");
                    isHooked = true;
                }
                catch
                {
                    // noop
                    _interface.Ping(RemoteHooking.GetCurrentProcessId(), $"Hooking {xinputType.Key}.dll failed");
                }
            }
            if (!isHooked)
            {
                _interface.ReportError(RemoteHooking.GetCurrentProcessId(), new Exception("No viable DLL to hook, payload exiting"));
                return;
            }

            try
            {
                while (_interface.Ping(RemoteHooking.GetCurrentProcessId(), ""))
                {
                    Thread.Sleep(1);
                    _shouldPassthru = _interface.ShouldPassthru();
                    if (_messageQueue.Count > 0)
                    {
                        lock (_messageQueue)
                        {
                            _interface.Report(RemoteHooking.GetCurrentProcessId(), _messageQueue);
                            _messageQueue.Clear();
                        }
                    }
                    if (_ex != null)
                    {
                        _interface.ReportError(RemoteHooking.GetCurrentProcessId(), _ex);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _interface.ReportError(RemoteHooking.GetCurrentProcessId(), e);
            }
            _interface.Ping(RemoteHooking.GetCurrentProcessId(), "Exiting payload loop");
            _interface.Exit();
        }

        [DllImport("xinput1_3.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputSetState")]
        private static extern unsafe int XInputSetState1_3(int arg0, void* arg1);

        [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputSetState")]
        private static extern unsafe int XInputSetState1_4(int arg0, void* arg1);

        [DllImport("xinput9_1_0.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputSetState")]
        private static extern unsafe int XInputSetState9_1_0(int arg0, void* arg1);

        private static unsafe int XInputSetStateShim(XInputVersion aVersion, int aUserIndex, Vibration aVibrationRef)
        {
            switch (aVersion)
            {
                case XInputVersion.xinput1_3:
                    return XInputSetState1_3(aUserIndex, &aVibrationRef);
                case XInputVersion.xinput1_4:
                    return XInputSetState1_4(aUserIndex, &aVibrationRef);
                case XInputVersion.xinput9_1_0:
                    return XInputSetState9_1_0(aUserIndex, &aVibrationRef);
            }
            return 0;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        delegate uint XInputSetStateDelegate(int aGamePadIndex, ref Vibration aVibrationRef);

        private static uint XInputSetStateHookFunc1_3(int aGamePadIndex, ref Vibration aVibrationRef)
        {
            return XInputSetStateHookFunc(XInputVersion.xinput1_3, aGamePadIndex, ref aVibrationRef);
        }

        private static uint XInputSetStateHookFunc1_4(int aGamePadIndex, ref Vibration aVibrationRef)
        {
            return XInputSetStateHookFunc(XInputVersion.xinput1_4, aGamePadIndex, ref aVibrationRef);
        }

        private static uint XInputSetStateHookFunc9_1_0(int aGamePadIndex, ref Vibration aVibrationRef)
        {
            return XInputSetStateHookFunc(XInputVersion.xinput9_1_0, aGamePadIndex, ref aVibrationRef);
        }

        private static uint XInputSetStateHookFunc(XInputVersion aVersion, int aGamePadIndex, ref Vibration aVibrationRef)
        {
            try
            {
                // Always send to the controller first, then do what we need to.
                if (_shouldPassthru)
                {
                    XInputSetStateShim(aVersion, aGamePadIndex, aVibrationRef);
                }

                GHRXInputModPayload This = _instance;
                // No reason to send duplicate packets.
                if (This._lastMessage.LeftMotorSpeed == aVibrationRef.LeftMotorSpeed &&
                    This._lastMessage.RightMotorSpeed == aVibrationRef.RightMotorSpeed)
                {
                    return 0;
                }
                This._lastMessage = new Vibration
                {
                    LeftMotorSpeed = aVibrationRef.LeftMotorSpeed,
                    RightMotorSpeed = aVibrationRef.RightMotorSpeed
                };

                lock (This._messageQueue)
                {
                    This._messageQueue.Enqueue(This._lastMessage);
                }
            }
            catch (Exception e)
            {
                _ex = e;
            }

            return 0;
        }
    }

}