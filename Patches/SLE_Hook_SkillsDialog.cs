using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Transpiler that replaces "/ 100f" with "/ cap" inside SkillsDialog.Setup.
    /// Uses a safer approach: prefers global UI denominator with a safe per-skill fallback only in SetValue contexts.
    /// </summary>
    [HarmonyPatch(typeof(global::SkillsDialog), nameof(global::SkillsDialog.Setup))]
    internal static class SLE_Hook_SkillsDialog_LevelBars
    {
        [HarmonyTranspiler]
        [HarmonyPriority(-9000)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            
            // Helper methods (safe per-skill / global fallback)
            var mi_GetUiDenominatorSafe = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetUiDenominatorForSkillSafe));
            var mi_GetUiDenominator = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetUiDenominator));
            if (mi_GetUiDenominator == null || mi_GetUiDenominatorSafe == null)
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] SkillsDialog: Cannot init UI denominator methods, using original code");
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
                    // Limit replacements to the immediate SetValue context to avoid false positives
                    bool isSetValueContext = false;
                    for (int j = i + 1; j < Math.Min(codes.Count, i + 10); j++)
                    {
                        if (codes[j].opcode == OpCodes.Callvirt &&
                            codes[j].operand?.ToString()?.Contains("SetValue") == true)
                        {
                            isSetValueContext = true;
                            break;
                        }
                    }
                
                    if (isSetValueContext)
                    {
                        // Try to infer a nearby local/argument that looks like the Skill object (fallback to global denominator)
                        CodeInstruction? ldSkillObj = null;
                        for (int back = Math.Max(0, i - 8); back < i; back++)
                        {
                            var ci = codes[back];
                            bool nearLevelAccess =
                                (ci.opcode == OpCodes.Ldfld && ci.operand?.ToString()?.Contains("Skills.Skill::m_level") == true) ||
                                (ci.opcode == OpCodes.Callvirt && ci.operand?.ToString()?.Contains("GetLevel") == true);
                
                            if (nearLevelAccess && back - 1 >= 0)
                            {
                                var prev = codes[back - 1];
                                if (prev.opcode == OpCodes.Ldloc || prev.opcode == OpCodes.Ldloc_S ||
                                    prev.opcode == OpCodes.Ldloc_0 || prev.opcode == OpCodes.Ldloc_1 || prev.opcode == OpCodes.Ldloc_2 || prev.opcode == OpCodes.Ldloc_3 ||
                                    prev.opcode == OpCodes.Ldarg || prev.opcode == OpCodes.Ldarg_S ||
                                    prev.opcode == OpCodes.Ldarg_0 || prev.opcode == OpCodes.Ldarg_1 || prev.opcode == OpCodes.Ldarg_2 || prev.opcode == OpCodes.Ldarg_3)
                                {
                                    ldSkillObj = prev;
                                    break;
                                }
                            }
                        }
                
                        List<CodeInstruction> newSeq;
                        if (ldSkillObj != null)
                        {
                            // Use per-skill denominator via safe helper
                            newSeq = new List<CodeInstruction>
                            {
                                ldSkillObj.Clone(),
                                new CodeInstruction(OpCodes.Call, mi_GetUiDenominatorSafe),
                                new CodeInstruction(OpCodes.Conv_R4)
                            };
                        }
                        else
                        {
                            // Fallback: global denominator
                            newSeq = new List<CodeInstruction>
                            {
                                new CodeInstruction(OpCodes.Call, mi_GetUiDenominator),
                                new CodeInstruction(OpCodes.Conv_R4)
                            };
                        }
                
                        codes[i] = newSeq[0];
                        codes.InsertRange(i + 1, newSeq.GetRange(1, newSeq.Count - 1));
                        i += newSeq.Count - 1;
                        replacementCount++;
                
                        SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] SkillsDialog: Replaced 100f #{replacementCount} with {(ldSkillObj != null ? "per-skill UI denominator (safe)" : "global UI denominator")}");
                    }
                }
            }
            
            SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] SkillsDialog: Total 100f replacements: {replacementCount}");
            return codes;
        }
    }
}