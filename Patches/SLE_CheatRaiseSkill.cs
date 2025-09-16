using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Skills.CheatRaiseSkill内の100fハードコードを動的capに置換するTranspiler。
    /// バニラの処理はそのまま活かし、上限のみ拡張する。
    /// </summary>
    [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.CheatRaiseSkill))]
    [HarmonyPatch(new Type[] { typeof(string), typeof(float), typeof(bool) })]
    internal static class SLE_CheatRaiseSkill_Transpiler
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // 必要なメソッド参照を取得
            var mi_GetCap = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetCap));
            var fi_m_info = AccessTools.Field(typeof(global::Skills.Skill), "m_info");
            var fi_m_skill = AccessTools.Field(typeof(global::Skills.SkillDef), "m_skill");

            if (mi_GetCap == null || fi_m_info == null || fi_m_skill == null)
                return codes;

            // 100fを動的なcapに置換
            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];

                // 「ldc.r4 100.0」を探して置換
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Math.Abs(f - 100f) < 0.0001f)
                {
                    // Clampの文脈かどうかチェック（前後の命令を確認）
                    bool isClampContext = false;
                    for (int j = Math.Max(0, i - 5); j < Math.Min(codes.Count, i + 5); j++)
                    {
                        if (codes[j].opcode == OpCodes.Call &&
                            codes[j].operand?.ToString()?.Contains("Clamp") == true)
                        {
                            isClampContext = true;
                            break;
                        }
                    }

                    if (isClampContext)
                    {
                        // スキル変数をスタックから取得する方法を探す
                        // バニラコードの構造に合わせて適切なスキル参照を挿入

                        // 簡単な実装：デフォルトcapを使用
                        var fi_DefaultCap = AccessTools.Field(typeof(SkillConfigManager), nameof(SkillConfigManager.DefaultCap));
                        var prop_Value = AccessTools.Property(typeof(BepInEx.Configuration.ConfigEntry<int>), "Value");

                        if (fi_DefaultCap != null && prop_Value != null)
                        {
                            var newSeq = new List<CodeInstruction>
                            {
                                new CodeInstruction(OpCodes.Ldsfld, fi_DefaultCap),
                                new CodeInstruction(OpCodes.Callvirt, prop_Value.GetGetMethod()),
                                new CodeInstruction(OpCodes.Conv_R4)
                            };

                            codes[i] = newSeq[0];
                            codes.InsertRange(i + 1, newSeq.GetRange(1, newSeq.Count - 1));
                            i += newSeq.Count - 1;

                            SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] CheatRaiseSkill: Replaced 100f with DefaultCap");
                            break; // 1個だけ置換
                        }
                    }
                }
            }

            return codes;
        }
    }
}