namespace cafApi.Models
{
    public class Video
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = null!;
        public string Descricao { get; set; } = null!;
        public string Url { get; set; } = null!; // URL do vídeo (YouTube, Vimeo, etc.)
        public string? Thumbnail { get; set; } // URL da thumbnail do vídeo
        public int Duracao { get; set; } // Duração em segundos
        public int Ordem { get; set; } = 0; // Ordem de exibição
        public bool Ativo { get; set; } = true; // Soft delete

        // 🔗 Relacionamento com categoria
        public int CategoriaId { get; set; }
        public Categoria Categoria { get; set; } = null!;
    }
}
