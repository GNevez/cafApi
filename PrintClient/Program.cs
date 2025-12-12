using System.Net.Http.Json;
using System.Diagnostics;

namespace ClienteImpressaoCAF;

public class Program
{
    private static string _serverUrl = "";
    private static string _clienteId = "";
    private static string _impressoraPadrao = "";
    private static string _tempFolder = "";
    private static HttpClient _httpClient = null!;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("  Cliente de Impressao - Chase a Flare    ");
        Console.WriteLine("==========================================");
        Console.WriteLine();

        // Configurações via variáveis de ambiente (obrigatórias)
        _serverUrl = Environment.GetEnvironmentVariable("CAF_SERVER_URL");
        _clienteId = Environment.GetEnvironmentVariable("CAF_CLIENTE_ID");
        var intervaloStr = Environment.GetEnvironmentVariable("CAF_INTERVALO");
        _impressoraPadrao = Environment.GetEnvironmentVariable("CAF_IMPRESSORA") ?? ""; // Impressora é opcional (usa padrão do Windows)

        // Validar configurações obrigatórias
        if (string.IsNullOrEmpty(_serverUrl))
        {
            Console.WriteLine("[ERRO] Variável de ambiente CAF_SERVER_URL não definida!");
            Console.WriteLine("Exemplo: set CAF_SERVER_URL=https://api.chaseaflare.com");
            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
            return;
        }

        if (string.IsNullOrEmpty(_clienteId))
        {
            Console.WriteLine("[ERRO] Variável de ambiente CAF_CLIENTE_ID não definida!");
            Console.WriteLine("Exemplo: set CAF_CLIENTE_ID=impressora-01");
            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
            return;
        }

        if (string.IsNullOrEmpty(intervaloStr) || !int.TryParse(intervaloStr, out var intervaloSegundos))
        {
            Console.WriteLine("[ERRO] Variável de ambiente CAF_INTERVALO não definida ou inválida!");
            Console.WriteLine("Exemplo: set CAF_INTERVALO=10");
            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
            return;
        }

