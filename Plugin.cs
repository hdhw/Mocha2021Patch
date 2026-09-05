using BepInEx;
using BepInEx.Unity.IL2CPP;
using BestHTTP;
using HarmonyLib;
using Org.BouncyCastle.Crypto.Tls;

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
            var tlsBypassHarmony = new Harmony("Mocha2021Patch.TlsBypass");
            tlsBypassHarmony.PatchAll(typeof(BestHttpTlsBypassPatch));
            Log.LogInfo("Mocha2021Patch: BestHTTP TLS bypass patch applied");
        }
        catch (System.Exception e)
        {
            Log.LogError("Mocha2021Patch: BestHTTP TLS bypass patch FAILED: " + e);
        }

        try
        {
            var monoTlsHarmony = new Harmony("Mocha2021Patch.MonoTlsBypass");
            monoTlsHarmony.PatchAll(typeof(MonoTlsBypassPatch));
            Log.LogInfo("Mocha2021Patch: Mono legacy HttpWebRequest TLS bypass patch applied");
        }
        catch (System.Exception e)
        {
            Log.LogError("Mocha2021Patch: Mono legacy HttpWebRequest TLS bypass patch FAILED: " + e);
        }
    }

    internal static class DurableLog
    {
        private static readonly object Gate = new();

        public static void Write(string line)
        {
            try
            {
                lock (Gate)
                {
                    System.IO.File.AppendAllText("mocha_debug.log", System.DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + System.Environment.NewLine);
                }
            }
            catch { }
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
            var line = "[AUTH-DEBUG] OnCustomAuthenticationFailed message = " + EMNPHAPBBAH;
            System.Console.WriteLine("[Mocha2021Patch]" + line);
            DurableLog.Write(line);
        }
    }

    public static class BestHttpTlsBypassPatch
    {
        [HarmonyPatch(typeof(HTTPRequest), "Send")]
        [HarmonyPrefix]
        private static void Prefix(HTTPRequest __instance)
        {
            try
            {
                __instance.CustomCertificateVerifyer = null;
                __instance.CustomCertificationValidator = null;
                __instance.CustomClientCredentialsProvider = null;
                __instance.CustomTLSServerNameList = null;
                var uri = __instance.Uri.OriginalString;
                System.Console.WriteLine("[Mocha2021Patch][TLS] cleared cert validation on request: " + uri);
                DurableLog.Write("[TLS-SEND] " + uri);
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("[Mocha2021Patch][TLS] failed to clear cert validation: " + e);
                DurableLog.Write("[TLS-SEND-ERROR] " + e);
            }
        }

        [HarmonyPatch(typeof(LegacyTlsAuthentication), "NotifyServerCertificate")]
        [HarmonyPrefix]
        private static bool PrefixNotify()
        {
            DurableLog.Write("[TLS-NOTIFY] NotifyServerCertificate bypassed");
            return false;
        }
    }

    public static class MonoTlsBypassPatch
    {
        [HarmonyPatch(typeof(Il2CppSystem.Net.ServerCertValidationCallback), "Invoke")]
        [HarmonyPrefix]
        private static bool PrefixInvoke(ref bool __result)
        {
            __result = true;
            DurableLog.Write("[MONO-TLS] ServerCertValidationCallback.Invoke bypassed -> true");
            return false;
        }

        [HarmonyPatch(typeof(Il2CppSystem.Net.WebConnection), "CreateStream")]
        [HarmonyPostfix]
        private static void PostfixCreateStream(Il2CppSystem.Net.HttpWebRequest request, bool __result)
        {
            try
            {
                DurableLog.Write("[MONO-TLS] WebConnection.CreateStream(" + request.RequestUri.OriginalString + ") -> " + __result);
            }
            catch (System.Exception e)
            {
                DurableLog.Write("[MONO-TLS] CreateStream postfix logging failed: " + e);
            }
        }
    }
}
