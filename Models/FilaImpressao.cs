namespace cafApi.Models;

public class FilaImpressao
{
    public int Id { get; set; }
    public int? RotuloId { get; set; }
    public int? PedidoId { get; set; }
    public string? CodigoPedido { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string CaminhoArquivo { get; set; } = string.Empty;
    public StatusImpressao Status { get; set; } = StatusImpressao.Pendente;
    public int Tentativas { get; set; } = 0;
    public int MaxTentativas { get; set; } = 3;
    public string? MensagemErro { get; set; }
    public string? ImpressoraDestino { get; set; }
    public int Copias { get; set; } = 1;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataProcessamento { get; set; }
    public DateTime? DataImpressao { get; set; }
    public string? ClienteId { get; set; } // ID do cliente de impressão que pegou o job
    
    // Navegação
    public virtual Rotulo? Rotulo { get; set; }
}

public enum StatusImpressao
{
    Pendente = 0,
    EmProcessamento = 1,
    Impresso = 2,
    Erro = 3,
    Cancelado = 4
}
