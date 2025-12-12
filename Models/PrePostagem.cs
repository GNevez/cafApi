using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cafApi.Models;

public class PrePostagem
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int PedidoId { get; set; }
    
    [ForeignKey("PedidoId")]
    public Pedido Pedido { get; set; } = null!;
    
    // Dados da pré-postagem
    public string? CodigoRastreamento { get; set; }
    public string? IdPrePostagem { get; set; }
    public string? NumeroEtiqueta { get; set; }
    
    // Serviço dos Correios
    [Required]
    public string CodigoServico { get; set; } = "03220"; // SEDEX por padrão
    public string NomeServico { get; set; } = "SEDEX";
    
    // Dimensões e peso
    public decimal Peso { get; set; } = 0.3m; // kg
    public int Altura { get; set; } = 5; // cm
    public int Largura { get; set; } = 15; // cm
    public int Comprimento { get; set; } = 20; // cm
    
    // Valor declarado
    public decimal? ValorDeclarado { get; set; }
    
    // Status
    public StatusPrePostagem Status { get; set; } = StatusPrePostagem.Pendente;
    
    // Datas
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataPostagem { get; set; }
    public DateTime? DataEntrega { get; set; }
    
    // Observações e erros
    public string? Observacoes { get; set; }
    public string? MensagemErro { get; set; }
    
    // JSON da resposta dos Correios
    public string? RespostaCorreiosJson { get; set; }
}

public enum StatusPrePostagem
{
    Pendente = 0,
    Gerada = 1,
    Postada = 2,
    EmTransito = 3,
    Entregue = 4,
    Devolvido = 5,
    Cancelada = 6,
    Erro = 7
}
