using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Services
{
    public class ProdutoService : IProdutoService
    {
        private readonly ApplicationDbContext _context;

        public ProdutoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProdutoResponseDto>> GetAllAsync()
        {
            var produtos = await _context.Produtos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .ToListAsync();
                
            return produtos.Select(p => p.ToResponseDto());
        }

        public async Task<ProdutoResponseDto?> GetByIdAsync(int id)
        {
            var produto = await _context.Produtos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id);
                
            return produto?.ToResponseDto();
        }

        public async Task<ProdutoResponseDto?> GetBySlugAsync(string slug)
        {
            var produto = await _context.Produtos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Slug == slug);
                
            return produto?.ToResponseDto();
        }

        public async Task<IEnumerable<ProdutoResponseDto>> GetByCategoriaAsync(int categoriaId)
        {
            var produtos = await _context.Produtos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .Where(p => p.CategoriaId == categoriaId)
                .ToListAsync();
                
            return produtos.Select(p => p.ToResponseDto());
        }

        public async Task<Produtos> CreateAsync(Produtos produto)
        {
            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();
            return produto;
        }

        public async Task<List<Produtos>> CreateManyAsync(List<Produtos> produtos)
        {
            var produtosCriados = new List<Produtos>();
            
            foreach (var produto in produtos)
            {
                // Salva o produto primeiro
                var produtoParaSalvar = new Produtos
                {
                    Nome = produto.Nome,
                    Slug = produto.Slug,
                    Preco = produto.Preco,
                    PrecoOriginal = produto.PrecoOriginal,
                    IsSale = produto.IsSale,
                    IsNew = produto.IsNew,
                    ImagemPrincipal = produto.ImagemPrincipal,
                    ImagemHover = produto.ImagemHover,
                    CategoriaId = produto.CategoriaId
                };
                
                _context.Produtos.Add(produtoParaSalvar);
                await _context.SaveChangesAsync();
                
                // Agora adiciona as cores
                foreach (var cor in produto.CoresDisponiveis)
                {
                    var corParaSalvar = new ProdutosCor
                    {
                        Nome = cor.Nome,
                        QuantidadeEstoque = cor.QuantidadeEstoque,
                        ProdutosId = produtoParaSalvar.Id
                    };
                    
                    _context.ProdutosCores.Add(corParaSalvar);
                    await _context.SaveChangesAsync();
                    
                    // Adiciona as imagens
                    foreach (var imagem in cor.Imagens)
                    {
                        var imagemParaSalvar = new ProdutosCorImagem
                        {
                            Url = imagem.Url,
                            Ordem = imagem.Ordem,
                            ProdutosCorId = corParaSalvar.Id
                        };
                        
                        _context.ProdutosCorImagens.Add(imagemParaSalvar);
                    }
                }
                
                await _context.SaveChangesAsync();
                produtosCriados.Add(produtoParaSalvar);
            }
            
            return produtosCriados;
        }

        public async Task<List<Produtos>> CreateManyFromDtoAsync(List<ProdutoCreateDto> produtosDto)
        {
            var produtosCriados = new List<Produtos>();
            
            foreach (var produtoDto in produtosDto)
            {
                // Salva o produto primeiro
                var produtoParaSalvar = new Produtos
                {
                    Nome = produtoDto.Nome,
                    Slug = produtoDto.Slug,
                    Preco = produtoDto.Preco,
                    PrecoOriginal = produtoDto.PrecoOriginal,
                    IsSale = produtoDto.IsSale,
                    IsNew = produtoDto.IsNew,
                    ImagemPrincipal = produtoDto.ImagemPrincipal,
                    ImagemHover = produtoDto.ImagemHover,
                    CategoriaId = produtoDto.CategoriaId
                };
                
                _context.Produtos.Add(produtoParaSalvar);
                await _context.SaveChangesAsync();
                
                // Agora adiciona as cores
                foreach (var corDto in produtoDto.CoresDisponiveis)
                {
                    var corParaSalvar = new ProdutosCor
                    {
                        Nome = corDto.Nome,
                        QuantidadeEstoque = corDto.QuantidadeEstoque,
                        ProdutosId = produtoParaSalvar.Id
                    };
                    
                    _context.ProdutosCores.Add(corParaSalvar);
                    await _context.SaveChangesAsync();
                    
                    // Adiciona as imagens
                    foreach (var imagemDto in corDto.Imagens)
                    {
                        var imagemParaSalvar = new ProdutosCorImagem
                        {
                            Url = imagemDto.Url,
                            Ordem = imagemDto.Ordem,
                            ProdutosCorId = corParaSalvar.Id
                        };
                        
                        _context.ProdutosCorImagens.Add(imagemParaSalvar);
                    }
                }
                
                await _context.SaveChangesAsync();
                produtosCriados.Add(produtoParaSalvar);
            }
            
            return produtosCriados;
        }

        public async Task<bool> UpdateAsync(int id, Produtos produto)
        {
            var existingProduto = await _context.Produtos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Cores)
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProduto == null) return false;

            existingProduto.Nome = produto.Nome;
            existingProduto.Preco = produto.Preco;
            existingProduto.PrecoOriginal = produto.PrecoOriginal;
            existingProduto.IsSale = produto.IsSale;
            existingProduto.IsNew = produto.IsNew;
            existingProduto.ImagemPrincipal = produto.ImagemPrincipal;
            existingProduto.ImagemHover = produto.ImagemHover;
            existingProduto.CategoriaId = produto.CategoriaId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return false;

            _context.Produtos.Remove(produto);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
