namespace cafApi.Models.DTOs
{
    public class VideoDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Thumbnail { get; set; }
        public int Duracao { get; set; }
        public int Ordem { get; set; }
        public bool Ativo { get; set; }
        public int CategoriaId { get; set; }
        public string CategoriaNome { get; set; } = string.Empty;
        public string CategoriaSlug { get; set; } = string.Empty;
    }
}
