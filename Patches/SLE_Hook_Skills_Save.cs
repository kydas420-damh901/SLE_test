using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills.Save 実行前に不正な Skill エントリ（null/未初期化）をクリーンアップします。
    /// まれに他Modや不整合により m_skillData に null が混入しているケースを安全に回避します。
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

                    // m_info が未初期化のものも除去（Save で参照されるため）
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
                        // 取得に失敗する場合は安全側で除去
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