using System;
using TinyJson;

namespace IntifaceGameVibrationRouter
{
    public class GVRProtocolMessage
    {
        public string Type;

        public GVRProtocolMessage()
        {
            Type = GetType().Name;
        }

        public string Serialize()
        {
            return this.ToJson();
        }

        public static T Deserialize<T>(string aMsg)
        {
            return aMsg.FromJson<T>();
        }

        public static GVRProtocolMessage Deserialize(string aMsg)
        {
            var gvrmsg = aMsg.FromJson<GVRProtocolMessage>();
            switch (gvrmsg.Type)
            {
                case "Ping":
                    return aMsg.FromJson<Ping>();
                case "Log":
                    return aMsg.FromJson<Log>();
                case "XInputHaptics":
                    return aMsg.FromJson<XInputHaptics>();
                case "UnityXRViveHaptics":
                    return aMsg.FromJson<UnityXRViveHaptics>();
                case "UnityXROculusInputHaptics":
                    return aMsg.FromJson<UnityXROculusInputHaptics>();
                case "UnityXROculusClipHaptics":
                default:
                    throw new Exception($"Cannot parse message of type { gvrmsg.Type }");
            }

        }
    }

    public class Ping : GVRProtocolMessage
    {
    }

    public class Log : GVRProtocolMessage
    {
        public string Message;

        public Log(string aMsg)
        {
            Message = aMsg;
        }
    }

    public enum HandSpec
    {
        LEFT,
        RIGHT
    }

    public class XInputHaptics : GVRProtocolMessage
    {
        public uint LeftMotor;
        public uint RightMotor;
    }

    public class UnityXRViveHaptics : GVRProtocolMessage
    {
        public HandSpec Hand;
        public uint Duration;

        public UnityXRViveHaptics(HandSpec aHand, uint aDuration)
        {
            Hand = aHand;
            Duration = aDuration;
        }
    }

    public class UnityXROculusInputHaptics : GVRProtocolMessage
    {
        public HandSpec Hand;
        public float Frequency;
        public float Amplitude;

        public UnityXROculusInputHaptics(HandSpec aHand, float aFrequency, float aAmplitude)
        {
            Hand = aHand;
            Frequency = aFrequency;
            Amplitude = aAmplitude;
        }
    }

    public class UnityXROculusClipHaptics : GVRProtocolMessage
    {

    }
}
