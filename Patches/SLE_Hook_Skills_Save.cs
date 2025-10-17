using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Cleans up invalid Skill entries (null/uninitialized) before Skills.Save runs.
    /// Safely avoids rare cases where other mods or inconsistencies insert nulls into m_skillData.
    /// </summary>
    [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.Save))]
    internal static class SLE_Hook_Skills_Save_Cleanup
    {
        [HarmonyPrefix]
        private static void Prefix(global::Skills __instance)
        {
            try
            {
                var fiSkillData = AccessTools.Field(typeof(global::Skills), "m_skillData");
                var dict = fiSkillData?.GetValue(__instance) as IDictionary<global::Skills.SkillType, global::Skills.Skill>;
                if (dict == null) return;

                var badKeys = new List<global::Skills.SkillType>();
                foreach (var kv in dict)
                {
                    var skill = kv.Value;
                    if (skill == null)
                    {
                        badKeys.Add(kv.Key);
                        continue;
                    }

                    // Also remove entries with uninitialized m_info (referenced during Save)
                    try
                    {
                        var info = HarmonyLib.Traverse.Create(skill).Field("m_info").GetValue<global::Skills.SkillDef>();
                        if (info == null)
                        {
                            badKeys.Add(kv.Key);
                        }
                    }
                    catch
                    {
                        // If retrieval fails, remove conservatively for safety
                        badKeys.Add(kv.Key);
                    }
                }

                foreach (var k in badKeys)
                {
                    dict.Remove(k);
                }

                if (badKeys.Count > 0)
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Skills.Save cleanup: removed {badKeys.Count} invalid skill entries");
                }
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Skills.Save cleanup failed: {e}");
            }
        }
    }
}