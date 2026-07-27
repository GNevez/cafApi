namespace cafApi.Models.DTOs;

public class EmailMarketingDto
{
    public string Titulo { get; set; } = null!;
    public string Corpo { get; set; } = null!;
    public string? BannerUrl { get; set; }
    public string? BannerLink { get; set; }
    public SegmentoCliente Segmento { get; set; } = SegmentoCliente.Todos;
}

public enum SegmentoCliente
{
    Todos = 0,
    Compradores = 1,      // Clientes que já compraram
    NaoCompradores = 2    // Clientes cadastrados que nunca compraram
}

public class EmailMarketingResultDto
{
    public int TotalEnviados { get; set; }
    public int TotalFalhas { get; set; }
    public List<string> Erros { get; set; } = new();
}

public class PreviewEmailDto
{
    public string Html { get; set; } = null!;
    public int TotalDestinatarios { get; set; }
}
