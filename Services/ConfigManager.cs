using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECODownloader.Models;

namespace ECODownloader.Services;

public static class ConfigManager
{
    private static string Caminho => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static readonly byte[] Entropy = "ECODownloader_Config_v1"u8.ToArray();

    public static AppConfig Carregar()
    {
        if (!File.Exists(Caminho))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(Caminho);
            var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            config.Senha = Descriptografar(config.Senha);
            config.DbPassword = Descriptografar(config.DbPassword);
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Salvar(AppConfig config)
    {
        var dir = Path.GetDirectoryName(Caminho)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var salvar = new AppConfig
        {
            Usuario = config.Usuario,
            Senha = Criptografar(config.Senha),
            Servidor = config.Servidor,
            Database = config.Database,
            PastaExecutaveis = config.PastaExecutaveis,
            ExecutavelSelecionado = config.ExecutavelSelecionado,
            IniSelecionado = config.IniSelecionado,
            DbUser = config.DbUser,
            DbPassword = Criptografar(config.DbPassword),
        };

        var json = JsonSerializer.Serialize(salvar, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(Caminho, json);
    }

    private static string Criptografar(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return "";
        var dados = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(texto),
            Entropy,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(dados);
    }

    private static string Descriptografar(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return "";
        try
        {
            var dados = Convert.FromBase64String(texto);
            var decifrado = ProtectedData.Unprotect(
                dados,
                Entropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decifrado);
        }
        catch
        {
            return texto;
        }
    }
}
