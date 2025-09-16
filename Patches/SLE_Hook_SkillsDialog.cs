using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// SkillsDialog.Setup内の「/ 100f」を「/ cap」に置換するTranspiler。
    /// より安全なアプローチで、DefaultCapのみ使用。
    /// </summary>
    [HarmonyPatch(typeof(global::SkillsDialog), nameof(global::SkillsDialog.Setup))]
    internal static class SLE_Hook_SkillsDialog_LevelBars
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // DefaultCap参照を取得
            var fi_DefaultCap = AccessTools.Field(typeof(SkillConfigManager), nameof(SkillConfigManager.DefaultCap));
            var prop_Value = AccessTools.Property(typeof(BepInEx.Configuration.ConfigEntry<int>), "Value");

            if (fi_DefaultCap == null || prop_Value == null)
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] SkillsDialog: Cannot find DefaultCap, skipping patch");
                return codes;
            }

            int replacementCount = 0;

            // 安全な100f置換（SetValueの直前のみ）
            for (int i = 0; i < codes.Count - 1; i++)
            {
                var instr = codes[i];
                var nextInstr = codes[i + 1];

                // 「ldc.r4 100.0」の直後に「div」があり、その後「callvirt SetValue」がある場合のみ置換
                if (instr.opcode == OpCodes.Ldc_R4 &&
                    instr.operand is float f &&
                    Math.Abs(f - 100f) < 0.0001f &&
                    nextInstr.opcode == OpCodes.Div)
                {
                    // SetValueの呼び出しが近くにあるかチェック
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
                        // 100fをDefaultCapに置換
                        var newSeq = new List<CodeInstruction>
                        {
                            new CodeInstruction(OpCodes.Ldsfld, fi_DefaultCap),
                            new CodeInstruction(OpCodes.Callvirt, prop_Value.GetGetMethod()),
                            new CodeInstruction(OpCodes.Conv_R4)
                        };

                        codes[i] = newSeq[0];
                        codes.InsertRange(i + 1, newSeq.GetRange(1, newSeq.Count - 1));
                        i += newSeq.Count - 1;
                        replacementCount++;

                        SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] SkillsDialog: Replaced 100f #{replacementCount} with DefaultCap");
                    }
                }
            }

            SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] SkillsDialog: Total 100f replacements: {replacementCount}");
            return codes;
        }
    }
}