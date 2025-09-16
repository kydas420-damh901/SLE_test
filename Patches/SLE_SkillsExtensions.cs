
using System;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills から安全に Skill を取得する拡張
    /// </summary>
    internal static class SLE_SkillsExtensions
    {
        internal static global::Skills.Skill GetSkillSafe(this global::Skills skills, global::Skills.SkillType st)
        {
            if (skills == null) return null;

            // 署名がある環境では普通に呼べる
            try
            {
                return skills.GetSkill(st);
            }
            catch
            {
                // フォールバック：m_skillData を走査
                var list = skills.m_skillData;
                if (list == null) return null;
                for (int i = 0; i < list.Count; i++)
                {
                    var s = list[i];
                    if (s?.m_info != null && s.m_info.m_skill == st)
                        return s;
                }
                return null;
            }
        }
    }
}
