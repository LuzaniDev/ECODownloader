namespace ECODownloader.Models;

public class DatabaseInfo
{
    public string? Versao { get; set; }
    public int? Build { get; set; }
    public string? DatabasePath { get; set; }
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }

    public string DisplayString => Sucesso
        ? $"v{Versao} (Build #{Build})"
        : $"Não foi possível detectar: {Erro}";
}
