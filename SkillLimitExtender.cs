using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;

namespace SkillLimitExtender
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SkillLimitExtenderPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "SkillLimitExtender";
        internal const string PluginName = "SkillLimitExtender";
        internal const string PluginVersion = "1.0.0";

        internal new static ManualLogSource Logger { get; private set; } = null!;
        private readonly Harmony _harmony = new(PluginGuid);

        private void Awake()
        {
            Logger = base.Logger;

            try
            {
                // 設定とYAML初期化
                SkillConfigManager.Initialize(Config);
                YamlExporter.EnsureYamlExists();

                // Harmonyパッチ適用
                _harmony.PatchAll(typeof(SkillLimitExtenderPlugin).Assembly);

                Logger.LogInfo($"[SLE] Plugin loaded successfully (v{PluginVersion})");
            }
            catch (Exception e)
            {
                Logger.LogError($"[SLE] Awake failed: {e}");
            }
        }

        private void Start()
        {
            // ゲーム開始後にRPC登録
            if (ZNet.instance != null)
            {
                try
                {
                    ZRoutedRpc.instance.Register<int, bool>("SLE_ConfigSync", SkillConfigManager.OnConfigReceivedStatic);
                    SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] RPC registered successfully");
                }
                catch (Exception e)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] RPC registration failed: {e}");
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (Exception e)
            {
                Logger.LogError($"[SLE] UnpatchSelf failed: {e}");
            }
        }
    }

    /// <summary>
    /// プレイヤーがワールドにスポーンした時にサーバー設定を送信
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class SLE_Hook_PlayerSpawned
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (ZNet.instance?.IsServer() == true && __instance != null)
            {
                // プレイヤーがスポーンした時にサーバー設定を送信
                SkillConfigManager.SendConfigToClients();
            }
        }
    }
}