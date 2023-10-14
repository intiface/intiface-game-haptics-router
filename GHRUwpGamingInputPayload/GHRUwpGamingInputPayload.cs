using EasyHook;
using System;
using System.Collections.Generic;
using System.Threading;
using GHRXInputModInterface;
using Windows.Gaming.Input;

namespace GHRUwpGamingInputPayload
{
    public class GHRUwpGamingInputPayload : IEntryPoint
    {
        private readonly GHRXInputModInterface.GHRXInputModInterface _interface;
        private readonly Queue<Vibration> _messageQueue = new Queue<Vibration>();
        private static Exception _ex;
        private Dictionary<int, Vibration> _lastMessages = new Dictionary<int, Vibration>();

        public GHRUwpGamingInputPayload(
            RemoteHooking.IContext aInContext,
            String aInChannelName)
        {
            _interface = RemoteHooking.IpcConnectClient<GHRXInputModInterface.GHRXInputModInterface>(aInChannelName);
        }

        public void Run(
            RemoteHooking.IContext aInContext,
            String aInArg1)
        {
            // Set hook for all threads.
            object myLock = new object();
            _interface.Ping(RemoteHooking.GetCurrentProcessId(), "Payload installed. Running payload loop.");

            try
            {
                while (_interface.Ping(RemoteHooking.GetCurrentProcessId(), ""))
                {
                    Thread.Sleep(16);
                    lock (myLock)
                    {
                        foreach (var gamepad in Gamepad.Gamepads)
                        {
                            _messageQueue.Enqueue(new Vibration
                            {
                                LeftMotorSpeed = (ushort)(gamepad.Vibration.LeftMotor * 65536),
                                RightMotorSpeed = (ushort)(gamepad.Vibration.RightMotor * 65536),
                                ControllerIndex = 0
                            });
                            _interface.Ping(RemoteHooking.GetCurrentProcessId(), $"Vibration: {gamepad.Vibration.LeftMotor} {gamepad.Vibration.RightMotor}");
                        }
                    }
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
    }
}