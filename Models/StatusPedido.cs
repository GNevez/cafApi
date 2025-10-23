namespace cafApi.Models
{
    public enum StatusPedido
    {
        Pendente = 0,           // Aguardando pagamento
        Pago = 1,               // Pagamento confirmado
        Processando = 2,        // Em preparação
        Enviado = 3,            // Enviado para entrega
        Entregue = 4,           // Entregue ao cliente
        Cancelado = 5,          // Pedido cancelado
        Devolvido = 6           // Devolvido
    }
}
