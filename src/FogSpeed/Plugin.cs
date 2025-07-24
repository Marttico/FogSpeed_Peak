using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration; 
using HarmonyLib;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;


[BepInPlugin("marttico.fogspeed", "FogSpeed", "0.1.3")]
public class FogSpeedPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    private ConfigEntry<float> defaultFogSpeed;

    private static ConfigEntry<bool> hotkeysEnabled;

    private ConfigEntry<KeyCode> increaseFogSpdKey;
    private ConfigEntry<KeyCode> pauseFogKey;
    private ConfigEntry<KeyCode> decreaseFogSpdKey;
    private ConfigEntry<KeyCode> negateFogSpdKey;
    private static ConfigEntry<KeyCode> advanceFogKey;

    private static float fogSpeed = 0.3f;
    private static bool isPaused = false;

    public static bool fogChangeQueued = false;
    
    private PhotonView photonView;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo("FogSpeedPlugin Awake");

        photonView = GetComponent<PhotonView>();

        // Load Config
        defaultFogSpeed = Config.Bind("General","DefaultFogSpeed", 0.3f,"Default speed multiplier for the fog effect.");
        advanceFogKey = Config.Bind("General","AdvanceFogKey",KeyCode.I,"Default key to advance fog.");
        decreaseFogSpdKey = Config.Bind("General", "DecreaseFogSpeedKey",KeyCode.J,"Default key to decrease fog speed multiplier.");
        pauseFogKey = Config.Bind("General","PauseFogKey",KeyCode.K,"Default key to pause or resume fog progression.");
        increaseFogSpdKey = Config.Bind("General","IncreaseFogSpeedKey",KeyCode.L,"Default key to increase fog speed multiplier.");
        negateFogSpdKey = Config.Bind("General","NegateFogSpeedKey",KeyCode.O,"Default key to negate fog speed.");
        hotkeysEnabled = Config.Bind("General","EnableHotkeys",true,"Check this if you want to enable hotkeys");
        

        fogSpeed = defaultFogSpeed.Value;

        // Patch all methods.
        var harmony = new Harmony("marttico.fogspeed");
        harmony.PatchAll();
        
        Log.LogInfo("FogSpeedPlugin Loaded");
    }

    private void Update(){
        if (Input.GetKeyDown(decreaseFogSpdKey.Value) && hotkeysEnabled.Value){
            FogSpeedPlugin.fogSpeed /= 1.5f;
            FogSpeedPlugin.Log.LogInfo($"Fog speed decreased to {fogSpeed}");
            fogChangeQueued = true;
        }

        if (Input.GetKeyDown(pauseFogKey.Value) && hotkeysEnabled.Value){
            FogSpeedPlugin.isPaused = !FogSpeedPlugin.isPaused;
            FogSpeedPlugin.Log.LogInfo("Fog paused or resumed");
            fogChangeQueued = true;
        }

        if (Input.GetKeyDown(increaseFogSpdKey.Value) && hotkeysEnabled.Value){
            FogSpeedPlugin.fogSpeed *= 1.5f;
            FogSpeedPlugin.Log.LogInfo($"Fog speed increased to {fogSpeed}");
            fogChangeQueued = true;
        }

        if (Input.GetKeyDown(negateFogSpdKey.Value) && hotkeysEnabled.Value){
            FogSpeedPlugin.fogSpeed *= -1f;
            FogSpeedPlugin.Log.LogInfo($"Fog speed inverted to {fogSpeed}");
            fogChangeQueued = true;
        }

        FogSpeedPlugin.fogSpeed = Mathf.Max(-50f,FogSpeedPlugin.fogSpeed);
        FogSpeedPlugin.fogSpeed = Mathf.Min(50f,FogSpeedPlugin.fogSpeed);
    }
    
    [HarmonyPatch(typeof(OrbFogHandler), "Move")]
    static class OrbFogSpeedHandler_Patch {
        static void Prefix(OrbFogHandler __instance){
            if(FogSpeedPlugin.isPaused){
                __instance.speed = 0;
            }else{
                __instance.speed = FogSpeedPlugin.fogSpeed;
            }
        }
    }
    
    [HarmonyPatch(typeof(OrbFogHandler), "Update")]
    static class OrbFogHandlerStart_Patch {
        static void Prefix(OrbFogHandler __instance){
            if (Input.GetKeyDown(advanceFogKey.Value) && !__instance.isMoving && PhotonNetwork.IsMasterClient && FogSpeedPlugin.hotkeysEnabled.Value){
                __instance.photonView.RPC("StartMovingRPC", RpcTarget.All);
                FogSpeedPlugin.Log.LogInfo("Fog has been advanced");
            }
        }
    }

    [HarmonyPatch(typeof(OrbFogHandler), "Sync")]
    static class OrbFogHandlerSync_Patch {
        static void Prefix(OrbFogHandler __instance){
            if(FogSpeedPlugin.fogChangeQueued){
                FogSpeedPlugin.Log.LogInfo("RPCA SYNCING");
                FogSpeedPlugin.fogChangeQueued = false;
                __instance.photonView.RPC("RPCA_SyncFog", RpcTarget.Others, __instance.currentSize, __instance.isMoving);
            }
        }
    }

    [HarmonyPatch(typeof(VersionString), "Update")]
    static class VersionStringPatch{
        static void Postfix(VersionString __instance){
            if (PhotonNetwork.InRoom && UnityEngine.Object.FindObjectOfType<OrbFogHandler>())
			{
                string text;
                if (FogSpeedPlugin.isPaused){
                    text = $"\nFog speed paused";
                }else{
                    text = $"\nFog speed: {FogSpeedPlugin.fogSpeed:F2}";
                }
                TextMeshProUGUI text2 = __instance.m_text;
                ((TMP_Text)text2).text = ((TMP_Text)text2).text + text;
            }
        }
    }
    
}
