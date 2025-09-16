using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills から安全に Skill を取得（private API を反射でカバー）
    /// </summary>
    internal static class SLE_SkillsExtensions
    {
        private static readonly MethodInfo _miGetSkill =
            AccessTools.Method(typeof(global::Skills), "GetSkill", new Type[] { typeof(global::Skills.SkillType) });

        private static readonly FieldInfo _fiSkillData =
            AccessTools.Field(typeof(global::Skills), "m_skillData");

        internal static global::Skills.Skill? GetSkillSafe(this global::Skills skills, global::Skills.SkillType st)
        {
            if (skills == null) return null;

            // 1) private GetSkill(skillType) を反射で呼ぶ
            if (_miGetSkill != null)
            {
                try
                {
                    return (global::Skills.Skill?)_miGetSkill.Invoke(skills, new object[] { st });
                }
                catch { /* fallbackへ */ }
            }

            // 2) m_skillData を直接読む（Dictionary<SkillType, Skill>）
            if (_fiSkillData != null)
            {
                try
                {
                    var dict = _fiSkillData.GetValue(skills) as IDictionary<global::Skills.SkillType, global::Skills.Skill>;
                    if (dict != null && dict.TryGetValue(st, out var s))
                        return s;
                }
                catch { /* ignore */ }
            }

            return null;
        }
    }
}
