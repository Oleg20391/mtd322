using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MtdVpn;

public partial class MainWindow : Window
{
    const string AutoLabel = "Авто (лучший доступный)";

    readonly Store _store = Store.Load();
    readonly SingBox _core = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    System.Windows.Forms.NotifyIcon? _tray;
    readonly DispatcherTimer _updTimer = new() { Interval = TimeSpan.FromHours(3) };
    bool _busy, _loading, _exit, _updating;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) => await TickAsync();
        SetupTray();
        ShowCorrectPanel();

        Title = $"MTD VPN {Updater.Current.ToString(3)}";
        _updTimer.Tick += async (_, _) => await CheckUpdateAsync();
        _updTimer.Start();
        Loaded += async (_, _) => { await Task.Delay(8000); await CheckUpdateAsync(); };
    }

    // ---------- Автообновление ----------
    async Task CheckUpdateAsync()
    {
        // не трогаем работающее соединение: обновляемся только когда VPN выключен
        if (_updating || _busy || Switch.IsChecked == true) return;
        _updating = true;
        try
        {
            var upd = await Updater.DownloadIfNewAsync(t => TxtServer.Text = t);
            if (upd == null) return;
            if (Switch.IsChecked == true || _busy) { TxtServer.Text = ""; return; }   // успели подключиться, обновимся позже
            SetStatus($"Обновляюсь до {upd.Value.Version}…");
            Updater.RunInstaller(upd.Value.Path);
            ExitApp();
        }
        catch { TxtServer.Text = ""; }
        finally { _updating = false; }
    }

    // ---------- Экраны ----------
    void ShowCorrectPanel()
    {
        bool has = _store.Current != null;
        WelcomePanel.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        MainPanel.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        if (has) { FillProfiles(); FillServers(); FillMode(); }
    }

    void FillProfiles()
    {
        _loading = true;
        CmbProfile.ItemsSource = _store.Profiles.Select(p => p.Name).ToList();
        CmbProfile.SelectedIndex = Math.Clamp(_store.Active, 0, _store.Profiles.Count - 1);
        _loading = false;
    }

    void FillServers()
    {
        _loading = true;
        var items = new[] { AutoLabel }.Concat(_store.Current!.Tags()).ToList();
        CmbServer.ItemsSource = items;
        TxtCount.Text = $"Найдено серверов: {items.Count - 1}";
        int idx = _store.Server == null ? 0 : items.IndexOf(_store.Server);
        CmbServer.SelectedIndex = idx < 0 ? 0 : idx;
        if (idx < 0) _store.Server = null;
        _loading = false;
    }

    void FillMode()
    {
        _loading = true;
        CmbMode.SelectedIndex = Math.Clamp(_store.Mode, 0, 3);
        LstApps.ItemsSource = _store.Apps.ToList();
        AppsPanel.Visibility = _store.Mode is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;
        TxtHint.Visibility = _store.Mode == 3 ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;
    }

    async void CmbMode_Changed(object s, SelectionChangedEventArgs e)
    {
        if (_loading || CmbMode.SelectedIndex < 0) return;
        _store.Mode = CmbMode.SelectedIndex; _store.Save();
        FillMode();
        if (Switch.IsChecked == true) await ReconnectAsync();
    }

    async void BtnAddApp_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Программы (*.exe)|*.exe", Multiselect = true, Title = "Выберите программы" };
        if (dlg.ShowDialog() != true) return;
        foreach (var path in dlg.FileNames)
        {
            var name = System.IO.Path.GetFileName(path);
            if (!_store.Apps.Contains(name, StringComparer.OrdinalIgnoreCase)) _store.Apps.Add(name);
        }
        _store.Save(); FillMode();
        if (Switch.IsChecked == true) await ReconnectAsync();
    }

    sealed class RunningApp
    {
        public string Exe { get; set; } = "";
        public string Title { get; set; } = "";
        public override string ToString() => $"{Exe}   —   {Title}";
    }

    async void BtnPickRunning_Click(object s, RoutedEventArgs e)
    {
        var apps = Process.GetProcesses()
            .Where(p => { try { return !string.IsNullOrWhiteSpace(p.MainWindowTitle); } catch { return false; } })
            .Select(p => new RunningApp { Exe = p.ProcessName + ".exe", Title = p.MainWindowTitle })
            .Where(a => !a.Exe.Equals("MtdVpn.exe", StringComparison.OrdinalIgnoreCase)
                     && !a.Exe.Equals("sing-box.exe", StringComparison.OrdinalIgnoreCase))
            .GroupBy(a => a.Exe, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(a => a.Exe).ToList();

        if (apps.Count == 0) { MessageBox.Show("Не найдено запущенных программ с окнами."); return; }

        var list = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = apps,
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x2B)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        var ok = new Button { Content = "Добавить выбранные", Padding = new Thickness(12), Margin = new Thickness(10) };
        var panel = new DockPanel();
        DockPanel.SetDock(ok, Dock.Bottom);
        panel.Children.Add(ok);
        panel.Children.Add(list);
        var win = new Window
        {
            Title = "Запущенные программы (можно выбрать несколько)",
            Width = 520, Height = 460, Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x1C)),
            Content = panel
        };
        ok.Click += (_, _) => win.DialogResult = true;
        if (win.ShowDialog() != true) return;

        foreach (var a in list.SelectedItems.Cast<RunningApp>())
            if (!_store.Apps.Contains(a.Exe, StringComparer.OrdinalIgnoreCase)) _store.Apps.Add(a.Exe);
        _store.Save(); FillMode();
        if (Switch.IsChecked == true) await ReconnectAsync();
    }

    async void BtnRemoveApp_Click(object s, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is not string name) return;
        _store.Apps.Remove(name); _store.Save(); FillMode();
        if (Switch.IsChecked == true) await ReconnectAsync();
    }

    void SetStatus(string text, bool ok = false)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = ok ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)) : Brushes.White;
    }

    // ---------- Профили ----------
    async Task<bool> AddProfileAsync(string url)
    {
        url = url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            MessageBox.Show("Это не похоже на ссылку. Скопируйте её целиком, вместе с https://");
            return false;
        }
        var p = new Profile { Url = url, Name = uri.Host };
        try { await Subscription.RefreshAsync(p); }
        catch (Exception ex) { MessageBox.Show("Не удалось получить подписку:\n" + ex.Message); return false; }

        if (_store.Profiles.Any(x => x.Name == p.Name)) p.Name += $" ({_store.Profiles.Count + 1})";
        _store.Profiles.Add(p);
        _store.Active = _store.Profiles.Count - 1;
        _store.Server = null;
        _store.Save();
        return true;
    }

    async void BtnContinue_Click(object s, RoutedEventArgs e)
    {
        BtnContinue.IsEnabled = false;
        BtnContinue.Content = "Проверяю…";
        if (await AddProfileAsync(TxtUrl.Text)) { TxtUrl.Clear(); ShowCorrectPanel(); }
        BtnContinue.Content = "Продолжить";
        BtnContinue.IsEnabled = true;
    }

    void BtnShowAdd_Click(object s, RoutedEventArgs e) =>
        AddPanel.Visibility = AddPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    async void BtnAdd_Click(object s, RoutedEventArgs e)
    {
        if (!await AddProfileAsync(TxtNewUrl.Text)) return;
        TxtNewUrl.Clear();
        AddPanel.Visibility = Visibility.Collapsed;
        FillProfiles(); FillServers();
        if (Switch.IsChecked == true) await ReconnectAsync();
    }

    async void BtnDelete_Click(object s, RoutedEventArgs e)
    {
        var p = _store.Current;
        if (p == null) return;
        if (MessageBox.Show($"Удалить профиль {p.Name}?", "MTD VPN", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        if (Switch.IsChecked == true) await DisconnectAsync();
        _store.Profiles.Remove(p);
        _store.Active = 0; _store.Server = null; _store.Save();
        ShowCorrectPanel();
    }

    async void CmbProfile_Changed(object s, SelectionChangedEventArgs e)
    {
        if (_loading || CmbProfile.SelectedIndex < 0) return;
        _store.Active = CmbProfile.SelectedIndex; _store.Server = null; _store.Save();
        FillServers();
        if (Switch.IsChecked == true) await ReconnectAsync();
    }

    async void CmbServer_Changed(object s, SelectionChangedEventArgs e)
    {
        if (_loading || CmbServer.SelectedIndex < 0) return;
        _store.Server = CmbServer.SelectedIndex == 0 ? null : (string)CmbServer.SelectedItem;
        _store.Save();
        if (Switch.IsChecked == true) await ReconnectAsync();
    }

    void BtnAuto_Click(object s, RoutedEventArgs e) => CmbServer.SelectedIndex = 0;

    // ---------- Подключение ----------
    async void Switch_Click(object s, RoutedEventArgs e)
    {
        if (_busy) { Switch.IsChecked = !Switch.IsChecked; return; }
        if (Switch.IsChecked == true) await ConnectAsync(); else await DisconnectAsync();
    }

    async Task ConnectAsync()
    {
        var p = _store.Current;
        if (p == null) { Switch.IsChecked = false; return; }
        _busy = true; Switch.IsEnabled = false; SetStatus("Подключаюсь…");
        try
        {
            // обновляем подписку; если сайт недоступен, берём сохранённые серверы
            try { await Subscription.RefreshAsync(p); _store.Save(); FillServers(); } catch { }

            var servers = p.GetOutbounds();
            if (servers.Count == 0) throw new Exception("В профиле нет серверов.");
            servers = await ConfigBuilder.ResolveServersAsync(servers);
            if (_store.Mode == 1 && _store.Apps.Count == 0)
                throw new Exception("Выбран режим «только выбранные программы», но список пуст. Добавьте программу в разделе «Туннелирование».");
            await _core.StartAsync(ConfigBuilder.Build(servers, _store.Server, _store.Mode, _store.Apps));
            if (_store.Mode == 3) SystemProxy.Set();

            Switch.IsChecked = true;
            SetStatus("Подключено", ok: true);
            _timer.Start();
            await TickAsync();
        }
        catch (Exception ex)
        {
            SystemProxy.Clear(true);
            Switch.IsChecked = false;
            SetStatus("Не удалось подключиться");
            TxtServer.Text = "";
            MessageBox.Show(ex.Message, "MTD VPN");
        }
        finally { _busy = false; Switch.IsEnabled = true; }
    }

    async Task DisconnectAsync()
    {
        _busy = true; Switch.IsEnabled = false;
        _timer.Stop();
        await Task.Run(() => _core.Stop());
        SystemProxy.Clear(true);
        Switch.IsChecked = false;
        SetStatus("Отключено"); TxtServer.Text = "";
        _busy = false; Switch.IsEnabled = true;
    }

    async Task ReconnectAsync()
    {
        await DisconnectAsync();
        Switch.IsChecked = true;
        await ConnectAsync();
    }

    async Task TickAsync()
    {
        if (_busy || Switch.IsChecked != true) return;
        if (!_core.IsRunning)
        {
            _timer.Stop(); SystemProxy.Clear(true); Switch.IsChecked = false;
            SetStatus("Соединение прервано"); TxtServer.Text = "";
            return;
        }
        string? now = _store.Server ?? await _core.ActiveAsync();
        TxtServer.Text = now == null ? "" : "Сервер: " + now;
    }

    void BtnLog_Click(object s, RoutedEventArgs e)
    {
        var text = _core.FullLog();
        var tb = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(text) ? "(журнал пуст: подключение ещё не запускалось)" : text,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x1C)),
            Foreground = Brushes.White
        };
        tb.Loaded += (_, _) => tb.ScrollToEnd();
        new Window { Title = "Журнал ядра", Width = 720, Height = 460, Content = tb, Owner = this }.Show();
    }

    // ---------- Трей ----------
    void SetupTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            Text = "MTD VPN",
            Visible = true
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowMe());
        menu.Items.Add("Выход", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowMe();
    }

    void ShowMe() { Show(); WindowState = WindowState.Normal; Activate(); }

    void ExitApp()
    {
        _exit = true;
        _core.Stop();
        SystemProxy.Clear(true);
        _tray?.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exit) { e.Cancel = true; Hide(); }   // крестик сворачивает в трей, VPN продолжает работать
        base.OnClosing(e);
    }
}
