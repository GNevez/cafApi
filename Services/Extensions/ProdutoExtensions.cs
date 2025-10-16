using cafApi.Models;
using cafApi.Models.DTOs;

namespace cafApi.Services.Extensions
{
    public static class ProdutoExtensions
    {
        public static ProdutoResponseDto ToResponseDto(this Produtos produto)
        {
            return new ProdutoResponseDto
            {
                Id = produto.Id,
                Nome = produto.Nome,
                SKU = produto.SKU,
                CodigoExterno = produto.CodigoExterno,
                Fabricante = produto.Fabricante,
                Slug = produto.Slug,
                Preco = produto.Preco,
                PrecoOriginal = produto.PrecoOriginal,
                IsSale = produto.IsSale,
                IsNew = produto.IsNew,
                ImagemPrincipal = produto.ImagemPrincipal,
                ImagemHover = produto.ImagemHover,
                CategoriaId = produto.CategoriaId,
                CategoriaNome = produto.Categoria?.Nome ?? string.Empty,
                CoresDisponiveis = produto.CoresDisponiveis.Select(cor => new ProdutoCorResponseDto
                {
                    Id = cor.Id,
                    Nome = cor.Nome,
                    QuantidadeEstoque = cor.QuantidadeEstoque,
                    Hex1 = cor.Hex1,
                    Hex2 = cor.Hex2,
                    Imagens = cor.Imagens.Select(img => new ProdutoCorImagemResponseDto
                    {
                        Id = img.Id,
                        Url = img.Url,
                        Ordem = img.Ordem
                    }).ToList()
                }).ToList()
            };
        }
    }
}
