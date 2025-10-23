namespace cafApi.Models
{
    public enum StatusCarrinho
    {
        Ativo = 0,              // Carrinho ativo (em uso)
        Finalizado = 1,          // Carrinho convertido em pedido
        Abandonado = 2,          // Carrinho abandonado (não finalizado)
        Expirado = 3             // Carrinho expirado
    }
}
