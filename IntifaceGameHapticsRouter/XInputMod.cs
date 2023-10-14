using System;
using GHRXInputModInterface;

namespace IntifaceGameHapticsRouter
{
    class XInputMod : EasyHookMod
    {

        private Vibration _lastVibration = new Vibration();

        public override bool CanUseMod(IntPtr handle)
        {
            return CanUseMod(handle, "xinput");
        }

        public override void Attach(int aProcessId)
        {
           Attach<GHRXInputModPayload.GHRXInputModPayload>(aProcessId, "GHRXInputModPayload.dll");
        }

        public XInputMod()
        {
            GHRXInputModInterface.GHRXInputModInterface.VibrationCommandReceived += OnVibrationCommand;
            GHRXInputModInterface.GHRXInputModInterface.VibrationPingMessageReceived += base.OnVibrationPingMessage;
            GHRXInputModInterface.GHRXInputModInterface.VibrationExceptionReceived += base.OnVibrationException;
            GHRXInputModInterface.GHRXInputModInterface.VibrationExitReceived += base.OnVibrationExit;
            GHRXInputModInterface.GHRXInputModInterface.VibrationLogMessageReceived += base.OnVibrationLogMessage;
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
