using System.Collections.Generic;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MtdVpn;

public static class ConfigBuilder
{
    /// DNS-сервер обычной сети компьютера (роутер/провайдер), берётся до запуска туннеля
    static string? GetSystemDns()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var props = ni.GetIPProperties();
                bool hasGateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                if (!hasGateway) continue;
                var dns = props.DnsAddresses.FirstOrDefault(d =>
                    d.AddressFamily == AddressFamily.InterNetwork && !d.ToString().StartsWith("198.18."));
                if (dns != null) return dns.ToString();
            }
        }
        catch { }
        return null;
    }

    /// Заменяет доменные адреса серверов на IP (резолв системным DNS ДО старта туннеля),
    /// чтобы подключение не зависело от DNS-серверов в туннеле.
    public static async Task<List<JsonObject>> ResolveServersAsync(List<JsonObject> servers)
    {
        var result = new List<JsonObject>();
        foreach (var src in servers)
        {
            var o = (JsonObject)src.DeepClone();
            var host = o["server"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(host) && !IPAddress.TryParse(host, out _))
            {
                IPAddress? ip = null;
                try
                {
                    var addrs = await Dns.GetHostAddressesAsync(host).WaitAsync(TimeSpan.FromSeconds(6));
                    ip = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                }
                catch { }
                ip ??= await Doh.ResolveAsync(host);   // запасной вариант: DNS-over-HTTPS
                if (ip != null)
                {
                    if (o["tls"] is JsonObject tls && tls["enabled"]?.GetValue<bool>() == true && tls["server_name"] == null)
                        tls["server_name"] = host;   // SNI не должен потеряться
                    o["server"] = ip.ToString();
                }
            }
            result.Add(o);
        }
        return result;
    }

    /// serverTag == null -> автовыбор (urltest с переключением при падении сервера)
    public static string Build(List<JsonObject> servers, string? serverTag, int mode, List<string> apps)
    {
        var outbounds = new JsonArray();
        var tags = new List<string>();
        int i = 0;
        foreach (var s in servers)
        {
            var o = (JsonObject)s.DeepClone();
            var tag = o["tag"]?.GetValue<string>();
            if (string.IsNullOrEmpty(tag) || tags.Contains(tag)) { tag = $"server-{++i}"; o["tag"] = tag; }
            tags.Add(tag);
            outbounds.Add(o);
        }

        outbounds.Add(new JsonObject
        {
            ["type"] = "urltest",
            ["tag"] = "auto",
            ["outbounds"] = new JsonArray(tags.Select(t => (JsonNode)t).ToArray()),
            ["url"] = "https://www.gstatic.com/generate_204",
            ["interval"] = "30s",
            ["tolerance"] = 100,
            ["idle_timeout"] = "30m"
        });
        outbounds.Add(new JsonObject { ["type"] = "direct", ["tag"] = "direct" });

        var final = serverTag != null && tags.Contains(serverTag) ? serverTag : "auto";

        var rules = new JsonArray
        {
            new JsonObject { ["action"] = "sniff" },
            new JsonObject { ["protocol"] = "dns", ["action"] = "hijack-dns" },
            new JsonObject { ["ip_is_private"] = true, ["outbound"] = "direct" }
        };
        bool proxyMode = mode == 3;   // режим без TUN: системный прокси
        bool split = (mode == 1 || mode == 2) && apps.Count > 0;
        if (split)
            rules.Add(new JsonObject
            {
                ["process_name"] = new JsonArray(apps.Select(a => (JsonNode)a).ToArray()),
                ["outbound"] = mode == 1 ? final : "direct"
            });
        rules.Add(new JsonObject { ["domain_suffix"] = new JsonArray("bot.nu", "ru", "xn--p1ai"), ["outbound"] = "direct" });
        var routeFinal = mode == 1 && split ? "direct" : final;

        // DNS: программы, которые идут напрямую, получают DNS своей обычной сети, а не через VPN
        var dnsRules = new JsonArray();
        string dnsFinal = "dns-remote";
        if (proxyMode)
            dnsRules.Add(new JsonObject { ["domain_suffix"] = new JsonArray("bot.nu", "ru", "xn--p1ai"), ["action"] = "route", ["server"] = "dns-direct" });
        if (split)
        {
            dnsRules.Add(new JsonObject
            {
                ["process_name"] = new JsonArray(apps.Select(a => (JsonNode)a).ToArray()),
                ["action"] = "route",
                ["server"] = mode == 1 ? "dns-remote" : "dns-direct"
            });
            if (mode == 1) dnsFinal = "dns-direct";
        }
        var dnsSection = new JsonObject
        {
            ["servers"] = new JsonArray
            {
                new JsonObject { ["type"] = "https", ["tag"] = "dns-remote", ["server"] = "1.1.1.1", ["detour"] = final },
                new JsonObject { ["type"] = "udp", ["tag"] = "dns-direct", ["server"] = GetSystemDns() ?? "77.88.8.8" }
            },
            ["final"] = dnsFinal,
            ["strategy"] = "ipv4_only"
        };
        if (dnsRules.Count > 0) dnsSection["rules"] = dnsRules;

        JsonArray inbounds = proxyMode
            ? new JsonArray { new JsonObject { ["type"] = "mixed", ["tag"] = "mixed-in", ["listen"] = "127.0.0.1", ["listen_port"] = 10808 } }
            : new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "tun", ["tag"] = "tun-in", ["interface_name"] = "MtdVPN",
                    ["address"] = new JsonArray("198.18.0.1/30"),
                    ["auto_route"] = true, ["strict_route"] = false, ["stack"] = "mixed",
                    // локальная сеть и служебные адреса в туннель не заворачиваем
                    ["route_exclude_address"] = new JsonArray(
                        "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "169.254.0.0/16", "224.0.0.0/4", "255.255.255.255/32")
                }
            };

        var cfg = new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "info", ["timestamp"] = true },
            ["dns"] = dnsSection,
            ["inbounds"] = inbounds,
            ["outbounds"] = outbounds,
            ["route"] = new JsonObject
            {
                ["auto_detect_interface"] = true,
                ["default_domain_resolver"] = "dns-direct",
                ["final"] = routeFinal,
                ["rules"] = rules
            },
            ["experimental"] = new JsonObject
            {
                ["clash_api"] = new JsonObject { ["external_controller"] = "127.0.0.1:9090" }
            }
        };
        return cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
