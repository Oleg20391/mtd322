using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MtdVpn;

/// Запасной способ узнать адрес сайта, если DNS провайдера/оператора не отвечает:
/// DNS-over-HTTPS (Cloudflare и Google, запрос идёт сразу на IP).
public static class Doh
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    static readonly string[] Servers = { "https://1.1.1.1/dns-query", "https://8.8.8.8/resolve", "https://dns.google/resolve" };

    public static async Task<IPAddress?> ResolveAsync(string host)
    {
        foreach (var server in Servers)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{server}?name={Uri.EscapeDataString(host)}&type=A");
                req.Headers.Accept.ParseAdd("application/dns-json");
                using var resp = await Http.SendAsync(req);
                var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
                foreach (var a in json?["Answer"] as JsonArray ?? new JsonArray())
                    if (a?["type"]?.GetValue<int>() == 1 && IPAddress.TryParse(a["data"]?.GetValue<string>(), out var ip))
                        return ip;
            }
            catch { /* пробуем следующий сервер */ }
        }
        return null;
    }

    /// HttpClient, который находит адрес через DoH, а TLS проверяет по исходному имени сайта
    public static HttpClient CreateClient(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (ctx, ct) =>
            {
                var host = ctx.DnsEndPoint.Host;
                var ip = IPAddress.TryParse(host, out var parsed) ? parsed : await ResolveAsync(host);
                if (ip == null) throw new HttpRequestException("Не удалось определить адрес " + host);
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(ip, ctx.DnsEndPoint.Port), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        return new HttpClient(handler) { Timeout = timeout };
    }
}
