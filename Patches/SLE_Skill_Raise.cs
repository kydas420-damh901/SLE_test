
using UnityEngine;

namespace SkillLimitExtender
{
    /// <summary>
    /// ユーティリティ：スキルの現在値をcap内に収める
    /// （既存の呼び出し先から使えるよう最低限を用意）
    /// </summary>
    internal static class SLE_Skill_Raise
    {
        internal static void ClampToCap(global::Skills skills, global::Skills.SkillType st)
        {
            var s = skills.GetSkillSafe(st);
            if (s == null) return;
            int cap = SkillConfigManager.GetCap(st);
            s.m_level = Mathf.Clamp(s.m_level, 0f, cap);
        }
    }
}
