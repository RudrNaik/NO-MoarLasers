using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace MoreLasers
{
    public enum MultiplayerMode
    {
        MpDisabled,     // Mod disabled in all multiplayer (can join anyone) but accessible in singleplayer.
        RestrictedMM    // Mod is enalbled in MP with others, however requires a version match.
    }

    [BepInPlugin("com.Spiny.MoreLasers", "Moar Lasers", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;                    //Da loggeeeeer
        internal static ConfigEntry<int> IncreasedTargets;      //Stores the number of targets to increase.
        internal static ConfigEntry<MultiplayerMode> MpMode;    //Enum to store if in MpDisabled or RestrictedMM.

        private static bool? _cachedIsMultiplayer = null;       //bool to check if we have checked for MP already in the scene we've loaded. Upon scene change, this is reset.
        internal static bool _hasLoggedMpState = false;         //bool to check if we have a logged MP state. Mainly to communicate with the patcher.

        private void Awake()
        {
            Log = Logger;

            IncreasedTargets = Config.Bind(
                "Laser Designator",
                "Increased_Targets",
                3,
                "Number of additional targets (added to base game limit, 0 = no change)"
            );

            MpMode = Config.Bind(
                "Multiplayer",
                "MpConfig",
                MultiplayerMode.MpDisabled,
                "MpDisabled: Mod disabled in all multiplayer (can join anyone) but accessible in singleplayer. RestrictedMM: Mod is enalbled in MP with others, however requires a version match, and means both players need to have the mod."
            );

            Logger.LogInfo("Moar Lasers Loading.");

            Harmony harmony = new Harmony("com.Spiny.MoreLasers");
            harmony.PatchAll();

            Logger.LogInfo("Moar Lasers loaded.");
        }

        internal static void ResetMultiplayerCache()
        {
            _cachedIsMultiplayer = null;
            _hasLoggedMpState = false;
        }

        internal static bool IsMultiplayer()
        {
            // If RestrictedMM mode, never consider it "multiplayer" as we have a different version entirely.
            if (MpMode.Value == MultiplayerMode.RestrictedMM)
            {
                return false;
            }

            if (_cachedIsMultiplayer.HasValue) { 
                return _cachedIsMultiplayer.Value;
            }

            try
            {
                // Check if hosting with players (playing with others)
                Type serverType = AccessTools.TypeByName("Mirage.NetworkServer");
                if (serverType != null)
                {
                    var serverInstances = UnityEngine.Object.FindObjectsOfType(serverType);

                    if (serverInstances.Length > 0)
                    {
                        object server = serverInstances[0];
                        PropertyInfo activeProperty = serverType.GetProperty("Active");

                        if (activeProperty != null)
                        {
                            object activeValue = activeProperty.GetValue(server);

                            if (activeValue != null && (bool)activeValue)
                            {
                                PropertyInfo connectionsProperty = serverType.GetProperty("connections");
                                if (connectionsProperty != null)
                                {
                                    object connections = connectionsProperty.GetValue(server);
                                    if (connections is System.Collections.ICollection collection)
                                    {
                                        if (collection.Count > 1)
                                        {
                                            // Hosting with remote players
                                            _cachedIsMultiplayer = true;
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Check if connected as client to remote server (playing with others).
                Type clientType = AccessTools.TypeByName("Mirage.NetworkClient");
                if (clientType != null)
                {
                    var clientInstances = UnityEngine.Object.FindObjectsOfType(clientType);

                    if (clientInstances.Length > 0)
                    {
                        object client = clientInstances[0];
                        PropertyInfo activeProperty = clientType.GetProperty("Active");
                        PropertyInfo isLocalClientProperty = clientType.GetProperty("IsLocalClient");

                        if (activeProperty != null)
                        {
                            object activeValue = activeProperty.GetValue(client);

                            if (activeValue != null && (bool)activeValue)
                            {
                                if (isLocalClientProperty != null)
                                {
                                    object isLocalValue = isLocalClientProperty.GetValue(client);

                                    if (isLocalValue != null && !(bool)isLocalValue)
                                    {
                                        // Connected to remote server
                                        _cachedIsMultiplayer = true;
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Error checking multiplayer state: {ex.Message}");
            }

            // if all else is false, player is in singleplayer.
            _cachedIsMultiplayer = false;
            return false;
        }
    }

    // Version modifier for restrictedMM. Credit to Nikkoraps for developing the original version this is based off of.
    // Changes the version of the game so that you and others can play with the mod still active.
    [HarmonyPatch(typeof(Application), nameof(Application.version), MethodType.Getter)]
    internal static class VersionGetterPatch
    {
        static void Postfix(ref string __result)
        {
            if (Plugin.MpMode.Value == MultiplayerMode.RestrictedMM)
            {
                __result += "_MoreLasers-v1.0.0";
            }
        }
    }

    //Patches in the laser designator.
    [HarmonyPatch(typeof(LaserDesignator), "Awake")]
    internal class LaserDesignatorAwakePatch
    {
        static void Postfix(LaserDesignator __instance)
        {
            if (Plugin.IsMultiplayer())
            {
                if (!Plugin._hasLoggedMpState)
                {
                    Plugin.Log.LogInfo($"In multiplayer with others, MoarLasers disabled. (mode: {Plugin.MpMode.Value})");
                    Plugin._hasLoggedMpState = true;
                }
                return;
            }

            int originalMax = Traverse.Create(__instance).Field<int>("maxTargets").Value;
            int newMax = Plugin.IncreasedTargets.Value + originalMax;

            Traverse.Create(__instance).Field("maxTargets").SetValue(newMax);

            if (!Plugin._hasLoggedMpState)
            {
                Plugin.Log.LogInfo($"Laser targets increased by {Plugin.IncreasedTargets.Value} (mode: {Plugin.MpMode.Value})");
                Plugin._hasLoggedMpState = true;
            }
        }
    }

    //When loading a new scene, reset the cache so we can make the first check and not spam the logs with 13 billion logs.
    [HarmonyPatch(typeof(UnityEngine.SceneManagement.SceneManager), "Internal_SceneLoaded")]
    internal static class SceneLoadPatch
    {
        static void Postfix()
        {
            Plugin.ResetMultiplayerCache();
        }
    }
}