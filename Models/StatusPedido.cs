namespace cafApi.Models
{
    public enum StatusPedido
    {
        AguardandoConfirmacao = 0,  // Aguardando confirmação
        EmSeparacao = 1,            // Em separação
        ACaminho = 2,               // A caminho
        Finalizado = 3,             // Finalizado/Entregue
        Cancelado = 4               // Cancelado
    }
}
