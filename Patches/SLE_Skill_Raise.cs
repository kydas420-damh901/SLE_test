using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills.Skill.Raise 内の 100f ハードコードを、そのスキルの cap に置換する Transpiler。
    /// これにより通常成長時も cap（YAML/設定）まで上昇可能になる。
    /// </summary>
    [HarmonyPatch(typeof(global::Skills.Skill), nameof(global::Skills.Skill.Raise))]
    internal static class SLE_Skill_Raise_Transpiler
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // フィールドとメソッド参照を取得
            var fi_m_info = AccessTools.Field(typeof(global::Skills.Skill), "m_info");
            var fi_m_skill = AccessTools.Field(typeof(global::Skills.SkillDef), "m_skill");
            var mi_GetCap = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetCap));

            if (fi_m_info == null || fi_m_skill == null || mi_GetCap == null)
                return codes; // 安全策：何も置換できなければ元のILを返す

            // 置換ロジック：
            //   ldc.r4 100.0 → （ldarg.0 → ldfld m_info → ldfld m_skill → call GetCap → conv.r4）
            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];

                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && System.Math.Abs(f - 100f) < 0.0001f)
                {
                    // 100f を cap に差し替える
                    // ldarg.0                         // this (Skills.Skill)
                    // ldfld     Skills.Skill::m_info
                    // ldfld     Skills.SkillDef::m_skill
                    // call      int SkillConfigManager.GetCap(Skills.SkillType)
                    // conv.r4
                    var newSeq = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, fi_m_info),
                        new CodeInstruction(OpCodes.Ldfld, fi_m_skill),
                        new CodeInstruction(OpCodes.Call, mi_GetCap),
                        new CodeInstruction(OpCodes.Conv_R4)
                    };

                    // 先頭の命令を現在のスロットに置き換え、残りを後ろに挿入
                    codes[i] = newSeq[0];
                    codes.InsertRange(i + 1, newSeq.GetRange(1, newSeq.Count - 1));

                    // シーケンスを飛び越えないように i を進める
                    i += newSeq.Count - 1;
                }
            }

            return codes;
        }
    }
}
