using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MtdVpn;

/// Проверяет последний релиз на GitHub и ставит его тихо
public static class Updater
{
    const string Repo = "Oleg20391/mtd322";
    static readonly HttpClient Http = Create();

    static HttpClient Create()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("MtdVpn-Updater");
        return h;
    }

    public static Version Current => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    static Version Norm(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    /// Если вышла новая версия, скачивает установщик и возвращает (версия, путь). Иначе null.
    public static async Task<(string Version, string Path)?> DownloadIfNewAsync(Action<string>? status = null)
    {
        var json = await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        var rel = JsonNode.Parse(json);
        var tag = rel?["tag_name"]?.GetValue<string>()?.TrimStart('v', 'V');
        if (tag == null || !Version.TryParse(tag, out var latest)) return null;
        if (Norm(latest) <= Norm(Current)) return null;

        var asset = (rel!["assets"] as JsonArray)?
            .FirstOrDefault(a => a?["name"]?.GetValue<string>() == "MtdVpn-Setup.exe");
        var url = asset?["browser_download_url"]?.GetValue<string>();
        if (url == null) return null;

        status?.Invoke($"Загружаю обновление {tag}…");
        var path = Path.Combine(Path.GetTempPath(), "MtdVpn-Setup-update.exe");
        await using (var src = await Http.GetStreamAsync(url))
        await using (var dst = File.Create(path))
            await src.CopyToAsync(dst);

        // проверка целостности, если GitHub отдал контрольную сумму
        var digest = asset?["digest"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(digest) && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            using var sha = SHA256.Create();
            await using var fs = File.OpenRead(path);
            var actual = Convert.ToHexString(await sha.ComputeHashAsync(fs));
            if (!actual.Equals(digest[7..], StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch { }
                throw new Exception("Контрольная сумма обновления не совпала.");
            }
        }
        return (tag, path);
    }

    public static void RunInstaller(string path) =>
        Process.Start(new ProcessStartInfo(path, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /UPDATED=1") { UseShellExecute = true });
}
