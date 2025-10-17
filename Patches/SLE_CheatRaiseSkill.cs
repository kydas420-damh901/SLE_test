using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Transpiler replacing the 100f hardcode in Skills.CheatRaiseSkill with a dynamic cap.
    /// Preserves vanilla behavior and only extends the upper limit.
    /// </summary>
    [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.CheatRaiseSkill))]
    [HarmonyPatch(new Type[] { typeof(string), typeof(float), typeof(bool) })]
    internal static class SLE_CheatRaiseSkill_Transpiler
    {
        [HarmonyTranspiler]
        [HarmonyPriority(-9000)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Acquire required method references
            var mi_GetCap = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetCap));
            var fi_m_info = AccessTools.Field(typeof(global::Skills.Skill), "m_info");
            var fi_m_skill = AccessTools.Field(typeof(global::Skills.SkillDef), "m_skill");

            if (mi_GetCap == null || fi_m_info == null || fi_m_skill == null)
                return codes;

            // Replace 100f with dynamic cap
            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];

                // Find and replace 'ldc.r4 100.0'
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Math.Abs(f - 100f) < 0.0001f)
                {
                    // Check if this is clamp context (inspect surrounding instructions)
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
                        // Safer implementation: use global UI denominator (fallback: 250)
                        var mi_GetUiDenominator = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetUiDenominator));
                        if (mi_GetUiDenominator != null)
                        {
                            var newInstr = new CodeInstruction(OpCodes.Call, mi_GetUiDenominator);
                            codes[i] = newInstr;
                            SkillLimitExtenderPlugin.Logger?.LogDebug("[SLE] CheatRaiseSkill: Replaced 100f with GetUiDenominator()");
                            break; // replace only one occurrence
                        }
                    }
                }
            }

            return codes;
        }
    }
}