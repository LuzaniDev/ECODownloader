using System.Diagnostics;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;
using ECODownloader.Models;

namespace ECODownloader.Services;

public partial class BancoDetector
{
    private readonly string? _ecosisPath;

    public BancoDetector(string? ecosisPath = null)
    {
        _ecosisPath = ecosisPath;
    }

    public static (string? Versao, int? Build) ReadExeVersion(string exePath)
    {
        if (!File.Exists(exePath))
            return (null, null);
        try
        {
            var ver = FileVersionInfo.GetVersionInfo(exePath);
            var fileVer = ver.FileVersion ?? "";
            var parts = fileVer.Split('.');
            if (parts.Length >= 3)
            {
                var v = $"{parts[0]}.{parts[1]}.{parts[2]}";
                int? b = parts.Length >= 4 && int.TryParse(parts[3], out var build) ? build : null;
                return (v, b);
            }
        }
        catch { }
        return (null, null);
    }

    public static (string Server, int Port, string Database)? ParseIniFile(string iniPath)
    {
        if (!File.Exists(iniPath))
            return null;
        try
        {
            var lines = File.ReadAllLines(iniPath);
            var dadosLine = lines.FirstOrDefault(l =>
                l.StartsWith("dados=", StringComparison.OrdinalIgnoreCase));
            if (dadosLine == null) return null;

            var raw = dadosLine.Split('=', 2)[1].Trim();
            var match = Regex.Match(raw,
                @"^(?<server>[^/]+?)(?:/(?<port>\d+))?:(?<database>.+)$");
            if (!match.Success) return null;

            var server = match.Groups["server"].Value;
            var port = match.Groups["port"].Success
                ? int.Parse(match.Groups["port"].Value) : 3050;
            var database = match.Groups["database"].Value;
            return (server, port, database);
        }
        catch { return null; }
    }

    public DatabaseInfo Detectar()
    {
        if (_ecosisPath == null)
            return new DatabaseInfo { Sucesso = false, Erro = "Caminho Ecosis não configurado" };

        var dbInfo = TentarFirebird();
        if (dbInfo.Sucesso) return dbInfo;

        dbInfo = TentarDosExecutaveis();
        if (dbInfo.Sucesso) return dbInfo;

        return dbInfo;
    }

    public DatabaseInfo DetectarComParametros(string server, int port, string database,
        string usuario = "SYSDBA", string senha = "masterkey")
    {
        var csb = new FbConnectionStringBuilder
        {
            DataSource = server,
            Port = port,
            Database = database,
            UserID = usuario,
            Password = senha,
            Pooling = false,
            ConnectionTimeout = 10
        };

        var fbClient = LocateFbClient();
        if (fbClient != null)
            csb.ClientLibrary = fbClient;

        return QueryDatabase(csb);
    }

