using BepInEx;
using BepInEx.Unity.IL2CPP;
using BestHTTP;
using HarmonyLib;

namespace Mocha2021Patch;

[BepInPlugin("Mocha2021Patch", "Mocha2021 Server Redirect", "1.0.0")]
public class Plugin : BasePlugin
{
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
            var diagHarmony = new Harmony("Mocha2021Patch.Diagnostics");
            diagHarmony.PatchAll(typeof(AuthDiagnosticsPatch));
            Log.LogInfo("Mocha2021Patch: auth diagnostics patch applied");
        }
        catch (System.Exception e)
        {
            Log.LogError("Mocha2021Patch: auth diagnostics patch FAILED: " + e);
        }

        try
        {
            var tlsHarmony = new Harmony("Mocha2021Patch.Tls");
            tlsHarmony.PatchAll(typeof(TlsPatch));
            Log.LogInfo("Mocha2021Patch: TLS 1.2 patch applied");
        }
        catch (System.Exception e)
        {
            Log.LogError("Mocha2021Patch: TLS 1.2 patch FAILED: " + e);
        }

        try
        {
            var webRequestHarmony = new Harmony("Mocha2021Patch.WebRequest");
            webRequestHarmony.PatchAll(typeof(WebRequestRedirectPatch));
            Log.LogInfo("Mocha2021Patch: WebRequest.Create redirect patch applied");
        }
        catch (System.Exception e)
        {
            Log.LogError("Mocha2021Patch: WebRequest.Create redirect patch FAILED: " + e);
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

    public static class AuthDiagnosticsPatch
    {
        [HarmonyPatch(typeof(PUNNetworkManager), "OnCustomAuthenticationFailed")]
        [HarmonyPrefix]
        private static void Prefix(string EMNPHAPBBAH)
        {
            System.Console.WriteLine("[Mocha2021Patch][AUTH-DEBUG] OnCustomAuthenticationFailed message = " + EMNPHAPBBAH);
        }
    }

    public static class SslDiagnosticsPatch
    {
        [HarmonyPatch(typeof(Il2CppSystem.Net.Security.SslStream), "AuthenticateAsClient", new System.Type[] { typeof(string) })]
        [HarmonyPrefix]
        private static void Prefix(string targetHost)
        {
            System.Console.WriteLine("[Mocha2021Patch][SSL-DEBUG] AuthenticateAsClient targetHost = " + targetHost);
        }

        [HarmonyPatch(typeof(Il2CppSystem.Net.Security.SslStream), "AuthenticateAsClientAsync",
            new System.Type[] { typeof(string), typeof(Il2CppSystem.Security.Cryptography.X509Certificates.X509CertificateCollection), typeof(Il2CppSystem.Security.Authentication.SslProtocols), typeof(bool) })]
        [HarmonyPrefix]
        private static void PrefixAsync(string targetHost)
        {
            System.Console.WriteLine("[Mocha2021Patch][SSL-DEBUG] AuthenticateAsClientAsync targetHost = " + targetHost);
        }
    }

    public static class TlsPatch
    {
        [HarmonyPatch(typeof(Il2CppSystem.Net.ServicePointManager), "get_SecurityProtocol")]
        [HarmonyPrefix]
        private static bool Prefix(ref Il2CppSystem.Net.SecurityProtocolType __result)
        {
            __result = Il2CppSystem.Net.SecurityProtocolType.Tls12;
            return false;
        }
    }

    public static class WebRequestRedirectPatch
    {
        [HarmonyPatch(typeof(Il2CppSystem.Net.WebRequest), "Create", new System.Type[] { typeof(string) })]
        [HarmonyPrefix]
        private static bool Prefix(ref string requestUriString, ref Il2CppSystem.Net.WebRequest __result)
        {
            try
            {
                if (string.IsNullOrEmpty(requestUriString)) return true;
                var parsed = new System.Uri(requestUriString);
                var host = parsed.Host;

                var isTarget = host.EndsWith("rec.net", System.StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("recroom.com", System.StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("exitgames.com", System.StringComparison.OrdinalIgnoreCase);
                if (!isTarget) return true;

                var newUrl = "http://localhost:5000" + parsed.PathAndQuery;
                System.Console.WriteLine("[Mocha2021Patch] WebRequest.Create redirected " + requestUriString + " -> " + newUrl);
                requestUriString = newUrl;
                return true;
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("[Mocha2021Patch] WebRequest.Create redirect failed: " + e);
                return true;
            }
        }
    }
}
