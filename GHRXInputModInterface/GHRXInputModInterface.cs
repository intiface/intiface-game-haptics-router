using System;
using System.Collections.Generic;

namespace GHRXInputModInterface
{
    [Serializable]
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public struct Vibration
#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        public ushort LeftMotorSpeed;
        public ushort RightMotorSpeed;

        public static bool operator ==(Vibration c1, Vibration c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(Vibration c1, Vibration c2)
        {
            return !c1.Equals(c2);
        }
    }

    public class GHRXInputModInterface : MarshalByRefObject
    {
        // This will be used as a singleton in the IPC Server, and we should only ever have one process hooked 
        // with this interface. Just make the EventHandler static so we can attach as needed from anywhere.
        public static event EventHandler<Vibration> VibrationCommandReceived;
        public static event EventHandler<Exception> VibrationExceptionReceived;
        public static event EventHandler VibrationPingMessageReceived;
        public static event EventHandler<string> VibrationLogMessageReceived;
        public static event EventHandler VibrationExitReceived;
        private static bool _shouldStop;
        public static bool _shouldPassthru = true;

        public GHRXInputModInterface()
        {
            // Every time we create a new instance, reset the static stopping variable.
            _shouldStop = false;
            _shouldPassthru = true;
        }

        public bool ShouldPassthru()
        {
            return _shouldPassthru;
        }

        public void Exit()
        {
            VibrationExitReceived?.Invoke(this, EventArgs.Empty);
        }

        public static void Detach()
        {
            _shouldStop = true;
        }

        public void Report(Int32 aPid, Queue<Vibration> aCommands)
        {
            foreach (var command in aCommands)
            {
                VibrationCommandReceived?.Invoke(this, command);
            }
        }

        public void ReportError(Int32 aPid, Exception aEx)
        {
            VibrationExceptionReceived?.Invoke(this, aEx);
        }

        public bool Ping(Int32 aPid, string aMsg)
        {
            if (aMsg.Length > 0)
            {
                VibrationLogMessageReceived?.Invoke(this, aMsg);
                return !_shouldStop;
            }
            VibrationPingMessageReceived?.Invoke(this, EventArgs.Empty);
            return !_shouldStop;
        }
    }
}