    private static string? LocateFbClient()
    {
        var candidates = new[]
        {
            @"C:\Ecosis\windows\fbclient.dll",
            @"C:\Program Files\Firebird\Firebird_2_5\bin\fbclient.dll",
            @"C:\Program Files (x86)\Firebird\Firebird_2_5\bin\fbclient.dll",
            @"C:\Program Files\Firebird\Firebird_3_0\fbclient.dll",
            @"C:\Program Files (x86)\Firebird\Firebird_3_0\fbclient.dll",
            @"C:\Program Files\Firebird\Firebird_4_0\fbclient.dll",
            @"C:\Program Files (x86)\Firebird\Firebird_4_0\fbclient.dll",
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private DatabaseInfo TentarFirebird()
    {
        var iniPath = Path.Combine(_ecosisPath!, "windows", "eco.ini");
        if (!File.Exists(iniPath))
            return new DatabaseInfo { Sucesso = false, Erro = "eco.ini não encontrado" };

        try
        {
            var lines = File.ReadAllLines(iniPath);
            var dadosLine = lines.FirstOrDefault(l => l.StartsWith("dados=", StringComparison.OrdinalIgnoreCase));
            if (dadosLine == null)
                return new DatabaseInfo { Sucesso = false, Erro = "Linha 'dados=' não encontrada" };

            var rawValue = dadosLine.Split('=', 2)[1].Trim();
            var fbClientPath = ObterFbClientPath(lines);

            return ConsultarFirebird(rawValue, fbClientPath);
        }
        catch (Exception ex)
        {
            return new DatabaseInfo { Sucesso = false, Erro = $"Erro ao ler eco.ini: {ex.Message}" };
        }
    }

    private string? ObterFbClientPath(string[] lines)
    {
        var fbLine = lines.FirstOrDefault(l => l.StartsWith("Firebird=", StringComparison.OrdinalIgnoreCase));
        if (fbLine == null) return null;

        var path = fbLine.Split('=', 2)[1].Trim();
        return File.Exists(path) ? path : null;
    }

    private DatabaseInfo ConsultarFirebird(string rawValue, string? fbClientPath,
        string usuario = "SYSDBA", string senha = "masterkey")
    {
        var parsed = ParseConexao(rawValue);
        if (parsed == null)
            return new DatabaseInfo { Sucesso = false, Erro = $"Formato de conexão inválido" };

        var csb = new FbConnectionStringBuilder
        {
            DataSource = parsed.Value.Server,
            Port = parsed.Value.Port,
            Database = parsed.Value.Database,
            UserID = usuario,
            Password = senha,
            Pooling = false,
            ConnectionTimeout = 10
        };

        if (fbClientPath != null)
            csb.ClientLibrary = fbClientPath;

        return QueryDatabase(csb);
    }

    private static DatabaseInfo QueryDatabase(FbConnectionStringBuilder csb)
    {
        try
        {
            using var conn = new FbConnection(csb.ConnectionString);
            conn.Open();

            try
            {
                using var cmd = new FbCommand(
                    "SELECT FIRST 1 VERSAO FROM GERDBUPDATETERMINAL ORDER BY GID DESC", conn);
                var result = cmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(result))
                {
                    var parts = result.Split('.');
                    var versao = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : result;
                    var build = parts.Length >= 4 && int.TryParse(parts[3], out var b) ? b : (int?)null;
                    return new DatabaseInfo
                    {
                        Versao = versao,
                        Build = build,
                        DatabasePath = csb.Database,
                        Sucesso = true
                    };
                }
            }
            catch { }

            try
            {
                using var cmd = new FbCommand(
                    "SELECT FIRST 1 VERSAO FROM TGERLICENCA ORDER BY NUMERO DESC", conn);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    var str = result.ToString()!;
                    if (int.TryParse(str, out var encoded) && encoded > 0)
                    {
                        var versao = $"{encoded / 10000000}.{(encoded / 100000) % 100}.{(encoded / 100) % 1000}";
                        var build = encoded % 100;
                        return new DatabaseInfo
                        {
                            Versao = versao,
                            Build = build == 0 ? null : build,
                            DatabasePath = csb.Database,
                            Sucesso = true
                        };
                    }
                }
            }
            catch { }

            return new DatabaseInfo
            {
                Sucesso = false,
                DatabasePath = csb.Database,
                Erro = "Nenhuma versão encontrada no banco de dados"
            };
        }
        catch (Exception ex)
        {
            return new DatabaseInfo
            {
                Sucesso = false,
                DatabasePath = csb.Database,
                Erro = $"Falha ao conectar: {ex.Message}"
            };
        }
    }

    private DatabaseInfo TentarDosExecutaveis()
    {
        var exeDir = Path.Combine(_ecosisPath!, "windows");

        var candidates = new[] { "eco68.exe", "eco67.exe", "eco.exe" };
        foreach (var name in candidates)
        {
            var path = Path.Combine(exeDir, name);
            if (!File.Exists(path)) continue;

            try
            {
                var ver = FileVersionInfo.GetVersionInfo(path);
                var fileVer = ver.FileVersion ?? "";
                var parts = fileVer.Split('.');

                if (parts.Length >= 3)
                {
                    var versao = $"{parts[0]}.{parts[1]}.{parts[2]}";
                    int? build = parts.Length >= 4 && int.TryParse(parts[3], out var b) ? b : null;
                    return new DatabaseInfo { Versao = versao, Build = build, Sucesso = true };
                }
            }
            catch { }
        }

        return new DatabaseInfo { Sucesso = false, Erro = "Nenhum executável ECO encontrado" };
    }

    private static (string Server, int Port, string Database)? ParseConexao(string raw)
    {
        var match = MyRegex().Match(raw);
        if (!match.Success) return null;

        var server = match.Groups["server"].Value;
        var port = match.Groups["port"].Success ? int.Parse(match.Groups["port"].Value) : 3050;
        var database = match.Groups["database"].Value;

        return (server, port, database);
    }

    [GeneratedRegex(@"^(?<server>[^/]+?)(?:/(?<port>\d+))?:(?<database>.+)$")]
    private static partial Regex MyRegex();
}
