using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ECODownloader.Models;

namespace ECODownloader.Services;

public class WebLiberacaoClient : IDisposable
{
    private readonly HttpClient _apiClient;
    private readonly HttpClient _downloadClient;
    private readonly string _baseUrl;
    private readonly string _apiBaseUrl;
    private bool _autenticado;
    private string? _token;

    public WebLiberacaoClient(string baseUrl = "https://webliberacao.ec.eco.br",
        string apiBaseUrl = "https://api-webliberacao.ec.eco.br")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');

        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = true
        };

        _apiClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _downloadClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(60)
        };

        foreach (var (name, value) in Headers)
        {
            _apiClient.DefaultRequestHeaders.Add(name, value);
            _downloadClient.DefaultRequestHeaders.Add(name, value);
        }
    }

    private static readonly (string, string)[] Headers =
    [
        ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"),
        ("Accept", "application/json, text/plain, */*")
    ];

    public async Task<(bool Autenticado, int StatusCode)> LoginAsync(string usuario, string senha)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { login = usuario, senha });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _apiClient.PostAsync($"{_apiBaseUrl}/api/sol-auth", content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var doc = JsonDocument.Parse(body);
                _token = doc.RootElement.GetProperty("token").GetString();
                _autenticado = true;

                _apiClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _token);
                _downloadClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _token);

                return (true, 200);
            }

            _autenticado = false;

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                    System.Diagnostics.Debug.WriteLine($"Login falhou: {detail.GetString()}");
            }
            catch { }

            return (false, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro no login: {ex.Message}");
            return (false, 0);
        }
    }

    public async Task<ApiResponse?> ObterLiberacoesAsync()
    {
        if (!_autenticado)
            throw new InvalidOperationException("Não autenticado. Execute LoginAsync primeiro.");

        var response = await _apiClient.GetAsync("/api/liberacao/sistema/ECO?page=1");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ApiResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<bool> BaixarArquivoAsync(string filename, string destino)
    {
        var url = $"/files/{filename}";
        var response = await _downloadClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(destino);
        await stream.CopyToAsync(fileStream);

        return true;
    }

    public async Task BaixarArquivoComProgressoAsync(
        string filename, string destino, IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var url = $"/files/{filename}";
        using var response = await _downloadClient.GetAsync(url,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(destino);

        var buffer = new byte[81920];
        long bytesLidos = 0;
        int bytesNoChunk;

        while ((bytesNoChunk = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesNoChunk, ct);
            bytesLidos += bytesNoChunk;
            if (totalBytes > 0)
                progress?.Report((int)(bytesLidos * 100 / totalBytes));
        }
    }

    public void Dispose()
    {
        _apiClient.Dispose();
        _downloadClient.Dispose();
    }
}
