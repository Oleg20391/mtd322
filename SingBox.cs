using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MtdVpn;

public class SingBox
{
    Process? _p;
    readonly StringBuilder _log = new();
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public bool IsRunning => _p is { HasExited: false };

    public static void KillAll()
    {
        foreach (var p in Process.GetProcessesByName("sing-box"))
            try { p.Kill(true); } catch { }
    }

    public async Task StartAsync(string config)
    {
        Stop();
        await Task.Delay(800);   // даём Windows освободить TUN-адаптер после остановки
        Exception? last = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try { await StartOnceAsync(config); return; }
            catch (Exception ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("take too much time"))
            {
                last = ex;
                Stop();
                await Task.Delay(2500);   // адаптер ещё занят: ждём и пробуем снова
            }
        }
        throw last!;
    }

    async Task StartOnceAsync(string config)
    {
        Stop();
        var exe = Path.Combine(AppContext.BaseDirectory, "sing-box.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("Не найден sing-box.exe рядом с программой.");

        Directory.CreateDirectory(Store.DataDir);
        var cfgPath = Path.Combine(Store.DataDir, "config.json");
        File.WriteAllText(cfgPath, config);

        _log.Clear();
        var psi = new ProcessStartInfo(exe, $"run -c \"{cfgPath}\"")
        {
            WorkingDirectory = Store.DataDir,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        _p = Process.Start(psi) ?? throw new Exception("Не удалось запустить ядро.");
        _p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_log) _log.AppendLine(e.Data); };
        _p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (_log) _log.AppendLine(e.Data); };
        _p.BeginErrorReadLine();
        _p.BeginOutputReadLine();

        // ждём, пока поднимется API ядра (значит, туннель создан)
        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(250);
            if (_p.HasExited) throw new Exception("Ядро завершилось с ошибкой:\n" + LastLog());
            try { await Http.GetAsync("http://127.0.0.1:9090/version"); return; } catch { }
        }
        Stop();
        throw new Exception("Ядро не запустилось за 15 секунд:\n" + LastLog());
    }

    public void Stop()
    {
        try { if (_p is { HasExited: false }) { _p.Kill(true); _p.WaitForExit(3000); } } catch { }
        _p = null;
    }

    /// Какой сервер сейчас выбран автовыбором
    public async Task<string?> ActiveAsync()
    {
        try
        {
            var json = await Http.GetStringAsync("http://127.0.0.1:9090/proxies/auto");
            return JsonNode.Parse(json)?["now"]?.GetValue<string>();
        }
        catch { return null; }
    }

    public string FullLog() { lock (_log) return _log.ToString(); }

    string LastLog()
    {
        lock (_log) return string.Join("\n", _log.ToString().Split('\n').TakeLast(8));
    }
}
