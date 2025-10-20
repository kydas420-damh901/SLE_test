using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Transpiler that replaces "/ 100f" with global UI denominator inside SkillsDialog.Setup.
    /// Always uses the same denominator for both levelbar and levelbar_total to ensure consistent scaling.
    /// </summary>
    [HarmonyPatch(typeof(global::SkillsDialog), nameof(global::SkillsDialog.Setup))]
    internal static class SLE_Hook_SkillsDialog_LevelBars
    {
        [HarmonyTranspiler]
        [HarmonyPriority(-9000)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var mi_GetUiDenominator = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetUiDenominator));

            if (mi_GetUiDenominator == null)
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] SkillsDialog: GetUiDenominator method missing; keeping original code");
                return codes;
            }

            int replacementCount = 0;

            for (int i = 0; i < codes.Count - 1; i++)
            {
                var instr = codes[i];
                var nextInstr = codes[i + 1];

                if (instr.opcode == OpCodes.Ldc_R4 &&
                    instr.operand is float f &&
                    Math.Abs(f - 100f) < 0.0001f &&
                    nextInstr.opcode == OpCodes.Div)
                {
                    // Check if this is in a SetValue context
                    bool isSetValueContext = false;
                    for (int j = i + 1; j < Math.Min(codes.Count, i + 12); j++)
                    {
                        if (codes[j].opcode == OpCodes.Callvirt &&
                            codes[j].operand?.ToString()?.Contains("SetValue") == true)
                        {
                            isSetValueContext = true;
                            break;
                        }
                    }

                    if (!isSetValueContext) continue;

                    // Replace with global UI denominator for consistent scaling
                    codes[i] = new CodeInstruction(OpCodes.Call, mi_GetUiDenominator);
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Conv_R4));
                    i++; // Skip the inserted instruction
                    replacementCount++;
                }
            }

            SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] SkillsDialog: Replaced {replacementCount} instances of 100f with global UI denominator");
            return codes;
        }
    }
}