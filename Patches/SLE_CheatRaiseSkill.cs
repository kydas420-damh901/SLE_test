
using UnityEngine;

namespace SkillLimitExtender
{
    /// <summary>
    /// チート用途の簡易上昇ヘルパ（実際のコンソールコマンドは別実装を想定）
    /// </summary>
    internal static class SLE_CheatRaiseSkill
    {
        internal static void AddLevel(global::Skills skills, global::Skills.SkillType st, float delta)
        {
            var s = skills.GetSkillSafe(st);
            if (s == null) return;
            int cap = SkillConfigManager.GetCap(st);
            s.m_level = Mathf.Clamp(s.m_level + delta, 0f, cap);
        }
    }
}
