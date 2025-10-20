using HarmonyLib;
using System.Collections.Generic;

namespace SkillLimitExtender
{
    internal static class SLE_VersionHandshake
    {
        // Validated peers (server-side)
        internal static readonly List<ZRpc> ValidatedPeers = new();

        internal static void RPC_SLE_Version(ZRpc rpc, ZPackage pkg)
        {
            try
            {
                var remoteVersion = pkg.ReadString();
                bool isServer = ZNet.instance?.IsServer() == true;

                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Version check: remote={remoteVersion}, local={VersionInfo.FullVersion}");

                if (remoteVersion != VersionInfo.FullVersion)
                {
                    if (isServer)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] Incompatible client version; disconnecting");
                        rpc.Invoke("Error", (object)3); // Disconnect peer (server-side)
                    }
                    else
                    {
                        SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] Server version differs; features may be limited");
                    }
                }
                else
                {
                    if (isServer)
                    {
                        ValidatedPeers.Add(rpc);
                    }
                    else
                    {
                        SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] Server and client versions match");
                    }
                }
            }
            catch (System.IO.EndOfStreamException ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Version handshake failed - incompatible data format: {ex.Message}");
                rpc.Invoke("Error", (object)3); // Disconnect on data format error
            }
            catch (System.Exception ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Version handshake error: {ex}");
            }
        }
    }

    // Register & send version on new connection
    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    internal static class SLE_VersionHandshake_OnNewConnection
    {
        [HarmonyPrefix]
        private static void Prefix(ZNetPeer peer, ZNet __instance)
        {
            peer.m_rpc.Register<ZPackage>("SLE_Version", new System.Action<ZRpc, ZPackage>(SLE_VersionHandshake.RPC_SLE_Version));

            var pkg = new ZPackage();
            pkg.Write(VersionInfo.FullVersion);
            peer.m_rpc.Invoke("SLE_Version", (object)pkg);
        }
    }

    // Enforce that peer sent version before proceeding
    [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
    internal static class SLE_VersionHandshake_VerifyClient
    {
        [HarmonyPrefix]
        private static bool Prefix(ZRpc rpc, ZPackage pkg, ZNet __instance)
        {
            if (__instance.IsServer() && !SLE_VersionHandshake.ValidatedPeers.Contains(rpc))
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] Peer never sent version; disconnecting");
                rpc.Invoke("Error", (object)3);
                return false; // Block underlying method
            }
            return true;
        }
    }

    // Cleanup validated list on disconnect
    [HarmonyPatch(typeof(ZNet), "Disconnect")]
    internal static class SLE_VersionHandshake_RemovePeerOnDisconnect
    {
        [HarmonyPrefix]
        private static void Prefix(ZNetPeer peer, ZNet __instance)
        {
            if (__instance.IsServer())
            {
                var rpc = peer?.m_rpc;
                if (rpc != null)
                {
                    SLE_VersionHandshake.ValidatedPeers.Remove(rpc);
                }
            }
        }
    }
}