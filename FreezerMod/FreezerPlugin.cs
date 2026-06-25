using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ClassicUs.Manactor;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace ClassicUs.FreezerMod
{
    [BepInPlugin(Guid, "Classic Us Freezer", "1.0.0")]
    [BepInDependency(ManactorPlugin.Guid)]
    public class FreezerPlugin : BasePlugin
    {
        public const string Guid = "classicus.freezer";
        public const string Version = "1.0.0";
        public const string RoleModName = "ClassicUsFreezer";

        public static string FreezerRoleName = "Freezer";
        public const string RpcSyncSettingsKey = "classicus.freezer.SyncSettings";
        public const string RequestFreezeKey = "classicus.freezer.RequestFreeze";
        public const string BroadcastFreezeKey = "classicus.freezer.BroadcastFreeze";

        public static ManualLogSource Log;

        public static ConfigEntry<bool> CfgEnabled;
        public static ConfigEntry<int> CfgCount;
        public static ConfigEntry<float> CfgAbilityCooldown;
        public static ConfigEntry<float> CfgFreezeDuration;
        public static ConfigEntry<float> CfgRoleChance;

        public static bool ActiveEnabled;
        public static int ActiveCount = 1;
        public static float ActiveAbilityCooldown = 30f;
        public static float ActiveFreezeDuration = 10f;
        public static float ActiveRoleChance = 100f;

        public override void Load()
        {
            Log = base.Log;

            CfgEnabled = Config.Bind("Game", "EnableFreezer", true,
                "Enables the Freezer role: an impostor that can freeze every player for a few seconds.");
            CfgCount = Config.Bind("Game", "FreezerCount", 1,
                new ConfigDescription("How many Freezers to assign per match.",
                    new AcceptableValueRange<int>(0, 3)));
            CfgAbilityCooldown = Config.Bind("Game", "FreezerAbilityCooldown", 30f,
                new ConfigDescription("Cooldown of the freeze button (seconds).",
                    new AcceptableValueRange<float>(5f, 120f)));
            CfgFreezeDuration = Config.Bind("Game", "FreezerFreezeDuration", 10f,
                new ConfigDescription("How many seconds players stay frozen.",
                    new AcceptableValueRange<float>(2f, 30f)));
            CfgRoleChance = Config.Bind("Game", "FreezerRoleChance", 100f,
                new ConfigDescription("Chance that a selected candidate becomes Freezer.",
                    new AcceptableValueRange<float>(0f, 100f)));

            ManactorAPI.Register(RoleModName, Version);
            ManactorAPI.RegisterRpcMethods(this);
            ManactorAPI.RegisterRpcMethods(typeof(FreezeLogic));

            new Harmony(Guid).PatchAll();

            Log.LogInfo("Classic Us Freezer loaded.");
        }

        public static bool IsTypeReady;

        public static void EnsureIl2CppTypeRegistered()
        {
            if (_il2CppTypeRegistered) return;
            _il2CppTypeRegistered = true;

            ManactorAPI.RegisterIl2CppType(() =>
            {
                try
                {
                    ClassInjector.RegisterTypeInIl2Cpp<FreezerRole>();
                    IsTypeReady = true;
                    Log.LogInfo("FreezerRole type registered in IL2CPP.");
                }
                catch (Exception e)
                {
                    Log.LogError("FreezerRole registration failed: " + e);
                }
            });
        }

        private static bool _il2CppTypeRegistered;

        public static void HostBroadcastSettings()
        {
            ActiveEnabled = CfgEnabled.Value;
            ActiveCount = CfgCount.Value;
            ActiveAbilityCooldown = CfgAbilityCooldown.Value;
            ActiveFreezeDuration = CfgFreezeDuration.Value;
            ActiveRoleChance = CfgRoleChance.Value;

            ManactorAPI.SendRpcMethod(RpcSyncSettingsKey, ActiveEnabled, (byte)ActiveCount, ActiveAbilityCooldown, ActiveFreezeDuration, ActiveRoleChance);

            Log.LogInfo($"Freezer settings sent: enabled={ActiveEnabled} count={ActiveCount} abilityCd={ActiveAbilityCooldown} freezeDur={ActiveFreezeDuration} chance={ActiveRoleChance}");
        }

        [ManactorRpc(RpcSyncSettingsKey)]
        private static void OnSyncSettingsRpc(byte senderId, bool enabled, byte count, float abilityCooldown, float freezeDuration, float roleChance)
        {
            ActiveEnabled = enabled;
            ActiveCount = count;
            ActiveAbilityCooldown = abilityCooldown;
            ActiveFreezeDuration = freezeDuration;
            ActiveRoleChance = roleChance;
            Log.LogInfo($"Freezer settings received: enabled={ActiveEnabled} count={ActiveCount} abilityCd={ActiveAbilityCooldown} freezeDur={ActiveFreezeDuration} chance={ActiveRoleChance}");
            FreezerMenuInjector.UpdateMenuValues();
        }

        public static bool IsFreezer(PlayerControl p)
        {
            if (p == null || p.Data == null) return false;
            var role = p.Data.myRole;
            return role != null && role.SafeTryCast<FreezerRole>() != null;
        }
    }
}
