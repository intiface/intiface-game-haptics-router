using System;
using GHRXInputModInterface;

namespace IntifaceGameHapticsRouter
{
    class UWPInputMod : EasyHookMod
    {
        private Vibration _lastVibration = new Vibration();

        /// <summary>
        /// Denotes whether we can use XInput mods with this process.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        /// <remarks>This is basically a copy of SharpMonoInjector.ProcessUtils.GetMonoModule, just shuffled a bit for checking for xinput.</remarks>
        public override bool CanUseMod(IntPtr handle)
        {
            return CanUseMod(handle, "Windows.Gaming.Input");
        }

        public UWPInputMod()
        {
            GHRXInputModInterface.GHRXInputModInterface.VibrationCommandReceived += OnVibrationCommand;
            GHRXInputModInterface.GHRXInputModInterface.VibrationPingMessageReceived += base.OnVibrationPingMessage;
            GHRXInputModInterface.GHRXInputModInterface.VibrationExceptionReceived += base.OnVibrationException;
            GHRXInputModInterface.GHRXInputModInterface.VibrationExitReceived += base.OnVibrationExit;
            GHRXInputModInterface.GHRXInputModInterface.VibrationLogMessageReceived += base.OnVibrationLogMessage;
        }

        public override void Attach(int aProcessId)
        {
            Attach<GHRUwpGamingInputPayload.GHRUwpGamingInputPayload>(aProcessId, "GHRUwpGamingInputPayload.dll");
        }

        protected override void OnVibrationCommand(object aObj, Vibration aVibration)
        {
            if (aVibration == _lastVibration)
            {
                return;
            }

            _lastVibration = aVibration;
            MessageReceivedHandler?.Invoke(this, new GHRProtocolMessageContainer { XInputHaptics = new XInputHaptics(aVibration.LeftMotorSpeed, aVibration.RightMotorSpeed, aVibration.ControllerIndex)});
        }

    }
}
