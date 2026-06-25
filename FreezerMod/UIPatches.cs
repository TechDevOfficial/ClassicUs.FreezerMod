using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ClassicUs.FreezerMod
{
    [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
    internal static class VersionShower_Start_Patch
    {
        private static void Postfix(VersionShower __instance)
        {
            try
            {
                var versionText = __instance.text;
                if (__instance == null || versionText == null) return;

                if (versionText.transform.Find("FreezerModVersion") != null) return;

                versionText.ForceMeshUpdate(false, false);
                var rend = versionText.GetComponent<MeshRenderer>();
                Bounds worldBounds = rend != null ? rend.bounds : new Bounds(versionText.transform.position, Vector3.zero);
                float gap = (worldBounds.size.y > 0f ? worldBounds.size.y : 0.3f) * 0.25f;
                float rightShift = (worldBounds.size.y > 0f ? worldBounds.size.y : 0.3f) * 0.23f;

                float baseY = worldBounds.min.y;
                foreach (Transform child in versionText.transform)
                {
                    if (!child.name.EndsWith("ModVersion")) continue;
                    var childRend = child.GetComponent<MeshRenderer>();
                    if (childRend != null) baseY = Mathf.Min(baseY, childRend.bounds.min.y);
                }

                var go = new GameObject("FreezerModVersion");
                go.transform.SetParent(versionText.transform, true);
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                go.transform.position = new Vector3(versionText.transform.position.x + rightShift, baseY - gap, versionText.transform.position.z);

                var tmp = go.AddComponent<TextMeshPro>();
                tmp.font = versionText.font;
                tmp.fontSharedMaterial = versionText.fontSharedMaterial;
                tmp.text = $"loaded FreezerMod v{FreezerPlugin.Version}";
                tmp.fontSize = versionText.fontSize;
                tmp.color = new Color(0.3f, 0.7f, 1f, 1f);
                tmp.alignment = versionText.alignment;
                tmp.enableWordWrapping = false;
            }
            catch (Exception e)
            {
                FreezerPlugin.Log.LogError("VersionShower patch: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
    internal static class PingTracker_Update_Patch
    {
        private static void Postfix(PingTracker __instance)
        {
            try
            {
                if (__instance != null && __instance.text != null)
                {
                    var t = __instance.text;
                    if (!t.Text.EndsWith("\nmod by Manu"))
                        t.Text += "\nmod by Manu";
                }
            }
            catch (Exception e)
            {
                FreezerPlugin.Log.LogError("PingTracker patch: " + e);
            }

            try
            {
                if (HudManager.InstanceExists)
                {
                    var tmp = HudManager.Instance.GameSettingsTMP;
                    if (tmp != null && !string.IsNullOrEmpty(tmp.text) && !tmp.text.Contains("Freezer Mod"))
                        tmp.text += "\n<color=#4DCCFF>< Freezer Mod ></color>";
                }
            }
            catch (Exception e)
            {
                FreezerPlugin.Log.LogError("HudManager GameSettings patch: " + e);
            }
        }
    }
}
