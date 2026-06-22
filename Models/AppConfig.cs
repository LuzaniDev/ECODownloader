using System.Text.Json.Serialization;

namespace ECODownloader.Models;

public class AppConfig
{
    [JsonPropertyName("usuario")]
    public string Usuario { get; set; } = "";

    [JsonPropertyName("senha")]
    public string Senha { get; set; } = "";

    [JsonPropertyName("servidor")]
    public string Servidor { get; set; } = "127.0.0.1/3050";

    [JsonPropertyName("database")]
    public string Database { get; set; } = @"\ecosis\dados\ECODADOS_RESTAURADA.ECO";

    [JsonPropertyName("pasta_exe")]
    public string PastaExecutaveis { get; set; } = @"C:\Ecosis\windows";

    [JsonPropertyName("exe_selecionado")]
    public string ExecutavelSelecionado { get; set; } = "";

    [JsonPropertyName("ini_selecionado")]
    public string IniSelecionado { get; set; } = "";

    [JsonPropertyName("db_usuario")]
    public string DbUser { get; set; } = "SYSDBA";

    [JsonPropertyName("db_senha")]
    public string DbPassword { get; set; } = "masterkey";
}
