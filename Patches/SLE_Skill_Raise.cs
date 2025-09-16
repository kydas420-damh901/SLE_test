using HarmonyLib;
using UnityEngine;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills.Skill.Raise の後段で cap クランプを適用
    /// </summary>
    [HarmonyPatch(typeof(global::Skills.Skill), nameof(global::Skills.Skill.Raise))]
    internal static class SLE_Skill_Raise_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(global::Skills.Skill __instance)
        {
            if (__instance == null || __instance.m_info == null) return;
            int cap = SkillConfigManager.GetCap(__instance.m_info.m_skill);
            __instance.m_level = Mathf.Clamp(__instance.m_level, 0f, cap);
        }
    }
}
