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
        internal static ManualLogSource Log;                    //Da loggerrrrrrrrrrrr
        internal static ConfigEntry<int> IncreasedTargets;      //Stores the number of targets to increase.
        internal static ConfigEntry<MpBlocker.MultiplayerMode> MpMode;    //Enum to store if in MpDisabled or RestrictedMM.

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
                "MultiplayerMode",
                MpBlocker.MultiplayerMode.MpDisabled,
                "MpDisabled: mod only active in singleplayer. RestrictedMM: mod active in MP with version matching."
            );

            MpBlocker.MpBlocker.SetEnum(MpMode.Value);
            MpMode.SettingChanged += (sender, args) => MpBlocker.MpBlocker.SetEnum(MpMode.Value);

            Logger.LogInfo("Moar Lasers Loading.");

            Harmony harmony = new Harmony("com.Spiny.MoreLasers");
            harmony.PatchAll();

            Logger.LogInfo("Moar Lasers loaded.");
        }        
    }

    // Version modifier for restrictedMM. Credit to Nikkoraps for developing the original version this is based off of.
    // Changes the version of the game so that you and others can play with the mod still active.
   

    //Patches in the laser designator.
    [HarmonyPatch(typeof(LaserDesignator), "Awake")]
    internal class LaserDesignatorAwakePatch
    {
        static void Postfix(LaserDesignator __instance)
        {
            if (MpBlocker.MpBlocker.IsMultiplayer())
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
}