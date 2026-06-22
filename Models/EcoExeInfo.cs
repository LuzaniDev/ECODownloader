namespace ECODownloader.Models;

public class EcoExeInfo
{
    public string Pasta { get; set; } = "";
    public string Executavel { get; set; } = "";
    public string IniFile { get; set; } = "";
    public string CaminhoIni => string.IsNullOrEmpty(IniFile) ? "" : Path.Combine(Pasta, IniFile);
    public string CaminhoExe => string.IsNullOrEmpty(Executavel) ? "" : Path.Combine(Pasta, Executavel);
}
