using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace MoreLasers
{
    [BepInPlugin("com.Spiny.MoreLasers", "Moar Lasers", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<int> MaxTargetsConfig;
        internal static ConfigEntry<bool> StrictMode;

        private void Awake()
        {
            Log = Logger;

            MaxTargetsConfig = Config.Bind(
                "Laser Designator",
                "Increased_Targets",
                3,
                "Number of additional targets (added to base game limit, so put it in as zero if you dont want to adjust this.)"
            );

            StrictMode = Config.Bind(
                "Multiplayer",
                "Strict_Mode",
                false,
                "If true, only allow multiplayer with other modded players via version match. If false, disable mod when playing in multiplayer."
            );

            Logger.LogInfo("Moar Lasers Loading.");

            Harmony harmony = new Harmony("com.Spiny.MoreLasers");
            harmony.PatchAll();
            harmony.PatchAll();

            Logger.LogInfo("Moar Lasers loaded.");
        }

        internal static bool IsMultiplayer()
        {
            try
            {
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
                                            //Log.LogInfo("Hosting multiplayer with remote players");
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

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
                                        //Log.LogInfo("Connected to remote server");
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

            return false;
        }
    }

    // Only modify version if user wants to require all players have the mod
    // Credit to Nikkorap for making the original version this is derivative of.
    [HarmonyPatch(typeof(Application), nameof(Application.version), MethodType.Getter)]
    internal static class VersionGetterPatch
    {
        static void Postfix(ref string __result)
        {
            if (Plugin.StrictMode.Value)
            {
                __result += "_MoreLasers-v1.0.0";
                Plugin.Log.LogInfo($"Game version modified to: {__result} (requires all players have mod)");
            }
        }
    }

    [HarmonyPatch(typeof(LaserDesignator), "Awake")]
    internal class LaserDesignatorAwakePatch
    {
        static void Postfix(LaserDesignator __instance)
        {
            if (Plugin.IsMultiplayer())
            {
                Plugin.Log.LogInfo("Laser Mod Disabled due to MP with Vanilla.");
                return;
            }

            int originalMax = Traverse.Create(__instance).Field<int>("maxTargets").Value;
            int newMax = Plugin.MaxTargetsConfig.Value + originalMax;

            Traverse.Create(__instance).Field("maxTargets").SetValue(newMax);

            //Plugin.Log.LogInfo($"Laser targets increased: {originalMax} → {newMax}");
        }
    }
}