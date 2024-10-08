using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PugMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ModIOManager
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "ModIOManager";
        public const string PluginVersion = "1.0.0";
        public static ManualLogSource Log;
        public static PluginInfo pluginInfo;
        public static Harmony Harmony;
        public static ConfigFile Config;
        public static ConfigEntry<string> GameID;
        public static ConfigEntry<bool> Check;
        public static ConfigEntry<bool> Reset;
        public static List<string> Mods = [];
        public static Main Instance;
        public static string Folder;
        public static HttpClient Client = new();
        public static string API_KEY = "e573f74ee80400ebed17a32740527333";
        public static string downloadLink = null;

        public void Awake()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            pluginInfo = Info;
            Log = Logger;
            Instance = this;
            Harmony = new(PluginGUID);
            Config = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);
            GameID = Config.Bind("General", "Game ID", "5289", "default is core keeper");
            Check = Config.Bind("General", "Check for Newest Version", true, "requires network connection, may take a while loading if set to true");
            Reset = Config.Bind("General", "Redownload All", false, "if set to true, will download every mod from mod.io regardless of if the mod is present.");
            var _Mods = Config.Bind("General", "Mod List", "CoreLib, CoreLib.Localization, CoreLib.RewiredExtension", "Current Mod List. if the list disagrees with the current modlist, will install new ones.");
            Folder = Path.Combine(Paths.GameRootPath, "CoreKeeper_Data\\StreamingAssets\\Mods");
            if (!Directory.Exists(Folder)) { Log.LogInfo("Creating Mod.io Directory"); Directory.CreateDirectory(Folder); }
            Mods = _Mods.Value.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            Client.DefaultRequestHeaders.Add("User-Agent", "Other");
            foreach (var d in Directory.GetDirectories(Folder))
            {
                var raw = Path.GetFileName(d);
                if (raw.IndexOf('=') == -1) { Log.LogWarning($"Removing non-modiomanager mod {d}. please install it through here or refer to the readme!"); Directory.Delete(d); }
                try
                {
                    var mod = raw[0..raw.IndexOf('=')];
                    var version = long.Parse(raw[(raw.IndexOf("=") + 1)..]);
                    if (!Mods.Contains(mod)) { Log.LogInfo($"Removing {d}"); Directory.Delete(d); }
                    else
                    {
                        var download = Reset.Value;
                        if (!download && Check.Value) { var _version = CheckVersion(mod); download = version < _version; version = _version; }
                        if (download) { Log.LogInfo("Updating " + mod); Download(mod, version); }
                        Mods.Remove(mod);
                    }
                }
                catch { Log.LogWarning($"Removing non-modiomanager mod {d}. please install it through here or refer to the readme!"); Directory.Delete(d); }
            }
            foreach (var mod in Mods) { Log.LogInfo("Installing " + mod); Download(mod, CheckVersion(mod)); }
            Check.Value = false;
            Reset.Value = false;
            Client.Dispose();
        }

        public static long CheckVersion(string mod)
        {
            HttpRequestMessage req = new()
            {
                RequestUri = new Uri($"https://api.mod.io/v1/games/{GameID.Value}/mods?api_key={API_KEY}&name={mod}"),
                Method = HttpMethod.Get
            };
            req.Headers.Add("User-Agent", "Other");
            var t = Client.SendAsync(req).GetAwaiter().GetResult();
            var json = JObject.Parse(t.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var _data = json["data"].Children().ToList();
            if (_data.Count == 0) throw new Exception($"Mod {mod} does not exist!");
            downloadLink = _data[0]["modfile"]["download"]["binary_url"].Value<string>();
            return _data[0]["date_updated"].Value<long>();
        }

        public static void Download(string mod, long version)
        {
            var fname = Path.Combine(Folder, mod + ".zip");
            if (downloadLink == null) version = CheckVersion(mod);
            Log.LogInfo(downloadLink);
            HttpRequestMessage req = new()
            {
                RequestUri = new Uri(downloadLink),
                Method = HttpMethod.Get
            };
            req.Headers.Add("User-Agent", "Other");
            using (var response = Client.SendAsync(req).GetAwaiter().GetResult())
            using (var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            using (var file = File.OpenWrite(fname))
            {
                Log.LogInfo(file);
                stream.CopyToAsync(file).GetAwaiter().GetResult();
                Log.LogInfo("done");
            }
            ZipFile.ExtractToDirectory(fname, Path.Combine(Folder, mod + "=" + version), true);
            Task.Run(() => File.Delete(fname));
            downloadLink = null;
        }
    }
}
