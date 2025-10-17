using HarmonyLib;

namespace SkillLimitExtender
{
    internal static class SLE_TerminalCommands
    {
        public static void Register()
        {
            try
            {
                new Terminal.ConsoleCommand("sle_yaml_reload", "Reload SLE YAML", args =>
                {
                    if (ZNet.instance?.IsServer() == true)
                    {
                        SkillConfigManager.ReloadFromYaml();      // Server: reload YAML
                        SkillConfigManager.SendConfigToClientsIfChanged(); // Re-broadcast only if contents changed
                        args.Context.AddString("SLE: reloaded YAML; broadcasted only if changed.");
                    }
                    else
                    {
                        SkillConfigManager.ReloadFromYaml();      // Client: reload local YAML
                        args.Context.AddString("SLE: reloaded local YAML.");
                    }
                }, true);
            }
            catch (System.Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Failed to register console command: {e}");
            }
        }
    }
}