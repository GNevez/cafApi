namespace cafApi.Models;

public class Rotulo
{
    public int Id { get; set; }
    public int? IdPedido { get; set; }
    public string IdRecibo { get; set; } = string.Empty;
    public string? IdAtendimento { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string CaminhoArquivo { get; set; } = string.Empty;
    public DateTime DataGeracao { get; set; } = DateTime.UtcNow;
    public int QuantidadeRotulos { get; set; }
    public string? CodigosObjeto { get; set; } // JSON array de códigos
    public string? IdsPrePostagem { get; set; } // JSON array de IDs
    public string TipoRotulo { get; set; } = "P"; // P = Padrão
    public string FormatoRotulo { get; set; } = "ET"; // ET = Etiqueta
    public long TamanhoBytes { get; set; }
    public string? Observacao { get; set; }
}
