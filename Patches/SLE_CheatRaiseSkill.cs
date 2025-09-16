using HarmonyLib;
using UnityEngine;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills.CheatRaiseSkill の後段で cap クランプを適用
    /// </summary>
    [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.CheatRaiseSkill))]
    internal static class SLE_CheatRaiseSkill_Patch
    {
        // 引数の skill 名はバニラ準拠。第二引数（value）があってもなくてもOK
        [HarmonyPostfix]
        private static void Postfix(global::Skills __instance, global::Skills.SkillType skill)
        {
            if (__instance == null) return;

            var s = __instance.GetSkillSafe(skill);
            if (s == null || s.m_info == null) return;

            int cap = SkillConfigManager.GetCap(s.m_info.m_skill);
            s.m_level = Mathf.Clamp(s.m_level, 0f, cap);
        }
    }
}
