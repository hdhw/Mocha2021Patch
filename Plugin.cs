using BepInEx;
using BepInEx.Unity.IL2CPP;
using BestHTTP;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Mocha2021Patch;

[BepInPlugin("Mocha2021Patch", "Mocha2021 Server Redirect", "1.0.0")]
public class Plugin : BasePlugin
{
    private const string PhotonAppIdRealtime = "7ff599b9-d7d4-4d49-989f-ea5aee49af99";
    private const string PhotonAppIdVoice = "2d02323f-72c6-4301-9589-28b83e04d439";

    public override void Load()
    {
        try
        {
            var httpHarmony = new Harmony("Mocha2021Patch.Http");
            httpHarmony.PatchAll(typeof(HttpRedirectPatch));
            Log.LogInfo("Mocha2021Patch: HTTP redirect patch applied");
        }
        catch (System.Exception e)
        {
            Log.LogError("Mocha2021Patch: HTTP redirect patch FAILED: " + e);
        }

        try
        {
            var photonHarmony = new Harmony("Mocha2021Patch.Photon");
            photonHarmony.PatchAll(typeof(PhotonSettingsPatch));
            Log.LogInfo("Mocha2021Patch: Photon settings patch applied");
        }
        catch (System.Exception e)
        {
            Log.LogError("Mocha2021Patch: Photon settings patch FAILED: " + e);
        }
    }

    public static class HttpRedirectPatch
    {
        [HarmonyPatch(typeof(HTTPManager), "SendRequest", new System.Type[] { typeof(HTTPRequest) })]
        [HarmonyPrefix]
        private static void Prefix(ref HTTPRequest request)
        {
            try
            {
                var il2cppUri = request.Uri;
                if (il2cppUri == null) return;

                var absolute = il2cppUri.AbsoluteUri;
                var parsed = new System.Uri(absolute);
                var host = parsed.Host;

                var isTarget = host.EndsWith("rec.net", System.StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("recroom.com", System.StringComparison.OrdinalIgnoreCase);
                if (!isTarget) return;

                var newUrl = "http://localhost:5000" + parsed.PathAndQuery;
                request.Uri = new Il2CppSystem.Uri(newUrl);
                System.Console.WriteLine("[Mocha2021Patch] redirected " + absolute + " -> " + newUrl);
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("[Mocha2021Patch] redirect failed: " + e);
            }
        }
    }

    public static class PhotonSettingsPatch
    {
        private static bool _patched;

        [HarmonyPatch(typeof(Resources), "Load", new System.Type[] { typeof(string), typeof(Il2CppSystem.Type) })]
        [HarmonyPostfix]
        private static void TypedLoadPostfix(string path, ref UnityEngine.Object __result)
        {
            Apply(path, __result);
        }

        private static void Apply(string path, UnityEngine.Object result)
        {
            try
            {
                if (_patched) return;
                if (path != "PhotonServerSettings" || result == null) return;

                var settings = result.TryCast<ServerSettings>();
                if (settings == null) return;

                var appSettings = settings.AppSettings;
                if (appSettings == null) return;

                appSettings.AppIdRealtime = PhotonAppIdRealtime;
                appSettings.AppIdVoice = PhotonAppIdVoice;
                appSettings.Protocol = ExitGames.Client.Photon.ConnectionProtocol.Udp;
                _patched = true;
                System.Console.WriteLine("[Mocha2021Patch] patched PhotonServerSettings AppIdRealtime/AppIdVoice/Protocol=Udp");
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("[Mocha2021Patch] photon settings patch failed: " + e);
            }
        }
    }
}
