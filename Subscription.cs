using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MtdVpn;

public static class Subscription
{
    static readonly string[] Supported = { "vless", "hysteria2", "hysteria", "vmess", "trojan", "shadowsocks", "tuic" };

    static async Task<string> GetTextAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            // По User-Agent Remnawave отдаёт готовый sing-box конфиг
            http.DefaultRequestHeaders.UserAgent.ParseAdd("sing-box/1.12.0");
            return await http.GetStringAsync(url);
        }
        catch (HttpRequestException)
        {
            // обычный DNS не справился (например, мобильный оператор) — пробуем через DoH
            using var http = Doh.CreateClient(TimeSpan.FromSeconds(25));
            http.DefaultRequestHeaders.UserAgent.ParseAdd("sing-box/1.12.0");
            return await http.GetStringAsync(url);
        }
    }

    public static async Task RefreshAsync(Profile p)
    {
        string text = await GetTextAsync(p.Url);

        JsonArray? all;
        try { all = JsonNode.Parse(text)?["outbounds"] as JsonArray; }
        catch { throw new Exception("Подписка вернула не sing-box конфиг. Проверьте ссылку и что в Remnawave включён sing-box формат."); }

        var servers = new JsonArray();
        foreach (var n in all ?? new JsonArray())
        {
            var o = n as JsonObject;
            var type = o?["type"]?.GetValue<string>();
            if (o != null && type != null && Supported.Contains(type))
                servers.Add(o.DeepClone());
        }
        if (servers.Count == 0) throw new Exception("В подписке не найдено ни одного сервера.");

        p.OutboundsJson = servers.ToJsonString();
        p.Updated = DateTime.Now;
    }
}
