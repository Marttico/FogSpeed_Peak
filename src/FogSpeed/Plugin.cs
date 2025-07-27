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
using UnityEngine.SceneManagement;

[BepInPlugin("marttico.fogspeed", "FogSpeed", "0.2.1")]
public class FogSpeedPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    private ConfigEntry<float> defaultFogSpeed;

    private static ConfigEntry<bool> hotkeysEnabled;

    private ConfigEntry<KeyCode> increaseFogSpdKey;
    private ConfigEntry<KeyCode> pauseFogKey;
    private ConfigEntry<KeyCode> decreaseFogSpdKey;
    private ConfigEntry<KeyCode> negateFogSpdKey;
    private ConfigEntry<KeyCode> advanceFogKey;
    private ConfigEntry<KeyCode> resetFogSpdKey;

    private float fogSpeed = 0.3f;
    private bool isPaused = false;
    
    private PhotonView photonView; 
    private FogSpeedSyncHandler syncHandler;

    private float retrycounter;

    public static OrbFogHandler? orbfoghandler;

    private void registerPhotonView()
    {
        photonView = orbfoghandler.GetComponent<PhotonView>();
        if (photonView == null)
        {
            Log.LogError("PhotonView is null on OrbFogHandler!");
        }
        else
        {
            syncHandler = orbfoghandler.gameObject.AddComponent<FogSpeedSyncHandler>();
            syncHandler.fogHandler = orbfoghandler;

            PhotonNetwork.AddCallbackTarget(this);
        }
    }

    private void Awake()
    {
        Log = Logger;

        // Load Config
        defaultFogSpeed = Config.Bind("General", "DefaultFogSpeed", 0.3f, "Default speed multiplier for the fog effect.");
        advanceFogKey = Config.Bind("General", "AdvanceFogKey", KeyCode.I, "Default key to advance fog.");
        decreaseFogSpdKey = Config.Bind("General", "DecreaseFogSpeedKey", KeyCode.J, "Default key to decrease fog speed multiplier.");
        pauseFogKey = Config.Bind("General", "PauseFogKey", KeyCode.K, "Default key to pause or resume fog progression.");
        increaseFogSpdKey = Config.Bind("General", "IncreaseFogSpeedKey", KeyCode.L, "Default key to increase fog speed multiplier.");
        negateFogSpdKey = Config.Bind("General", "NegateFogSpeedKey", KeyCode.O, "Default key to negate fog speed.");
        resetFogSpdKey = Config.Bind("General", "ResetFogSpeedKey", KeyCode.U, "Reset fog speed.");
        hotkeysEnabled = Config.Bind("General", "EnableHotkeys", true, "Check this if you want to enable hotkeys");

        fogSpeed = defaultFogSpeed.Value;

        // Patch all methods.
        var harmony = new Harmony("marttico.fogspeed");
        harmony.PatchAll();

        Log.LogInfo("FogSpeedPlugin Loaded.");
    }

    private void sendFogSpeed()
    {
        fogSpeed = Mathf.Max(-50f, fogSpeed);
        fogSpeed = Mathf.Min(50f, fogSpeed);

        if (isPaused)
        {
            photonView.RPC("RPCA_SyncFogSpeed", RpcTarget.All, 0f);
        }
        else
        {
            photonView.RPC("RPCA_SyncFogSpeed", RpcTarget.All, fogSpeed);
        }
    }

    private void Update()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            if (Input.GetKeyDown(decreaseFogSpdKey.Value) && hotkeysEnabled.Value)
            {
                fogSpeed /= 1.5f;
                Log.LogInfo($"Fog speed decreased to {fogSpeed}");
                sendFogSpeed();
            }

            if (Input.GetKeyDown(pauseFogKey.Value) && hotkeysEnabled.Value)
            {
                isPaused = !isPaused;
                Log.LogInfo("Fog paused or resumed");
                sendFogSpeed();
            }

            if (Input.GetKeyDown(increaseFogSpdKey.Value) && hotkeysEnabled.Value)
            {
                fogSpeed *= 1.5f;
                Log.LogInfo($"Fog speed increased to {fogSpeed}");
                sendFogSpeed();
            }

            if (Input.GetKeyDown(negateFogSpdKey.Value) && hotkeysEnabled.Value)
            {
                fogSpeed *= -1f;
                Log.LogInfo($"Fog speed inverted to {fogSpeed}");
                sendFogSpeed();
            }

            if (Input.GetKeyDown(resetFogSpdKey.Value) && hotkeysEnabled.Value)
            {
                fogSpeed = 0.3f;
                Log.LogInfo($"Fog speed reset to {fogSpeed}");
                sendFogSpeed();
            }

            if (Input.GetKeyDown(advanceFogKey.Value) && !orbfoghandler.isMoving && PhotonNetwork.IsMasterClient && hotkeysEnabled.Value)
            {
                photonView.RPC("StartMovingRPC", RpcTarget.All);
                Log.LogInfo("Fog has been advanced");
                sendFogSpeed();
            }
        }

        retrycounter += Time.deltaTime;
        if (orbfoghandler == null && retrycounter > 5f)
        {
            retrycounter = 0;
            orbfoghandler = Object.FindAnyObjectByType<OrbFogHandler>();
            if (orbfoghandler != null)
            {
                registerPhotonView();
            }
            else
            {
                Log.LogInfo("OrbFogHandler not found, retrying in 5 seconds!");
            }
        }
    }


    [HarmonyPatch(typeof(VersionString), "Update")]
    static class VersionStringPatch{
        static void Postfix(VersionString __instance){
            if (PhotonNetwork.InRoom && FogSpeedPlugin.orbfoghandler != null)
			{
                float speed = FogSpeedPlugin.orbfoghandler.speed;
                string text;
                if (speed == 0){
                    text = $"\nFog speed paused";
                }else{
                    text = $"\nFog speed: {speed:F2}";
                }
                TextMeshProUGUI text2 = __instance.m_text;
                ((TMP_Text)text2).text = ((TMP_Text)text2).text + text;
            }
        }
    }
}

public class FogSpeedSyncHandler : MonoBehaviourPun
{
    public OrbFogHandler fogHandler;

    [PunRPC]
    public void RPCA_SyncFogSpeed(float spd)
    {
        if (fogHandler != null)
        {
            FogSpeedPlugin.Log.LogInfo($"[RPC] Synced fog speed: {spd}");
            fogHandler.speed = spd;
        }
        else
        {
            FogSpeedPlugin.Log.LogWarning("FogHandler was null during RPC.");
        }
    }
}