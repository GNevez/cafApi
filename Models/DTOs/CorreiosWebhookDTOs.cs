namespace cafApi.Models.DTOs
{
    /// <summary>
    /// DTO que representa o payload recebido do webhook dos Correios
    /// </summary>
    public class CorreiosWebhookPayload
    {
        /// <summary>
        /// Código de rastreamento do objeto
        /// </summary>
        public string CodigoObjeto { get; set; } = null!;
        
        /// <summary>
        /// Tipo do evento (ex: BDE-1, PO-1, OEC-1)
        /// </summary>
        public string TipoEvento { get; set; } = null!;
        
        /// <summary>
        /// Descrição do evento
        /// </summary>
        public string? DescricaoEvento { get; set; }
        
        /// <summary>
        /// Data e hora do evento
        /// </summary>
        public DateTime DataEvento { get; set; }
        
        /// <summary>
        /// Unidade dos Correios onde ocorreu o evento
        /// </summary>
        public string? Unidade { get; set; }
        
        /// <summary>
        /// Cidade onde ocorreu o evento
        /// </summary>
        public string? Cidade { get; set; }
        
        /// <summary>
        /// UF onde ocorreu o evento
        /// </summary>
        public string? Uf { get; set; }
        
        /// <summary>
        /// Dados adicionais do evento (JSON)
        /// </summary>
        public object? DadosAdicionais { get; set; }
    }

    /// <summary>
    /// Resposta do processamento do webhook
    /// </summary>
    public class CorreiosWebhookResponse
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
        public string? CodigoObjeto { get; set; }
        public string? TipoEvento { get; set; }
        public string? AcaoTomada { get; set; }
    }

    /// <summary>
    /// Resultado do processamento interno do evento
    /// </summary>
    public class EventoProcessadoResult
    {
        public bool Processado { get; set; }
        public string? AcaoTomada { get; set; }
        public TipoObjetoCorreios TipoObjeto { get; set; }
        public int? PedidoId { get; set; }
        public int? DevolucaoId { get; set; }
        public bool EmailEnviado { get; set; }
    }

    /// <summary>
    /// Tipo de objeto rastreado (Pedido ou Devolução)
    /// </summary>
    public enum TipoObjetoCorreios
    {
        Pedido,
        Devolucao,
        NaoIdentificado
    }
}
