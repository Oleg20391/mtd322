using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MtdVpn;

public class Profile
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string OutboundsJson { get; set; } = "[]";   // кэш серверов, чтобы работало без обновления подписки
    public DateTime Updated { get; set; }

    public List<JsonObject> GetOutbounds() =>
        (JsonNode.Parse(OutboundsJson) as JsonArray)?.Select(n => n!.AsObject()).ToList() ?? new();

    public List<string> Tags() =>
        GetOutbounds().Select(o => o["tag"]?.GetValue<string>() ?? "").Where(t => t != "").ToList();
}

public class Store
{
    public List<Profile> Profiles { get; set; } = new();
    public int Active { get; set; }
    public string? Server { get; set; }   // null = автовыбор
    public int Mode { get; set; }          // 0 = весь трафик, 1 = только выбранные программы, 2 = все кроме выбранных
    public List<string> Apps { get; set; } = new();   // имена exe, например chrome.exe

    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MtdVpn");
    public static string DataDir => Dir;
    static string FilePath => Path.Combine(Dir, "profiles.json");

    public Profile? Current => Profiles.Count == 0 ? null : Profiles[Math.Clamp(Active, 0, Profiles.Count - 1)];

    public static Store Load()
    {
        try   // перенос настроек из старой папки, чтобы профили не пропали
        {
            var oldDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SushiVpn");
            if (Directory.Exists(oldDir) && !Directory.Exists(Dir)) Directory.Move(oldDir, Dir);
        }
        catch { }
        try { return JsonSerializer.Deserialize<Store>(File.ReadAllText(FilePath)) ?? new Store(); }
        catch { return new Store(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