        // Verificar se SumatraPDF está instalado
        var sumatraPath = GetSumatraPdfPath();
        if (string.IsNullOrEmpty(sumatraPath))
        {
            Console.WriteLine("[ERRO] SumatraPDF não encontrado!");
            Console.WriteLine("Instale em: https://www.sumatrapdfreader.org/download-free-pdf-viewer");
            Console.WriteLine("Ou via: winget install SumatraPDF.SumatraPDF");
            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($"Servidor: {_serverUrl}");
        Console.WriteLine($"Cliente ID: {_clienteId}");
        Console.WriteLine($"Intervalo: {intervaloSegundos} segundos");
        Console.WriteLine($"SumatraPDF: {sumatraPath}");
        if (!string.IsNullOrEmpty(_impressoraPadrao))
            Console.WriteLine($"Impressora: {_impressoraPadrao}");
        else
            Console.WriteLine("Impressora: (padrão do Windows)");
        Console.WriteLine();

        // Pasta temporária
        _tempFolder = Path.Combine(Path.GetTempPath(), "CAF_Rotulos");
        Directory.CreateDirectory(_tempFolder);

        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_serverUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        Console.WriteLine("Iniciando monitoramento da fila de impressão...");
        Console.WriteLine("Pressione Ctrl+C para parar");
        Console.WriteLine();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nEncerrando...");
        };

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                // Buscar jobs pendentes
                var response = await _httpClient.GetAsync($"/api/FilaImpressao/pendentes?clienteId={_clienteId}&limite=5", cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var jobs = await response.Content.ReadFromJsonAsync<List<FilaImpressaoDto>>(cancellationToken: cts.Token);
                    
                    if (jobs != null && jobs.Count > 0)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Encontrados {jobs.Count} job(s) pendente(s)");
                        
                        foreach (var job in jobs)
                        {
                            await ProcessarJob(job);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Fila vazia - aguardando...");
                    }
                }
                else
                {
                    Console.WriteLine($"[ERRO] Falha ao buscar fila: {response.StatusCode}");
                }
            }
            catch (TaskCanceledException) when (cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRO] {ex.Message}");
            }
            
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervaloSegundos), cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        Console.WriteLine("Cliente encerrado.");
        _httpClient.Dispose();
    }

    private static async Task ProcessarJob(FilaImpressaoDto job)
    {
        Console.WriteLine();
        Console.WriteLine($"Processando Job #{job.Id} - {job.NomeArquivo}");
        
        try
        {
            // Reservar o job
            Console.WriteLine("  Reservando...");
            var reserveResponse = await _httpClient.PostAsync($"/api/FilaImpressao/{job.Id}/reservar?clienteId={_clienteId}", null);
            if (!reserveResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"  [SKIP] Job já reservado por outro cliente");
                return;
            }
            
            // Baixar o PDF
            Console.WriteLine("  Baixando PDF...");
            var pdfResponse = await _httpClient.GetAsync($"/api/FilaImpressao/{job.Id}/download");
            if (!pdfResponse.IsSuccessStatusCode)
            {
                await ReportarErro(job.Id, "Falha ao baixar PDF");
                return;
            }
            
            var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
            var pdfPath = Path.Combine(_tempFolder, job.NomeArquivo);
            await File.WriteAllBytesAsync(pdfPath, pdfBytes);
            
            // Imprimir
            Console.WriteLine("  Enviando para impressora...");
            var impressora = !string.IsNullOrEmpty(job.ImpressoraDestino) ? job.ImpressoraDestino : _impressoraPadrao;
            var resultado = await ImprimirPdf(pdfPath, impressora, job.Copias);
            
            if (resultado.Success)
            {
                Console.WriteLine("  [OK] Impressão enviada com sucesso!");
                await _httpClient.PostAsync($"/api/FilaImpressao/{job.Id}/confirmar?clienteId={_clienteId}", null);
                
                // Limpar arquivo temporário
                try { File.Delete(pdfPath); } catch { }
            }
            else
            {
                Console.WriteLine($"  [ERRO] {resultado.Message}");
                await ReportarErro(job.Id, resultado.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [ERRO] {ex.Message}");
            await ReportarErro(job.Id, ex.Message);
        }
    }

    private static async Task<(bool Success, string Message)> ImprimirPdf(string filePath, string impressora, int copias)
    {
        try
        {
            var sumatraPath = GetSumatraPdfPath();
            if (string.IsNullOrEmpty(sumatraPath))
            {
                return (false, "SumatraPDF não encontrado");
            }

            // SumatraPDF imprime silenciosamente sem abrir janela
            var args = string.IsNullOrEmpty(impressora)
                ? $"-print-to-default -silent -print-settings \"{copias}x\" \"{filePath}\""
                : $"-print-to \"{impressora}\" -silent -print-settings \"{copias}x\" \"{filePath}\"";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = sumatraPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            await process.WaitForExitAsync();
            
            return (true, "Impressão enviada via SumatraPDF");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string? GetSumatraPdfPath()
    {
        var possiblePaths = new[]
        {
            @"C:\Program Files\SumatraPDF\SumatraPDF.exe",
            @"C:\Program Files (x86)\SumatraPDF\SumatraPDF.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF", "SumatraPDF.exe")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static async Task ReportarErro(int jobId, string mensagem)
    {
        try
        {
            var body = new { mensagemErro = mensagem, clienteId = _clienteId };
            await _httpClient.PostAsJsonAsync($"/api/FilaImpressao/{jobId}/erro", body);
        }
        catch { }
    }
}

// DTOs
public class FilaImpressaoDto
{
    public int Id { get; set; }
    public int? RotuloId { get; set; }
    public int? PedidoId { get; set; }
    public string? CodigoPedido { get; set; }
    public string NomeArquivo { get; set; } = "";
    public string CaminhoArquivo { get; set; } = "";
    public string Status { get; set; } = "";
    public int Tentativas { get; set; }
    public string? ImpressoraDestino { get; set; }
    public int Copias { get; set; }
    public DateTime DataCriacao { get; set; }
}
