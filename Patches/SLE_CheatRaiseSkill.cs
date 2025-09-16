using System;
using HarmonyLib;
using UnityEngine;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills.CheatRaiseSkill(string name, float value, bool showMessage)
    /// 実行後に cap クランプを適用する。
    /// </summary>
    [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.CheatRaiseSkill))]
    [HarmonyPatch(new Type[] { typeof(string), typeof(float), typeof(bool) })]
    internal static class SLE_CheatRaiseSkill_Patch
    {
        // Postfix は元メソッドの引数シグネチャに合わせる
        [HarmonyPostfix]
        private static void Postfix(global::Skills __instance, string name, float value, bool showMessage)
        {
            if (__instance == null) return;
            if (string.IsNullOrWhiteSpace(name)) return;

            // 1) まず enum 名で解決（標準のチートはバニラスキル名を使う想定）
            if (Enum.TryParse<global::Skills.SkillType>(name, true, out var st))
            {
                var s = __instance.GetSkillSafe(st);
                if (s != null && s.m_info != null)
                {
                    int cap = SkillConfigManager.GetCap(s.m_info.m_skill);
                    s.m_level = Mathf.Clamp(s.m_level, 0f, cap);
                    return;
                }
            }

            // 2) それでも見つからない場合は、念のため全スキル走査（名前が大小/表記ゆれの場合のフォールバック）
            //    m_skillData は拡張で読んでいるので、GetSkillSafe で既知の列挙をひと通り試す
            foreach (global::Skills.SkillType t in Enum.GetValues(typeof(global::Skills.SkillType)))
            {
                var s2 = __instance.GetSkillSafe(t);
                if (s2 == null || s2.m_info == null) continue;

                // enum名と比較（大文字小文字無視）
                if (string.Equals(t.ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    int cap = SkillConfigManager.GetCap(s2.m_info.m_skill);
                    s2.m_level = Mathf.Clamp(s2.m_level, 0f, cap);
                    return;
                }
            }

            // 3) どうしても一致しない場合は何もしない（ログだけ）
            SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] CheatRaiseSkill postfix: skill '{name}' could not be resolved.");
        }
    }
}
