using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Harmony;
using IntifaceGameHapticsRouter;

namespace GHRUnityVRMod
{
    public class GHRUnityVRModFuncs
    {
#if DEBUG
        private static bool _useOutputFile = true;
#else
        private static bool _useOutputFile;
#endif
        private static StreamWriter _outFile;
        private static NamedPipeClientStream _stream;
        private static HarmonyInstance _harmony;

        static void WriteToStream(GHRProtocolMessageContainer aMsg)
        {
            aMsg.SendSerialized(_stream);
        }

        static void WriteLogToOutput(string aMsg)
        {
            _outFile?.WriteLine(aMsg);

            if (_stream == null)
            {
                return;
            }

            try
            {
                WriteToStream(new GHRProtocolMessageContainer
                {
                    Log = new Log(aMsg)
                });
            }
            catch (Exception ex)
            {

                _outFile?.WriteLine(ex);
                _stream.Close();
                _stream = null;
            }
        }

        // Harmony's AccessTools.TypeByName assumes we're in a system with
        // accurately linked libraries and filled-in types. Unfortunately, if
        // we're injecting in a already modded game, this may not be the case.
        // We need to check for reflection errors, and just skip types that
        // throw them while forming the LINQ queries.
        public static Type InternalTypeByName(string name)
        {
            var type = Type.GetType(name, false);
            if (type == null)
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(x =>
                    {
                        try
                        {
                            return x.GetTypes();
                        }
                        catch (ReflectionTypeLoadException)
                        {
                            return new Type[] { };
                        }
                    })
                    .FirstOrDefault(x => x.FullName == name);
            if (type == null)
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(x => {
                        try
                        {
                            return x.GetTypes();
                        }
                        catch (ReflectionTypeLoadException)
                        {
                            return new Type[] { };
                        }
                    })
                    .FirstOrDefault(x => x.Name == name);
            //if (type == null && Harmony.DEBUG)
            //    FileLog.Log("AccessTools.TypeByName: Could not find type named " + name);
            return type;
        }

        static void Attach(string aTypeName, string aMethodName, Type aPatchClass)
        {
            WriteLogToOutput($"Attaching to {aTypeName}.{aMethodName}");
            try
            {
                var originalType = InternalTypeByName(aTypeName);
                if (originalType == null)
                {
                    throw new Exception($"Can't find Type {aTypeName} to patch.");
                }

                var method = AccessTools.Method(originalType, aMethodName);
                if (method == null)
                {
                    throw new Exception($"Can't find method {aMethodName} to patch.");
                }

                var postfix = AccessTools.Method(aPatchClass, "PatchFunc");
                if (postfix == null)
                {
                    throw new Exception($"Can't find PatchFunc on type {aPatchClass.Name}.");
                }

                _harmony.Patch(method, null, new HarmonyMethod(postfix));
            }
            catch (ReflectionTypeLoadException ex)
            {
                WriteLogToOutput(ex.ToString());
                foreach (var lex in ex.LoaderExceptions)
                {
                    WriteLogToOutput(lex.ToString());
                }
            }
            catch (Exception ex)
            {
                WriteLogToOutput($"Not attaching to type {aTypeName}: {ex}");
            }
            WriteLogToOutput($"Attaching to {aTypeName}.{aMethodName} Successful");
        }

        static void Load()
        {
            if (_useOutputFile)
            {
                _outFile = new StreamWriter(Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%") + "\\ghrtest.txt");
                _outFile.AutoFlush = true;
                WriteLogToOutput("Created log file.");
            }

            try
            {
                _stream = new NamedPipeClientStream("GHRPipe");
                _stream.Connect();
            }
            catch (Exception ex)
            {
                WriteLogToOutput(ex.ToString());
            }

            try
            {
                _harmony = HarmonyInstance.Create("com.nonpolynomial.intiface_game_haptics_router");
            }
            catch (Exception ex)
            {
                WriteLogToOutput(ex.ToString());
                return;
            }
            
            WriteLogToOutput("Patching");
            Attach("CVRSystem", "TriggerHapticPulse", typeof(TriggerHapticPulse_Exfiltration_Patch));
            Attach("OVRPlugin", "SetControllerHaptics", typeof(TriggerHapticPulse_OculusClip_Exfiltration_Patch));
            Attach("OVRInput", "SetControllerVibration", typeof(TriggerHapticPulse_OculusInput_Exfiltration_Patch));
            WriteLogToOutput("Patching successful");
        }

        static class TriggerHapticPulse_Exfiltration_Patch
        {
            public static void PatchFunc(uint unControllerDeviceIndex, uint unAxisId, char usDurationMicroSec)
            {
                // TODO We need to create an instance of GetTrackedDeviceIndexForControllerRole and map Right/Left from ETrackedControllerRole to figure out correct hands here.
                var viveMsg = new UnityXRViveHaptics
                {
                    Duration = usDurationMicroSec, Hand = HandSpec.LEFT
                };
                WriteToStream(new GHRProtocolMessageContainer
                {
                    UnityXRViveHaptics = viveMsg
                });
            }
        }

        public struct HapticsBuffer
        {
            public IntPtr Samples;

            public int SamplesCount;
        }

        static class TriggerHapticPulse_OculusClip_Exfiltration_Patch
        {
            static void PatchFunc(uint controllerMask, HapticsBuffer hapticsBuffer)
            {
                // TODO This won't work if SampleSize != 1. Check Sample Size somewhere.
                byte[] clipBuffer = new byte[hapticsBuffer.SamplesCount];
                Marshal.Copy(hapticsBuffer.Samples, clipBuffer, 0, hapticsBuffer.SamplesCount);
                // If the buffer is nothing but 0s, we don't care about it.
                if (clipBuffer.Max() != 0)
                {
                    WriteToStream(new GHRProtocolMessageContainer { UnityXROculusClipHaptics = new UnityXROculusClipHaptics(HandSpec.LEFT, clipBuffer) });
                }
            }
        }

        static class TriggerHapticPulse_OculusInput_Exfiltration_Patch
        {
            private static float aLastFrequency;
            private static float aLastAmplitude;

            static void PatchFunc(float frequency, float amplitude, uint controllerMask)
            {
                if (aLastFrequency == frequency && aLastAmplitude == amplitude)
                {
                    return;
                }

                aLastFrequency = frequency;
                aLastAmplitude = amplitude;
                WriteToStream(new GHRProtocolMessageContainer { UnityXROculusInputHaptics = new UnityXROculusInputHaptics(HandSpec.LEFT, frequency, amplitude) });
            }
        }
    }
}
