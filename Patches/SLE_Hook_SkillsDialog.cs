using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills.GetSkillFactor 内の「/ 100f」を「/ cap」に置換するTranspiler。
    /// これによりスキル効果の計算係数が拡張上限に対応する。
    /// </summary>
    [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.GetSkillFactor))]
    internal static class SLE_Hook_SkillsDialog_LevelBars
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // 必要なメソッド参照を取得
            var mi_GetCap = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetCap));

            if (mi_GetCap == null)
                return codes; // 安全策：何も置換できなければ元のILを返す

            // 100fを動的なcapに置換
            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];

                // 「ldc.r4 100.0」を探して置換
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && System.Math.Abs(f - 100f) < 0.0001f)
                {
                    // 除算の文脈かどうかチェック（次の命令がdivかどうか）
                    if (i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Div)
                    {
                        // 100fを動的なcapに置換
                        // ldarg.1 (skillType パラメータ)
                        // call int SkillConfigManager.GetCap(Skills.SkillType)
                        // conv.r4
                        var newSeq = new List<CodeInstruction>
                        {
                            new CodeInstruction(OpCodes.Ldarg_1), // skillType パラメータ
                            new CodeInstruction(OpCodes.Call, mi_GetCap),
                            new CodeInstruction(OpCodes.Conv_R4)
                        };

                        // 先頭の命令を現在のスロットに置き換え、残りを後ろに挿入
                        codes[i] = newSeq[0];
                        codes.InsertRange(i + 1, newSeq.GetRange(1, newSeq.Count - 1));

                        // シーケンスを飛び越えないように i を進める
                        i += newSeq.Count - 1;

                        SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] GetSkillFactor: Replaced 100f with dynamic cap");
                        break; // GetSkillFactorには100fが1個だけなので終了
                    }
                }
            }

            return codes;
        }
    }
}