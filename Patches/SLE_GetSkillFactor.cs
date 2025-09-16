
using UnityEngine;

namespace SkillLimitExtender
{
    internal static class SLE_GetSkillFactor
    {
        /// <summary>
        /// バニラの 0..100 -> 0..cap に線形で正規化
        /// （必要ならここをバニラ成長曲線に合わせて非線形化）
        /// </summary>
        internal static float ScaleVanillaToCap(float level, int cap)
        {
            if (cap <= 0) return 0f;
            float t = Mathf.Clamp01(level / 100f);
            return t * cap;
        }

        /// <summary>
        /// スキル毎の cap を参照し、0..1 の係数を返す（UIや内部計算用）
        /// </summary>
        internal static float GetFactor(global::Skills skills, global::Skills.SkillType st)
        {
            var skill = skills.GetSkillSafe(st);     // 安全取得
            float level = skill?.m_level ?? 0f;

            int cap = SkillConfigManager.GetCap(st);
            float scaled = ScaleVanillaToCap(level, cap); // 0..cap
            return Mathf.Clamp01(scaled / cap);          // 0..1
        }

        /// <summary>
        /// UI最大値（cap + buff）を返す
        /// </summary>
        internal static float GetUiMaxWithBuff(int cap, float buffAmount)
        {
            return Mathf.Max(1f, cap + Mathf.Max(0f, buffAmount));
        }
    }
}
