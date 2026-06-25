using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace ClassicUs.FreezerMod
{
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.Start))]
    internal static class RoleManager_Start_Patch
    {
        private static void Postfix(RoleManager __instance) => RoleRegistration.EnsureFreezerRegistered(__instance);
    }

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRolesForTeam))]
    internal static class RoleManager_AssignRolesForTeam_Patch
    {
        private static void Prefix(RoleManager __instance, RoleTeamTypes type, int max)
        {
            RoleRegistration.EnsureFreezerRegistered(__instance);

            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            FreezerPlugin.HostBroadcastSettings();
        }

        private static void Postfix(RoleManager __instance, RoleTeamTypes type, int max)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (type != RoleTeamTypes.Impostor) return;
            if (!FreezerPlugin.ActiveEnabled || FreezerPlugin.ActiveCount <= 0) return;

            try { AssignFreezers(); }
            catch (Exception e) { FreezerPlugin.Log.LogError("Failed to assign Freezers: " + e); }
        }

        private static void AssignFreezers()
        {
            var rm = RoleManager.Instance;
            if (rm == null) return;

            var candidates = new List<PlayerControl>();
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.Disconnected || p.Data.IsDead) continue;
                var role = p.Data.myRole;
                if (role == null) continue;
                if (role.RoleTeamType != RoleTeamTypes.Impostor) continue;
                candidates.Add(p);
            }

            var rng = new System.Random();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int toAssign = Math.Min(FreezerPlugin.ActiveCount, candidates.Count);
            FreezerPlugin.Log.LogInfo($"[AssignFreezers] Assigning {toAssign} Freezer(s) from {candidates.Count} candidates");
            for (int i = 0; i < toAssign; i++)
            {
                var p = candidates[i];
                if (rng.NextDouble() * 100.0 >= FreezerPlugin.ActiveRoleChance)
                {
                    FreezerPlugin.Log.LogInfo($"[AssignFreezers] Role chance roll failed for playerId={p.Data.PlayerId}");
                    continue;
                }
                rm.AssignRole(p, FreezerPlugin.FreezerRoleName);
                FreezerPlugin.Log.LogInfo($"[AssignFreezers] Freezer assigned to playerId={p.Data.PlayerId}");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    internal static class PlayerControl_FixedUpdate_RoleRegistration_Patch
    {
        private static void Prefix(PlayerControl __instance)
        {
            if (__instance != PlayerControl.LocalPlayer) return;
            RoleRegistration.EnsureFreezerRegistered(RoleManager.Instance);
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.FixedUpdate))]
    internal static class HudManager_FixedUpdate_Patch
    {
        private static void Prefix(HudManager __instance)
        {
            if (RoleManager.InstanceExists)
                RoleRegistration.EnsureFreezerRegistered(RoleManager.Instance);

            try { FreezeButton.Tick(__instance); }
            catch (Exception e) { FreezerPlugin.Log.LogError("FreezeButton.Tick: " + e); }

            try { FreezeEffectManager.Tick(); }
            catch (Exception e) { FreezerPlugin.Log.LogError("FreezeEffectManager.Tick: " + e); }
        }
    }

    internal static class RoleRegistration
    {
        public static void EnsureFreezerRegistered(RoleManager rm)
        {
            if (rm == null) return;
            FreezerPlugin.EnsureIl2CppTypeRegistered();
            if (!FreezerPlugin.IsTypeReady) return;
            try
            {
                if (rm.allRoles != null)
                {
                    foreach (var r in rm.allRoles)
                        if (r != null && r.SafeTryCast<FreezerRole>() != null) return;
                }

                rm.AddRole(Il2CppType.Of<FreezerRole>(), FreezerPlugin.RoleModName);

                if (rm.allRoles == null) return;

                foreach (var role in rm.allRoles)
                {
                    if (role != null && role.SafeTryCast<FreezerRole>() != null)
                    {
                        FreezerPlugin.FreezerRoleName = role.roleCodeName;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                FreezerPlugin.Log.LogError("Failed to register Freezer role: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.OnAssign))]
    internal static class RoleBehaviour_OnAssign_Patch
    {
        private static void Postfix(RoleBehaviour __instance, PlayerControl player)
        {
            if (__instance == null || __instance.SafeTryCast<FreezerRole>() == null) return;
            try
            {
                __instance.RoleTeamType = RoleTeamTypes.Impostor;
                __instance.CanUseKillButton = true;
                __instance.CanVent = true;
                __instance.CanSabotage = true;
            }
            catch (Exception e)
            {
                FreezerPlugin.Log.LogError("Config Freezer OnAssign: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.roleDisplayName), MethodType.Getter)]
    internal static class RoleBehaviour_DisplayName_Patch
    {
        private static bool Prefix(RoleBehaviour __instance, ref string __result)
        {
            if (__instance == null || __instance.SafeTryCast<FreezerRole>() == null) return true;
            __result = "Freezer";
            return false;
        }
    }

    [HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.roleDescription), MethodType.Getter)]
    internal static class RoleBehaviour_Description_Patch
    {
        private static bool Prefix(RoleBehaviour __instance, ref string __result)
        {
            if (__instance == null || __instance.SafeTryCast<FreezerRole>() == null) return true;
            __result = "You are a Freezer. Press the freeze button to freeze every player for a few seconds.";
            return false;
        }
    }

    [HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.roleDescriptionShort), MethodType.Getter)]
    internal static class RoleBehaviour_DescriptionShort_Patch
    {
        private static bool Prefix(RoleBehaviour __instance, ref string __result)
        {
            if (__instance == null || __instance.SafeTryCast<FreezerRole>() == null) return true;
            __result = "Freeze everyone for a few seconds";
            return false;
        }
    }

    [HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.TeamColor), MethodType.Getter)]
    internal static class RoleBehaviour_TeamColor_Patch
    {
        private static bool Prefix(RoleBehaviour __instance, ref Color __result)
        {
            if (__instance == null || __instance.SafeTryCast<FreezerRole>() == null) return true;
            __result = new Color(0.3f, 0.7f, 1f, 1f);
            return false;
        }
    }

    [HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.IntroSound), MethodType.Getter)]
    internal static class RoleBehaviour_IntroSound_Patch
    {
        private static bool Prefix(RoleBehaviour __instance, ref AudioClip __result)
        {
            if (__instance == null || __instance.SafeTryCast<FreezerRole>() == null) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.KillAbilityImageName), MethodType.Getter)]
    internal static class RoleBehaviour_KillAbilityImageName_Patch
    {
        private static bool Prefix(RoleBehaviour __instance, ref string __result)
        {
            if (__instance == null || __instance.SafeTryCast<FreezerRole>() == null) return true;
            __result = string.Empty;
            return false;
        }
    }

    [HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.KillAbilityName), MethodType.Getter)]
    internal static class RoleBehaviour_KillAbilityName_Patch
    {
        private static bool Prefix(RoleBehaviour __instance, ref string __result)
        {
            if (__instance == null || __instance.SafeTryCast<FreezerRole>() == null) return true;
            __result = "Freeze";
            return false;
        }
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    internal static class ExileController_Begin_Patch
    {
        private static void Postfix(ExileController __instance, GameData.PlayerInfo exiled, bool tie)
        {
            if (__instance == null || exiled == null) return;
            try
            {
                var role = exiled.myRole;
                if (role == null || role.SafeTryCast<FreezerRole>() == null) return;

                string text = $"{exiled.PlayerName} was the Freezer.";
                if (__instance.Text != null) __instance.Text.Text = text;
                __instance.completeString = text;
            }
            catch (Exception e)
            {
                FreezerPlugin.Log.LogError("ExileController.Begin Freezer text patch: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.GetTeamColor))]
    internal static class IntroCutscene_GetTeamColor_Patch
    {
        private static void Postfix(RoleBehaviour role, ref Color __result)
        {
            if (role != null && role.SafeTryCast<FreezerRole>() != null)
                __result = new Color(0.3f, 0.7f, 1f, 1f);
        }
    }

    [HarmonyPatch(typeof(IntroCutscene._BeginTeam_d__18), nameof(IntroCutscene._BeginTeam_d__18.MoveNext))]
    internal static class IntroCutscene_BeginTeam_MoveNext_Patch
    {
        private static void Postfix(IntroCutscene._BeginTeam_d__18 __instance, ref bool __result)
        {
            if (!__result || __instance == null || __instance.__4__this == null) return;

            var local = PlayerControl.LocalPlayer;
            if (local != null && FreezerPlugin.IsFreezer(local))
            {
                __instance.__4__this.Title.text = "Freezer";
                __instance.__4__this.Title.color = new Color(0.3f, 0.7f, 1f, 1f);
                __instance.__4__this.DescriptionText.text = "You are a Freezer. Press the freeze button to freeze every player for a few seconds.";
            }
        }
    }

    internal static class FreezeButton
    {
        private static GameObject _buttonGo;
        private static SpriteRenderer _renderer;
        private static TextMeshPro _cooldownText;
        private static PassiveButton _passiveButton;
        private static float _cooldownRemaining;
        private static float _effectRemaining;

        public static void Tick(HudManager hud)
        {
            EnsureCreated(hud);
            if (_buttonGo == null) return;

            var local = PlayerControl.LocalPlayer;
            bool show = FreezerPlugin.IsFreezer(local) && local.Data != null && !local.Data.IsDead;
            if (_buttonGo.activeSelf != show) _buttonGo.SetActive(show);
            if (!show) return;

            if (_effectRemaining > 0f)
            {
                _effectRemaining = Math.Max(0f, _effectRemaining - Time.fixedDeltaTime);
                if (_cooldownText != null)
                {
                    _cooldownText.text = Math.Ceiling(_effectRemaining).ToString("0");
                    _cooldownText.color = new Color(0.3f, 0.9f, 0.3f, 1f);
                }
                if (_renderer != null) _renderer.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                if (_effectRemaining <= 0f) _cooldownRemaining = FreezerPlugin.ActiveAbilityCooldown;
            }
            else if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Math.Max(0f, _cooldownRemaining - Time.fixedDeltaTime);
                if (_cooldownText != null)
                {
                    _cooldownText.text = Math.Ceiling(_cooldownRemaining).ToString("0");
                    _cooldownText.color = Color.white;
                }
                if (_renderer != null) _renderer.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            else
            {
                if (_cooldownText != null)
                {
                    _cooldownText.text = string.Empty;
                    _cooldownText.color = Color.white;
                }
                if (_renderer != null) _renderer.color = Color.white;
            }
        }

        private static void EnsureCreated(HudManager hud)
        {
            if (_buttonGo != null) return;
            if (hud == null || hud.KillButton == null) return;

            var killButton = hud.KillButton;
            var clusterContainer = killButton.gameObject.transform.parent;
            var clone = UnityEngine.Object.Instantiate(killButton.gameObject, hud.transform);
            clone.name = "FreezeButton";

            var originalRenderer = clone.GetComponent<SpriteRenderer>();
            Bounds originalBounds = originalRenderer != null ? originalRenderer.bounds : default;

            var clusterAnchor = clusterContainer != null ? clusterContainer.GetComponentInParent<AspectPosition>() : null;
            var aspectPosition = clone.GetComponent<AspectPosition>();
            if (aspectPosition == null) aspectPosition = clone.AddComponent<AspectPosition>();

            aspectPosition.parentCam = clusterAnchor != null ? clusterAnchor.parentCam : hud.UICamera;
            aspectPosition.Alignment = AspectPosition.EdgeAlignments.LeftBottom;
            aspectPosition.DistanceFromEdge = new Vector3(1.4f, 1f, 0f);
            aspectPosition.updateAlways = true;
            aspectPosition.AdjustPosition();

            foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if (comp.TryCast<PassiveButton>() != null) continue;
                if (comp.TryCast<TextMeshPro>() != null) continue;
                if (comp.TryCast<AspectPosition>() != null) continue;
                comp.enabled = false;
                UnityEngine.Object.Destroy(comp);
            }

            _buttonGo = clone;
            _renderer = clone.GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = clone.AddComponent<SpriteRenderer>();

            var sprite = FreezeAssets.LoadFreezeSprite(originalBounds);
            if (sprite != null) _renderer.sprite = sprite;
            _renderer.color = Color.white;

            _cooldownText = clone.GetComponentInChildren<TextMeshPro>();

            _passiveButton = clone.GetComponentInChildren<PassiveButton>();
            if (_passiveButton != null && _passiveButton.OnClick != null)
            {
                _passiveButton.OnClick.RemoveAllListeners();
                _passiveButton.OnClick.AddListener((UnityEngine.Events.UnityAction)OnClick);
            }
        }

        private static void OnClick()
        {
            if (_cooldownRemaining > 0f || _effectRemaining > 0f) return;

            var local = PlayerControl.LocalPlayer;
            if (!FreezerPlugin.IsFreezer(local) || local.Data == null || local.Data.IsDead) return;

            _effectRemaining = FreezerPlugin.ActiveFreezeDuration;

            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost)
            {
                FreezeLogic.ResolveFreezeRequest(local.Data.PlayerId);
            }
            else
            {
                ManactorAPI.SendRpcMethod(FreezerPlugin.RequestFreezeKey);
            }
        }
    }

    internal static class FreezeLogic
    {
        [ManactorRpc(FreezerPlugin.RequestFreezeKey)]
        private static void OnFreezeRequest(byte senderId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            PlayerControl sender = null;
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null) continue;
                if (p.Data.PlayerId == senderId) { sender = p; break; }
            }

            if (sender == null || !FreezerPlugin.IsFreezer(sender) || sender.Data.IsDead) return;
            ResolveFreezeRequest(senderId);
        }

        public static void ResolveFreezeRequest(byte freezerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            FreezerPlugin.Log.LogInfo($"[Freezer] Broadcasting freeze for {FreezerPlugin.ActiveFreezeDuration}s");
            ManactorAPI.SendRpcMethod(FreezerPlugin.BroadcastFreezeKey, FreezerPlugin.ActiveFreezeDuration, freezerId);
            FreezeEffectManager.ApplyFreeze(FreezerPlugin.ActiveFreezeDuration, freezerId);
        }

        [ManactorRpc(FreezerPlugin.BroadcastFreezeKey)]
        private static void OnBroadcastFreeze(byte senderId, float duration, byte freezerId)
        {
            FreezeEffectManager.ApplyFreeze(duration, freezerId);
        }
    }

    internal static class FreezeEffectManager
    {
        private static float _unfreezeAt = -1f;
        private static byte _excludedFreezerId;
        private static readonly Dictionary<byte, GameObject> _effects = new();

        public static void ApplyFreeze(float duration, byte freezerId)
        {
            _unfreezeAt = Time.time + duration;
            _excludedFreezerId = freezerId;

            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.IsDead) continue;
                if (p.Data.PlayerId == freezerId) continue;
                Freeze(p);
            }
        }

        private static void Freeze(PlayerControl p)
        {
            p.moveable = false;
            if (p.rigidbody2D != null) p.rigidbody2D.velocity = Vector2.zero;
            EnsureEffect(p);
        }

        public static void Tick()
        {
            if (_unfreezeAt < 0f) return;

            bool stillFrozen = Time.time < _unfreezeAt;
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null) continue;
                if (p.Data.IsDead)
                {
                    RemoveEffect(p.Data.PlayerId);
                    continue;
                }
                if (p.Data.PlayerId == _excludedFreezerId) continue;

                if (stillFrozen)
                {
                    Freeze(p);
                }
            }

            if (!stillFrozen)
            {
                _unfreezeAt = -1f;
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p != null) p.moveable = true;
                }
                ClearAllEffects();
            }
        }

        private static void EnsureEffect(PlayerControl p)
        {
            if (p.Data == null) return;
            byte id = p.Data.PlayerId;
            if (_effects.TryGetValue(id, out var existing) && existing != null) return;

            var go = new GameObject("FreezeEffect");
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = FreezeAssets.GetRingSprite();
            renderer.color = new Color(0.45f, 0.85f, 1f, 0.95f);
            go.transform.localScale = Vector3.one * 3.2f;

            _effects[id] = go;
        }

        private static void RemoveEffect(byte id)
        {
            if (_effects.TryGetValue(id, out var go))
            {
                if (go != null) UnityEngine.Object.Destroy(go);
                _effects.Remove(id);
            }
        }

        private static void ClearAllEffects()
        {
            foreach (var kv in _effects)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            _effects.Clear();
        }
    }

    [HarmonyPatch(typeof(SettingMenu), nameof(SettingMenu.OnEnable))]
    internal static class SettingMenu_OnEnable_Patch
    {
        private static void Postfix(SettingMenu __instance)
        {
            var gameMenu = __instance.TryCast<GameSettingMenu>();
            if (gameMenu != null)
            {
                FreezerMenuInjector.ActiveMenu = gameMenu;
                try { FreezerMenuInjector.Inject(gameMenu); }
                catch (Exception e) { FreezerPlugin.Log.LogError("Inject toggle Freezer: " + e); }
            }
        }
    }

    internal static class FreezerMenuInjector
    {
        public static GameSettingMenu ActiveMenu;
        private static int _injectedCount;
        private static readonly Dictionary<int, float> _scrollerBaseMax = new();
        private static readonly Dictionary<string, TextMeshPro> _valueTexts = new();

        public static void Inject(GameSettingMenu menu)
        {
            if (menu == null || menu.AllItems == null || menu.AllItems.Count == 0) return;
            var parent = menu.AllItems[0].parent;
            if (parent == null) return;
            var template = menu.keyvaluePrefab;
            if (template == null) return;

            _injectedCount = 0;

            InjectToggle(menu, parent, template, "FreezerToggle", "Enable Freezer",
                () => {
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                        return FreezerPlugin.CfgEnabled.Value;
                    else
                        return FreezerPlugin.ActiveEnabled;
                },
                (val) => {
                    FreezerPlugin.CfgEnabled.Value = val;
                    FreezerPlugin.CfgEnabled.ConfigFile.Save();
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    {
                        FreezerPlugin.HostBroadcastSettings();
                    }
                });

            InjectNumeric(menu, parent, template, "FreezerCount", "Freezer Count", 1f, 1f, 3f, "0",
                () => {
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                        return FreezerPlugin.CfgCount.Value;
                    else
                        return FreezerPlugin.ActiveCount;
                },
                (val) => {
                    FreezerPlugin.CfgCount.Value = (int)val;
                    FreezerPlugin.CfgEnabled.ConfigFile.Save();
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    {
                        FreezerPlugin.HostBroadcastSettings();
                    }
                });

            InjectNumeric(menu, parent, template, "FreezerAbilityCooldown", "Freezer Ability Cooldown", 5f, 5f, 120f, "0s",
                () => {
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                        return FreezerPlugin.CfgAbilityCooldown.Value;
                    else
                        return FreezerPlugin.ActiveAbilityCooldown;
                },
                (val) => {
                    FreezerPlugin.CfgAbilityCooldown.Value = val;
                    FreezerPlugin.CfgEnabled.ConfigFile.Save();
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    {
                        FreezerPlugin.HostBroadcastSettings();
                    }
                });

            InjectNumeric(menu, parent, template, "FreezerFreezeDuration", "Freezer Freeze Duration", 1f, 2f, 30f, "0s",
                () => {
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                        return FreezerPlugin.CfgFreezeDuration.Value;
                    else
                        return FreezerPlugin.ActiveFreezeDuration;
                },
                (val) => {
                    FreezerPlugin.CfgFreezeDuration.Value = val;
                    FreezerPlugin.CfgEnabled.ConfigFile.Save();
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    {
                        FreezerPlugin.HostBroadcastSettings();
                    }
                });

            InjectNumeric(menu, parent, template, "FreezerRoleChance", "Freezer Role Chance", 10f, 0f, 100f, "0%",
                () => {
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                        return FreezerPlugin.CfgRoleChance.Value;
                    else
                        return FreezerPlugin.ActiveRoleChance;
                },
                (val) => {
                    FreezerPlugin.CfgRoleChance.Value = val;
                    FreezerPlugin.CfgEnabled.ConfigFile.Save();
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    {
                        FreezerPlugin.HostBroadcastSettings();
                    }
                });

            var scroller = parent.GetComponentInParent<Scroller>();
            if (scroller != null && scroller.YBounds != null)
            {
                int id = scroller.GetInstanceID();
                if (!_scrollerBaseMax.TryGetValue(id, out float baseMax))
                {
                    baseMax = scroller.YBounds.max;
                    _scrollerBaseMax[id] = baseMax;
                }

                var yb = scroller.YBounds;
                scroller.YBounds = new FloatRange(yb.min, baseMax + 2.5f);
            }
        }

        public static void UpdateMenuValues()
        {
            if (ActiveMenu == null || !ActiveMenu.gameObject.activeInHierarchy) return;

            try
            {
                if (_valueTexts.TryGetValue("FreezerToggle", out var toggleText) && toggleText != null)
                    toggleText.text = FreezerPlugin.ActiveEnabled ? "On" : "Off";

                if (_valueTexts.TryGetValue("FreezerCount", out var countText) && countText != null)
                    countText.text = FreezerPlugin.ActiveCount.ToString("0");

                if (_valueTexts.TryGetValue("FreezerAbilityCooldown", out var cooldownText) && cooldownText != null)
                    cooldownText.text = FreezerPlugin.ActiveAbilityCooldown.ToString("0s");

                if (_valueTexts.TryGetValue("FreezerFreezeDuration", out var durationText) && durationText != null)
                    durationText.text = FreezerPlugin.ActiveFreezeDuration.ToString("0s");

                if (_valueTexts.TryGetValue("FreezerRoleChance", out var chanceText) && chanceText != null)
                    chanceText.text = FreezerPlugin.ActiveRoleChance.ToString("0%");
            }
            catch (Exception e)
            {
                FreezerPlugin.Log.LogError("Error updating client menu: " + e);
            }
        }

        private static void InjectToggle(GameSettingMenu menu, Transform parent, NumberOption template, string name, string label, Func<bool> getter, Action<bool> setter)
        {
            var isHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            var existing = parent.Find(name);
            Transform target;
            TextMeshPro valueText;

            if (existing != null)
            {
                target = existing;
                float yPos = menu.YStart - (menu.AllItems.Count + _injectedCount) * menu.YOffset;
                target.localPosition = new Vector3(target.localPosition.x, yPos, target.localPosition.z);
                _valueTexts.TryGetValue(name, out valueText);
            }
            else
            {
                var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
                go.name = name;
                float y = menu.YStart - (menu.AllItems.Count + _injectedCount) * menu.YOffset;
                go.transform.localPosition = new Vector3(template.transform.localPosition.x, y, template.transform.localPosition.z);
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                go.SetActive(true);
                target = go.transform;

                var no = go.GetComponent<NumberOption>();
                var titleText = no != null ? no.TitleText : null;
                valueText = no != null ? no.ValueText : null;
                if (titleText != null) titleText.text = label;
                if (no != null) UnityEngine.Object.Destroy(no);

                _valueTexts[name] = valueText;
            }

            _injectedCount++;

            if (valueText != null) valueText.text = getter() ? "On" : "Off";

            foreach (var pb in target.GetComponentsInChildren<PassiveButton>())
            {
                if (pb == null) continue;
                pb.gameObject.SetActive(isHost);
                if (!isHost || pb.OnClick == null) continue;
                pb.OnClick.RemoveAllListeners();
                var capturedText = valueText;
                pb.OnClick.AddListener((UnityAction)(() =>
                {
                    setter(!getter());
                    if (capturedText != null)
                        capturedText.text = getter() ? "On" : "Off";
                }));
            }
        }

        private static void InjectNumeric(GameSettingMenu menu, Transform parent, NumberOption template, string name, string label, float step, float min, float max, string format, Func<float> getter, Action<float> setter)
        {
            var isHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            var existing = parent.Find(name);
            Transform target;
            TextMeshPro valueText;

            if (existing != null)
            {
                target = existing;
                float yPos = menu.YStart - (menu.AllItems.Count + _injectedCount) * menu.YOffset;
                target.localPosition = new Vector3(target.localPosition.x, yPos, target.localPosition.z);
                _valueTexts.TryGetValue(name, out valueText);
            }
            else
            {
                var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
                go.name = name;
                float y = menu.YStart - (menu.AllItems.Count + _injectedCount) * menu.YOffset;
                go.transform.localPosition = new Vector3(template.transform.localPosition.x, y, template.transform.localPosition.z);
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                go.SetActive(true);
                target = go.transform;

                var no = go.GetComponent<NumberOption>();
                var titleText = no != null ? no.TitleText : null;
                valueText = no != null ? no.ValueText : null;
                if (titleText != null) titleText.text = label;
                if (no != null) UnityEngine.Object.Destroy(no);

                _valueTexts[name] = valueText;
            }

            _injectedCount++;

            if (valueText != null) valueText.text = getter().ToString(format);

            var buttons = target.GetComponentsInChildren<PassiveButton>();
            var sorted = new List<PassiveButton>();
            foreach (var b in buttons) if (b != null) sorted.Add(b);
            sorted.Sort((a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));

            foreach (var pb in sorted) pb.gameObject.SetActive(isHost);

            if (isHost && sorted.Count >= 2)
            {
                var dec = sorted[0];
                var inc = sorted[sorted.Count - 1];
                var capturedText = valueText;

                dec.OnClick.RemoveAllListeners();
                dec.OnClick.AddListener((UnityAction)(() =>
                {
                    float val = Math.Max(min, getter() - step);
                    setter(val);
                    if (capturedText != null)
                        capturedText.text = getter().ToString(format);
                }));

                inc.OnClick.RemoveAllListeners();
                inc.OnClick.AddListener((UnityAction)(() =>
                {
                    float val = Math.Min(max, getter() + step);
                    setter(val);
                    if (capturedText != null)
                        capturedText.text = getter().ToString(format);
                }));
            }
        }
    }
}
