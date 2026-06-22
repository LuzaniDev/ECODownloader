using System.Text.Json.Serialization;

namespace ECODownloader.Models;

public class ApiResponse
{
    [JsonPropertyName("sistema")]
    public string Sistema { get; set; } = "";

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = "";

    [JsonPropertyName("grupos")]
    public List<GrupoInfo> Grupos { get; set; } = [];
}

public class GrupoInfo
{
    [JsonPropertyName("grupo")]
    public string Grupo { get; set; } = "";

    [JsonPropertyName("ultima_versao")]
    public bool UltimaVersao { get; set; }

    [JsonPropertyName("versoes")]
    public List<VersaoInfo> Versoes { get; set; } = [];
}

public class VersaoInfo
{
    [JsonPropertyName("versao")]
    public string Versao { get; set; } = "";

    [JsonPropertyName("ultima_versao")]
    public bool UltimaVersao { get; set; }

    [JsonPropertyName("builds")]
    public List<BuildInfo> Builds { get; set; } = [];
}

public class BuildInfo
{
    [JsonPropertyName("build")]
    public int Build { get; set; }

    [JsonPropertyName("versao")]
    public string Versao { get; set; } = "";

    [JsonPropertyName("publicador")]
    public string Publicador { get; set; } = "";

    [JsonPropertyName("data_publicacao")]
    public string DataPublicacao { get; set; } = "";

    [JsonPropertyName("link_ativo")]
    public bool LinkAtivo { get; set; }

    [JsonPropertyName("motivo_desativacao")]
    public string? MotivoDesativacao { get; set; }

    [JsonPropertyName("atualizacao_critica")]
    public bool AtualizacaoCritica { get; set; }

    [JsonPropertyName("atualizacao_recomendada")]
    public bool AtualizacaoRecomendada { get; set; }

    [JsonPropertyName("atualizacao_obrigatoria")]
    public bool AtualizacaoObrigatoria { get; set; }

    [JsonPropertyName("ultima_versao")]
    public bool UltimaVersao { get; set; }

    [JsonPropertyName("descontinuado")]
    public bool Descontinuado { get; set; }

    [JsonPropertyName("arquivos")]
    public List<ArquivoInfo> Arquivos { get; set; } = [];

    [JsonPropertyName("gits")]
    public List<GitInfo>? Gits { get; set; }

    public bool Disponivel => LinkAtivo && !Descontinuado;
}

public class ArquivoInfo
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("rar")]
    public string Rar { get; set; } = "";

    public bool IsExecutavel => Rar.Equals("eco.zip", StringComparison.OrdinalIgnoreCase);
    public bool IsSetup => Rar.StartsWith("EcoSetup", StringComparison.OrdinalIgnoreCase);
}

public class GitInfo
{
    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = "";

    [JsonPropertyName("solicitacao")]
    public string Solicitacao { get; set; } = "";
}
