using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ECODownloader.Models;
using ECODownloader.Services;

namespace ECODownloader.UI;

public partial class MainWindow : Window
{
    private readonly BancoDetector _detector = new(@"C:\Ecosis");
    private readonly WebLiberacaoClient _client = new();

    private DatabaseInfo? _dbInfo;
    private BuildInfo? _buildInfo;
    private ApiResponse? _apiResponse;
    private string _versao = "";
    private int? _buildNum;
    private ArquivoInfo? _arqExe;
    private ArquivoInfo? _arqSetup;
    private AppConfig _config = new();
    private EcoExeInfo _exeInfo = new();

    private bool _isDark = true;
    private bool _isDetecting;
    private bool _isDownloading;

    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = LogEntries;
        CarregarConfig();
        SetIcon();
        UpdateThemeButton();
        PopularIniList();
        PopularExeList();
        BtnDownloadExe.Click += BtnDownloadExe_Click;
        BtnDownloadSetup.Click += BtnDownloadSetup_Click;
        BtnDownloadBoth.Click += BtnDownloadBoth_Click;
    }

    private void SetIcon()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("ECODownloader.icons.app.ico");
            if (stream != null)
                Icon = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation,
                    BitmapCacheOption.OnLoad);
        }
        catch { }
    }

    private void CarregarConfig()
    {
        _config = ConfigManager.Carregar();
        TxtServer.Text = !string.IsNullOrEmpty(_config.Servidor)
            ? _config.Servidor : "127.0.0.1/3050";
        TxtDatabase.Text = !string.IsNullOrEmpty(_config.Database)
            ? _config.Database : @"\ecosis\dados\ECODADOS_RESTAURADA.ECO";
        TxtUsuario.Text = _config.Usuario;
        TxtSenha.Password = _config.Senha;
        TxtDbUser.Text = !string.IsNullOrEmpty(_config.DbUser)
            ? _config.DbUser : "SYSDBA";
        TxtDbPassword.Password = _config.DbPassword;
        TxtExeFolder.Text = !string.IsNullOrEmpty(_config.PastaExecutaveis)
            ? _config.PastaExecutaveis : @"C:\Ecosis\windows";
        _exeInfo.Executavel = _config.ExecutavelSelecionado;
        _exeInfo.IniFile = _config.IniSelecionado;
    }

    // ═══════════════════════════════════════════════
    //  Executable + INI Selection
    // ═══════════════════════════════════════════════

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog();
        dlg.FolderName = Directory.Exists(TxtExeFolder.Text)
            ? TxtExeFolder.Text : @"C:\Ecosis\windows";
        if (dlg.ShowDialog() == true)
        {
            TxtExeFolder.Text = dlg.FolderName;
            PopularIniList();
            PopularExeList();
        }
    }

    private void PopularExeList()
    {
        CmbExe.Items.Clear();
        var folder = TxtExeFolder.Text.Trim();
        if (!Directory.Exists(folder)) return;

        var exes = Directory.GetFiles(folder, "*.exe")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .OrderBy(f => f)
            .ToList();

        foreach (var exe in exes)
            CmbExe.Items.Add(exe);

        if (!string.IsNullOrEmpty(_exeInfo.Executavel) && CmbExe.Items.Contains(_exeInfo.Executavel))
            CmbExe.SelectedItem = _exeInfo.Executavel;
        else if (CmbExe.Items.Count > 0)
            CmbExe.SelectedIndex = 0;
    }

    private void PopularIniList()
    {
        CmbIni.Items.Clear();
        var folder = TxtExeFolder.Text.Trim();
        if (!Directory.Exists(folder)) return;

        var inis = Directory.GetFiles(folder, "*.ini")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .OrderBy(f => f)
            .ToList();

        foreach (var ini in inis)
            CmbIni.Items.Add(ini);

        // Default to eco.ini, then saved selection, then first
        if (!string.IsNullOrEmpty(_exeInfo.IniFile) && CmbIni.Items.Contains(_exeInfo.IniFile))
            CmbIni.SelectedItem = _exeInfo.IniFile;
        else if (CmbIni.Items.Contains("eco.ini"))
            CmbIni.SelectedItem = "eco.ini";
        else if (CmbIni.Items.Count > 0)
            CmbIni.SelectedIndex = 0;
    }

    private void CmbExe_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbExe.SelectedItem is not string exeName) return;

        _exeInfo.Pasta = TxtExeFolder.Text.Trim();
        _exeInfo.Executavel = exeName;

        // Auto-select matching ini in the combo
        var iniName = Path.GetFileNameWithoutExtension(exeName) + ".ini";
        if (!string.IsNullOrEmpty(iniName) && CmbIni.Items.Contains(iniName))
            CmbIni.SelectedItem = iniName;

        // Read exe version
        var (versao, build) = BancoDetector.ReadExeVersion(_exeInfo.CaminhoExe);
        if (versao != null)
        {
            var buildStr = build.HasValue ? $" (Build #{build})" : "";
            TxtDbStatus.Text = $"📄 {exeName} — v{versao}{buildStr}";
            AppendLog($"✔ {exeName}: v{versao}{buildStr}", LogLevel.Info);
        }
        else
            TxtDbStatus.Text = $"📄 {exeName}";
    }

    private void CmbIni_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbIni.SelectedItem is not string iniName) return;
        if (string.IsNullOrEmpty(TxtExeFolder.Text.Trim())) return;

        _exeInfo.IniFile = iniName;
        var iniPath = Path.Combine(TxtExeFolder.Text.Trim(), iniName);

        if (!File.Exists(iniPath)) return;

        var parsed = BancoDetector.ParseIniFile(iniPath);
        if (parsed.HasValue)
        {
            var (server, port, database) = parsed.Value;
            TxtServer.Text = $"{server}/{port}";
            TxtDatabase.Text = database;
            AppendLog($"✔ INI {iniName}: {server}:{port}{database}", LogLevel.Success);
        }
    }

    // ═══════════════════════════════════════════════
    //  Detection
    // ═══════════════════════════════════════════════

    private async void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetecting) return;
        ResetUi();
        _isDetecting = true;
        SetDetecting(true);

        AppendLog("> Detectando via C:\\Ecosis...", LogLevel.Info);

        // Also populate the exe folder from C:\Ecosis
        TxtExeFolder.Text = @"C:\Ecosis\windows";
        PopularIniList();
        PopularExeList();

        _dbInfo = await Task.Run(() => _detector.Detectar());

        await OnDetectionComplete();
    }

    private async void BtnDetect_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetecting) return;
        ResetUi();

        var rawServer = TxtServer.Text.Trim();
        var rawDb = TxtDatabase.Text.Trim();

        if (string.IsNullOrEmpty(rawServer) || string.IsNullOrEmpty(rawDb))
        {
            AppendLog("✖ Preencha servidor e banco de dados.", LogLevel.Error);
            return;
        }

        string server;
        int port = 3050;
        var sepIdx = rawServer.LastIndexOfAny([':', '/']);
        if (sepIdx >= 0 && int.TryParse(rawServer[(sepIdx + 1)..], out var parsedPort))
        {
            server = rawServer[..sepIdx];
            port = parsedPort;
        }
        else
        {
            server = rawServer;
        }

        if (string.IsNullOrWhiteSpace(server))
        {
            AppendLog("✖ Servidor inválido. Use formato: 127.0.0.1/3050", LogLevel.Error);
            return;
        }

        _isDetecting = true;
        SetDetecting(true);

        AppendLog($"> Conectando a {server}:{port}{rawDb}...", LogLevel.Info);

        var dbUser = TxtDbUser.Text.Trim();
        var dbSenha = TxtDbPassword.Password;
        _dbInfo = await Task.Run(() =>
            _detector.DetectarComParametros(server, port, rawDb, dbUser, dbSenha));

        await OnDetectionComplete();
    }

    private async Task OnDetectionComplete()
    {
        if (_dbInfo is { Sucesso: true })
        {
            _versao = _dbInfo.Versao!;
            _buildNum = _dbInfo.Build;
            TxtDbStatus.Text = $"✔ Conectado — v{_versao}" +
                               (_buildNum.HasValue ? $" (Build #{_buildNum})" : "");
            UpdateVersionCard($"🚀 ECO {_versao}",
                _buildNum.HasValue ? $"Build #{_buildNum}" : "Sem build");
            AppendLog($"✔ Versão detectada: v{_versao}" +
                      (_buildNum.HasValue ? $" (Build #{_buildNum})" : ""), LogLevel.Success);
        }
        else
        {
            TxtDbStatus.Text = $"✖ {_dbInfo?.Erro ?? "Falha na detecção"}";
            UpdateVersionCard("🚀 ECO", "Não detectado");
            AppendLog($"✖ {_dbInfo?.Erro ?? "Falha na detecção"}", LogLevel.Error);
            _isDetecting = false;
            SetDetecting(false);
            return;
        }

        await ProcurarNoSite();
    }

    private async Task ProcurarNoSite()
    {
        var usuario = TxtUsuario.Text.Trim();
        var senha = TxtSenha.Password;

        if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(senha))
        {
            AppendLog("✖ Informe usuário e senha do WebLiberações.", LogLevel.Error);
            _isDetecting = false;
            SetDetecting(false);
            return;
        }

        AppendLog("", LogLevel.None);
        AppendLog("> Conectando ao servidor WebLiberações...", LogLevel.Info);

        try
        {
            var (logou, statusCode) = await _client.LoginAsync(usuario, senha);
            if (!logou)
            {
                AppendLog($"✖ Falha no login (HTTP {statusCode}). Verifique as credenciais.", LogLevel.Error);
                _isDetecting = false;
                SetDetecting(false);
                return;
            }
            AppendLog($"✔ Servidor respondeu HTTP {statusCode}", LogLevel.Info);
            AppendLog("✔ Login autorizado com sucesso!", LogLevel.Success);

            AppendLog("> Obtendo versões disponíveis...", LogLevel.Info);
            _apiResponse = await _client.ObterLiberacoesAsync();

            if (_apiResponse == null)
            {
                AppendLog("✖ Erro ao obter liberações da API.", LogLevel.Error);
                _isDetecting = false;
                SetDetecting(false);
                return;
            }

            AppendLog($"✔ {_apiResponse.Descricao} — {_apiResponse.Grupos.Count} grupo(s) disponíveis",
                LogLevel.Success);

            if (_dbInfo is { Sucesso: true })
                MatchVersion();
            else
                ShowVersionPicker();
        }
        catch (Exception ex)
        {
            AppendLog($"✖ Erro inesperado: {ex.GetType().Name}. Verifique as configurações.", LogLevel.Error);
        }
        finally
        {
            _isDetecting = false;
            SetDetecting(false);
        }
    }

    private void SetDetecting(bool detecting)
    {
        BtnDetect.IsEnabled = !detecting;
        BtnAutoDetect.IsEnabled = !detecting;
        TxtServer.IsEnabled = !detecting;
        TxtDatabase.IsEnabled = !detecting;
        TxtUsuario.IsEnabled = !detecting;
        TxtSenha.IsEnabled = !detecting;
        TxtDbUser.IsEnabled = !detecting;
        TxtDbPassword.IsEnabled = !detecting;
        TxtExeFolder.IsEnabled = !detecting;
        BtnBrowseFolder.IsEnabled = !detecting;
        CmbExe.IsEnabled = !detecting;
        CmbIni.IsEnabled = !detecting;
    }

    private void ResetUi()
    {
        LogEntries.Clear();
        ProgressPanel.Visibility = Visibility.Collapsed;
        DownloadArea.Visibility = Visibility.Collapsed;
        BottomBar.Visibility = Visibility.Collapsed;
        VersionPicker.Visibility = Visibility.Collapsed;
    }

    // ═══════════════════════════════════════════════
    //  Config
    // ═══════════════════════════════════════════════

    private void BtnSalvarCred_Click(object sender, RoutedEventArgs e)
    {
        _config.Usuario = TxtUsuario.Text.Trim();
        _config.Senha = TxtSenha.Password;
        _config.DbUser = TxtDbUser.Text.Trim();
        _config.DbPassword = TxtDbPassword.Password;
        _config.Servidor = TxtServer.Text.Trim();
        _config.Database = TxtDatabase.Text.Trim();
        _config.PastaExecutaveis = TxtExeFolder.Text.Trim();
        _config.ExecutavelSelecionado = _exeInfo.Executavel;
        _config.IniSelecionado = _exeInfo.IniFile;

        ConfigManager.Salvar(_config);
        AppendLog("✔ Credenciais e configurações salvas em config.json", LogLevel.Success);
    }

    // ═══════════════════════════════════════════════
    //  Version Matching
    // ═══════════════════════════════════════════════

    private void MatchVersion()
    {
        if (_apiResponse == null) return;

        foreach (var grupo in _apiResponse.Grupos)
        {
            foreach (var versao in grupo.Versoes)
            {
                if (versao.Versao != _versao) continue;

                BuildInfo? match = null;
                if (_buildNum.HasValue)
                    match = versao.Builds.FirstOrDefault(b => b.Build == _buildNum.Value);
                match ??= versao.Builds.FirstOrDefault(b => b.LinkAtivo && !b.Descontinuado);

                if (match != null)
                {
                    PresentDownload(match);
                    return;
                }
            }
        }

        AppendLog("⚠ Versão exata não encontrada no servidor.", LogLevel.Warning);
        ShowVersionPicker();
    }

    private void ShowVersionPicker()
    {
        if (_apiResponse == null) return;

        VersionPicker.Visibility = Visibility.Visible;
        VersionList.Items.Clear();

        var items = new List<VersionItem>();
        foreach (var grupo in _apiResponse.Grupos)
        {
            foreach (var versao in grupo.Versoes)
            {
                foreach (var build in versao.Builds)
                {
                    if (!build.Disponivel) continue;
                    items.Add(new VersionItem
                    {
                        Versao = versao.Versao,
                        Build = build.Build,
                        BuildInfo = build,
                        Display = $"v{versao.Versao} (Build #{build.Build}) — {build.DataPublicacao}"
                    });
                }
            }
        }
        foreach (var item in items)
            VersionList.Items.Add(item);

        AppendLog($"> Selecione uma versão na lista acima ({items.Count} disponívei(s))",
            LogLevel.Info);
    }

    private void VersionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionList.SelectedItem is not VersionItem item) return;

        _buildInfo = item.BuildInfo;
        _versao = item.Versao;
        _buildNum = item.Build;
        VersionPicker.Visibility = Visibility.Collapsed;

        TxtDbStatus.Text = $"✔ Selecionado — v{_versao} (Build #{_buildNum})";
        AppendLog($"✔ Selecionado: v{_versao} (Build #{_buildNum})", LogLevel.Success);
        PresentDownload(item.BuildInfo);
    }

    // ═══════════════════════════════════════════════
    //  Download Presentation
    // ═══════════════════════════════════════════════

    private void PresentDownload(BuildInfo bi)
    {
        _buildInfo = bi;
        _arqExe = bi.Arquivos.FirstOrDefault(a => a.IsExecutavel);
        _arqSetup = bi.Arquivos.FirstOrDefault(a => a.IsSetup);

        UpdateVersionStatus(bi);

        DownloadArea.Visibility = _arqExe != null || _arqSetup != null
            ? Visibility.Visible : Visibility.Collapsed;

        var dest = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"ECO_v{_versao}");
        TxtDestino.Text = $"📂 {dest}";

        BottomBar.Visibility = _arqExe != null && _arqSetup != null
            ? Visibility.Visible : Visibility.Collapsed;

        AppendLog("✔ Pronto! Escolha o tipo de download acima.", LogLevel.Success);
    }

    // ═══════════════════════════════════════════════
    //  Downloads
    // ═══════════════════════════════════════════════

    private async void BtnDownloadExe_Click(object? sender, RoutedEventArgs e)
    {
        if (_arqExe != null) await DoDownload(_arqExe);
    }

    private async void BtnDownloadSetup_Click(object? sender, RoutedEventArgs e)
    {
        if (_arqSetup != null) await DoDownload(_arqSetup);
    }

    private async void BtnDownloadBoth_Click(object? sender, RoutedEventArgs e)
    {
        if (_arqExe != null) await DoDownload(_arqExe);
        if (_arqSetup != null) await DoDownload(_arqSetup);
    }

    private async Task DoDownload(ArquivoInfo arq)
    {
        if (_isDownloading)
        {
            AppendLog("⏳ Já existe um download em andamento.", LogLevel.Warning);
            return;
        }

        var dest = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"ECO_v{_versao}");
        Directory.CreateDirectory(dest);
        var caminho = Path.Combine(dest, arq.Rar);

        _isDownloading = true;
        SetDownloading(true);

        ProgressPanel.Visibility = Visibility.Visible;
        TxtProgressLabel.Text = $"⬇ Baixando {arq.Rar}...";
        ProgressBar.Value = 0;
        TxtProgressPct.Text = "0%";

        AppendLog($"⬇ Baixando {arq.Rar}...", LogLevel.Info);

        try
        {
            var progress = new Progress<int>(pct =>
            {
                ProgressBar.Value = pct;
                TxtProgressPct.Text = $"{pct}%";
            });

            await _client.BaixarArquivoComProgressoAsync(arq.Filename, caminho, progress);

            var tamanho = new FileInfo(caminho).Length;
            var tamanhoStr = FormatSize(tamanho);
            AppendLog($"✔ {arq.Rar} concluído! ({tamanhoStr})", LogLevel.Success);

            TxtProgressLabel.Text = $"✔ {arq.Rar} — {tamanhoStr}";
            ProgressBar.Value = 100;
            TxtProgressPct.Text = "100%";

            if (arq.IsExecutavel)
                await SubstituirEcoExe(caminho);

            if (arq.IsSetup)
                AppendLog($"ℹ EcoSetup baixado. Execute o instalador manualmente em: {caminho}", LogLevel.Info);
        }
        catch (OperationCanceledException)
        {
            AppendLog("✖ Download cancelado (timeout).", LogLevel.Error);
            TxtProgressLabel.Text = "✖ Download cancelado (timeout)";
        }
        catch
        {
            AppendLog($"✖ Erro ao baixar {arq.Rar}. Verifique a conexão e tente novamente.", LogLevel.Error);
            TxtProgressLabel.Text = $"✖ Erro no download";
        }
        finally
        {
            _isDownloading = false;
            SetDownloading(false);
        }
    }

    private async Task SubstituirEcoExe(string zipPath)
    {
        if (string.IsNullOrEmpty(_exeInfo.Executavel) || string.IsNullOrEmpty(_exeInfo.Pasta))
        {
            AppendLog("⚠ Nenhum executável selecionado. Substituição automática pulada.",
                LogLevel.Warning);
            return;
        }

        var targetDir = _exeInfo.Pasta;
        var targetExe = Path.Combine(targetDir, _exeInfo.Executavel);
        var backupPath = targetExe + ".bak";

        if (!Directory.Exists(targetDir))
        {
            AppendLog($"⚠ Diretório {targetDir} não encontrado. Substituição pulada.",
                LogLevel.Warning);
            return;
        }

        AppendLog($"📦 Extraindo {zipPath}...", LogLevel.Info);

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"ECO_UPDATE_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir));

            var novoExe = Directory.GetFiles(tempDir, "eco.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (novoExe == null)
            {
                AppendLog("⚠ eco.exe não encontrado dentro do zip.", LogLevel.Warning);
                Directory.Delete(tempDir, true);
                return;
            }

            // Rename extracted eco.exe to match the selected exe name
            var renamedExe = Path.Combine(tempDir, _exeInfo.Executavel);
            File.Move(novoExe, renamedExe);
            AppendLog($"📝 Renomeado para {_exeInfo.Executavel}", LogLevel.Info);

            // Backup old exe
            if (File.Exists(targetExe))
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Move(targetExe, backupPath);
                AppendLog($"💾 Backup: {backupPath}", LogLevel.Info);
            }

            File.Copy(renamedExe, targetExe, true);
            AppendLog($"✅ {_exeInfo.Executavel} substituído em {targetExe}", LogLevel.Success);

            Directory.Delete(tempDir, true);
        }
        catch
        {
            AppendLog($"✖ Erro ao substituir {_exeInfo.Executavel}. Verifique permissões e disco.", LogLevel.Error);
        }
    }

    private void SetDownloading(bool downloading)
    {
        BtnDownloadExe.IsEnabled = !downloading;
        BtnDownloadSetup.IsEnabled = !downloading;
        BtnDownloadBoth.IsEnabled = !downloading;
    }

    // ═══════════════════════════════════════════════
    //  UI Helpers
    // ═══════════════════════════════════════════════

    private static string FormatSize(long bytes) => bytes switch
    {
        > 1_000_000_000 => $"{bytes / 1_000_000_000.0:F2} GB",
        > 1_000_000 => $"{bytes / 1_000_000.0:F2} MB",
        > 1_000 => $"{bytes / 1_000.0:F2} KB",
        _ => $"{bytes} B"
    };

    private void UpdateVersionCard(string title, string subtitle)
    {
        TxtVersion.Text = title;
        TxtStatus.Text = subtitle;
    }

    private void UpdateVersionStatus(BuildInfo bi)
    {
        var tags = new List<string>();
        if (bi.AtualizacaoObrigatoria) tags.Add("🔴 Obrigatória");
        if (bi.AtualizacaoRecomendada) tags.Add("🟡 Recomendada");
        if (bi.UltimaVersao) tags.Add("🟢 Última");
        if (bi.LinkAtivo && !bi.Descontinuado) tags.Add("✅ Liberado");

        var status = tags.Count > 0 ? string.Join(" · ", tags) : "✅ Liberado";
        TxtStatus.Text = $"{status}  |  {bi.DataPublicacao}";
        TxtVersion.Text = $"🚀 ECO {bi.Versao}   Build #{bi.Build}";
        _versao = bi.Versao;
        _buildNum = bi.Build;
    }

    private void AppendLog(string message, LogLevel level)
    {
        if (message.Length == 0)
        {
            LogEntries.Add(new LogEntry { Text = " ", Foreground = GetBrush("TextSecBrush", Colors.Gray) });
            return;
        }

        var color = level switch
        {
            LogLevel.Success => GetBrush("SuccessBrush", Colors.LimeGreen),
            LogLevel.Warning => GetBrush("WarnBrush", Colors.Orange),
            LogLevel.Error => GetBrush("ErrorBrush", Colors.Red),
            LogLevel.Info => GetBrush("TextSecBrush", Colors.Gray),
            _ => GetBrush("TextSecBrush", Colors.Gray)
        };

        LogEntries.Add(new LogEntry { Text = message, Foreground = color });

        if (LogEntries.Count > 500)
            LogEntries.RemoveAt(0);

        if (LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private SolidColorBrush GetBrush(string key, Color fallback)
    {
        return FindResource(key) as SolidColorBrush ?? new SolidColorBrush(fallback);
    }

    private void UpdateThemeButton()
    {
        BtnTheme.Content = _isDark ? "🌙  Tema escuro" : "☀️  Tema claro";
    }

    // ═══════════════════════════════════════════════
    //  Events
    // ═══════════════════════════════════════════════

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        _isDark = !_isDark;
        var uri = _isDark ? "UI/Themes/Dark.xaml" : "UI/Themes/Light.xaml";
        var dict = new ResourceDictionary
        {
            Source = new Uri(uri, UriKind.Relative)
        };
        Application.Current.Resources.MergedDictionaries[0] = dict;
        UpdateThemeButton();
    }

    protected override void OnClosed(EventArgs e)
    {
        _client.Dispose();
        base.OnClosed(e);
    }
}

// ═══════════════════════════════════════════════════
//  Supporting Types
// ═══════════════════════════════════════════════════

public enum LogLevel { None, Info, Success, Warning, Error }

public class LogEntry
{
    public string Text { get; set; } = "";
    public SolidColorBrush Foreground { get; set; } = new(Colors.Gray);
}

public class VersionItem
{
    public string Versao { get; set; } = "";
    public int Build { get; set; }
    public BuildInfo BuildInfo { get; set; } = null!;
    public string Display { get; set; } = "";
}
