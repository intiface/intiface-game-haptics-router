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
        private static bool _useOutputFile = true;
        private static StreamWriter _outFile;
        private static NamedPipeClientStream _stream;

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

        static void Load()
        {
            if (_useOutputFile)
            {
                _outFile = new StreamWriter(Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%") + "\\bstest.txt");
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


            HarmonyInstance harmony;
            try
            {
                harmony = HarmonyInstance.Create("com.nonpolynomial.buttsaber");
            }
            catch (Exception ex)
            {
                WriteLogToOutput(ex.ToString());
                return;
            }
            
            WriteLogToOutput("Patching assemblies");
            try
            {
                try
                {
                    var original = InternalTypeByName("CVRSystem");
                    if (original == null)
                    {
                        throw new Exception("Can't find CVRSystem!");
                    }

                    var method = AccessTools.Method(original, "TriggerHapticPulse");
                    if (method == null)
                    {
                        throw new Exception("Can't find TriggerHapticPulse!");
                    }

                    var postfix = AccessTools.Method(typeof(TriggerHapticPulse_Exfiltration_Patch), "ValvePostfix");
                    if (postfix == null)
                    {
                        throw new Exception("Can't find ValvePostfix!");
                    }

                    harmony.Patch(method, null, new HarmonyMethod(postfix));
                }
                catch (Exception ex)
                {
                    WriteLogToOutput($"Not attaching to CVRSystem: {ex}");
                }

                try
                {
                    var original2 = InternalTypeByName("OVRPlugin");
                    if (original2 == null)
                    {
                        throw new Exception("Can't find OVRPlugin!");
                    }
                    var method2 = original2.GetMethod("SetControllerHaptics");
                    if (method2 == null)
                    {
                        throw new Exception("Can't find SetControllerHaptics!");
                    }

                    var postfix2 = AccessTools.Method(typeof(TriggerHapticPulse_Oculus_Exfiltration_Patch), "OculusPostfix");
                    if (postfix2 == null)
                    {
                        throw new Exception("Can't find OculusPostfix!");
                    }

                    harmony.Patch(method2, null, new HarmonyMethod(postfix2));
                }
                catch (Exception ex)
                {
                    WriteLogToOutput($"Not attaching to OVRPlugin: {ex}");
                }
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
                WriteLogToOutput(ex.ToString());
                return;
            }

            WriteLogToOutput("Patched assemblies");
        }

        static class TriggerHapticPulse_Exfiltration_Patch
        {
            public static void ValvePostfix(uint unControllerDeviceIndex, uint unAxisId, char usDurationMicroSec)
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

        static class TriggerHapticPulse_Oculus_Exfiltration_Patch
        {
            static void OculusPostfix(uint controllerMask, HapticsBuffer hapticsBuffer)
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

        // Output a string of "[l|r],[number]\n" over IPC. No reason to deal with
        // something like pbufs for this.
        //
        // [number] for BeatSaber will be a float between 0-1. We'll use the Vive
        // interpretation of this, which is a multiplier against 4000 microseconds.
        // See https://github.com/ValveSoftware/openvr/wiki/IVRSystem::TriggerHapticPulse
        // for more info. This is weird.
        //
        // It might be worth trying to hook this at the OVR/Oculus API level at some
        // point to make this a more generic solution, but that will mean translating
        // Oculus haptic clips for games that haven't moved to the new API and I don't
        // wanna.
        /*
        [HarmonyPatch(typeof(VRPlatformHelper), "TriggerHapticPulse")]
        static class TriggerHapticPulse_Exfiltration_Patch
        {
            static void Postfix(XRNode node, float strength = 1f)
            {
                WriteToOutput($"BS: Writing ${strength} to ${node}");
                var hand = node == XRNode.LeftHand ? "l" : "r";
                var msg = $"{hand},{strength.ToString()}\n";
                var b = Encoding.ASCII.GetBytes(msg);
                _stream.Write(b, 0, b.Length);
            }
        }
        */
        /* [HarmonyPatch(typeof(OVRPlugin), "SetControllerHaptics")]
        static class TriggerHapticPulse_Oculus_Exfiltration_Patch
        {
            static void Postfix(uint controllerMask, OVRPlugin.HapticsBuffer hapticsBuffer)
            {
                WriteToOutput($"OCULUS: Writing ${hapticsBuffer.SamplesCount} samples to ${controllerMask}");
            }
        }

        
        [HarmonyPatch(typeof(Valve.VR.CVRSystem), "TriggerHapticPulse")]
        static class TriggerHapticPulse_Valve_Exfiltration_Patch
        {
            static void Postfix(uint unControllerDeviceIndex, uint unAxisId, char usDurationMicroSec)
            {
                WriteToOutput($"VALVE: Writing ${usDurationMicroSec} duration to ${unControllerDeviceIndex}");
                var hand = node == XRNode.LeftHand ? "l" : "r";
                var msg = $"{hand},{strength.ToString()}\n";
                var b = Encoding.ASCII.GetBytes(msg);
                _stream.Write(b, 0, b.Length);
            }
        }
        */
    }
}
