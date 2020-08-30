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
        private List<LocalHook> _xinputSetStateHookObj = new List<LocalHook>();
        private Vibration _lastMessage = new Vibration { LeftMotorSpeed = 65535, RightMotorSpeed = 65535 };
        private static bool _shouldPassthru = true;
        private readonly Queue<Vibration> _messageQueue = new Queue<Vibration>();
        private static Exception _ex;
        private static GHRXInputModPayload _instance;

        // Use 1_3 first, seems to be most popular. It seems that some games
        // link both xinput1_4 and xinput9_1_0, but seem to prefer xinput9_1_0?
        // Not quite sure about difference yet. 
        private enum XInputVersion
        {
            xinput1_4,
            xinput1_3,
            xinput9_1_0,
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

            foreach (var xinputVersion in Enum.GetValues(typeof(XInputVersion)))
            {
                try
                {
                    _interface.Ping(RemoteHooking.GetCurrentProcessId(), $"Trying to hook {xinputVersion}.dll");
                    LocalHook hook = null;
                    switch ((XInputVersion)xinputVersion)
                    {
                        case XInputVersion.xinput1_3:
                            hook = LocalHook.Create(
                                LocalHook.GetProcAddress($"{xinputVersion}.dll", "XInputSetState"),
                                new XInputSetStateDelegate(XInputSetStateHookFunc1_3),
                                null);
                            break;
                        case XInputVersion.xinput1_4:
                            hook = LocalHook.Create(
                                LocalHook.GetProcAddress($"{xinputVersion}.dll", "XInputSetState"),
                                new XInputSetStateDelegate(XInputSetStateHookFunc1_4),
                                null);
                            break;
                        case XInputVersion.xinput9_1_0:
                            hook = LocalHook.Create(
                                LocalHook.GetProcAddress($"{xinputVersion}.dll", "XInputSetState"),
                                new XInputSetStateDelegate(XInputSetStateHookFunc9_1_0),
                                null);
                            break;
                    };
                    if (hook == null)
                    {
                        continue;
                    }
                    hook.ThreadACL.SetExclusiveACL(new Int32[1]);
                    _xinputSetStateHookObj.Add(hook);
                    _interface.Ping(RemoteHooking.GetCurrentProcessId(), $"Hooked {xinputVersion}.dll");
                }
                catch
                {
                    // noop
                    _interface.Ping(RemoteHooking.GetCurrentProcessId(), $"Hooking {xinputVersion}.dll failed");
                }
            }
            if (_xinputSetStateHookObj == null)
            {
                _interface.ReportError(RemoteHooking.GetCurrentProcessId(), new Exception("No viable DLL to hook, payload exiting"));
                return;
            }
            // Set hook for all threads.

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

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        delegate uint XInputSetStateDelegate(int aGamePadIndex, ref Vibration aVibrationRef);

        // I was having problems figuring out how to de-ref aVibrationRef to do the void* conversion, so this is the long way.
        // This code rarely changes, so fuck it. It works.
        private static unsafe uint XInputSetStateHookFunc1_3(int aGamePadIndex, ref Vibration aVibrationRef)
        {
            if (_shouldPassthru)
            {
                RunXInputSetState1_3(aGamePadIndex, aVibrationRef);
            }
            return XInputSetStateHookFunc(aGamePadIndex, aVibrationRef);
        }

        private static unsafe int RunXInputSetState1_3(int aGamePadIndex, Vibration aVibration)
        {
            return XInputSetState1_3(aGamePadIndex, &aVibration);
        }

        private static unsafe uint XInputSetStateHookFunc1_4(int aGamePadIndex, ref Vibration aVibrationRef)
        {
            if (_shouldPassthru)
            {
                RunXInputSetState1_4(aGamePadIndex, aVibrationRef);
            }
            return XInputSetStateHookFunc(aGamePadIndex, aVibrationRef);
        }

        private static unsafe int RunXInputSetState1_4(int aGamePadIndex, Vibration aVibration)
        {
            return XInputSetState1_4(aGamePadIndex, &aVibration);
        }

        private static unsafe uint XInputSetStateHookFunc9_1_0(int aGamePadIndex, ref Vibration aVibrationRef)
        {
            if (_shouldPassthru)
            {
                RunXInputSetState9_1_0(aGamePadIndex, aVibrationRef);
            }
            return XInputSetStateHookFunc(aGamePadIndex, aVibrationRef);
        }

        private static unsafe int RunXInputSetState9_1_0(int aGamePadIndex, Vibration aVibration)
        {
            return XInputSetState1_3(aGamePadIndex, &aVibration);
        }

        private static uint XInputSetStateHookFunc(int aGamePadIndex, Vibration aVibrationRef)
        {
            try
            {
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