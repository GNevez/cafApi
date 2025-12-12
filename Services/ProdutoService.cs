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

        public async Task<IEnumerable<ProdutoSearchDto>> SearchAsync(string query, int limit = 8)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<ProdutoSearchDto>();
            }

            var q = query.Trim();

            var produtos = await _context.Produtos
                .AsNoTracking()
                .Where(p => p.Active == true && EF.Functions.Like(p.Nome, $"%{q}%"))
                .OrderBy(p => p.Nome)
                .Select(p => new ProdutoSearchDto
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Slug = p.Slug,
                    Preco = p.Preco,
                    ImagemPrincipal = p.ImagemPrincipal
                })
                .Take(limit)
                .ToListAsync();

            return produtos;
        }

        public async Task<IEnumerable<ProdutoResponseDto>> GetAllAsync()
        {
            var produtos = await _context.Produtos
                .Where(p => p.Active == true) // 🔹 Apenas produtos ativos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .ToListAsync();
                
            return produtos.Select(p => p.ToResponseDto());
        }

        public async Task<(List<ProdutoResponseDto> produtos, int totalCount)> GetPaginatedAsync(int pageNumber, int pageSize, int? categoriaId = null, int? corId = null, decimal? precoMin = null, decimal? precoMax = null, string? ordenacao = null)
        {
            var query = _context.Produtos
                .Where(p => p.Active == true)
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .AsQueryable();

            // Filtro por categoria
            if (categoriaId.HasValue)
            {
                query = query.Where(p => p.CategoriaId == categoriaId.Value);
            }

            // Filtro por cor (ProdutosCor.Id)
            if (corId.HasValue)
            {
                query = query.Where(p => p.CoresDisponiveis.Any(c => c.Id == corId.Value));
            }

            // Filtro por preço mínimo
            if (precoMin.HasValue)
            {
                query = query.Where(p => p.Preco >= precoMin.Value);
            }

            // Filtro por preço máximo
            if (precoMax.HasValue)
            {
                query = query.Where(p => p.Preco <= precoMax.Value);
            }

            // Ordenação
            query = ordenacao?.ToLower() switch
            {
                "preco-asc" => query.OrderBy(p => p.Preco),
                "preco-desc" => query.OrderByDescending(p => p.Preco),
                "nome" => query.OrderBy(p => p.Nome),
                "mais-recente" => query.OrderByDescending(p => p.Id),
                _ => query.OrderBy(p => p.Id)
            };

            var totalCount = await query.CountAsync();

            var produtos = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (produtos.Select(p => p.ToResponseDto()).ToList(), totalCount);
        }

        public async Task<IEnumerable<ProdutoResponseDto>> GetInactiveAsync()
        {
            var produtos = await _context.Produtos
                .Where(p => p.Active == false) // 🔹 Apenas produtos desativados
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .ToListAsync();
                
            return produtos.Select(p => p.ToResponseDto());
        }

        public async Task<ProdutoResponseDto?> GetByIdAsync(int id)
        {
            var produto = await _context.Produtos
                .Where(p => p.Active == true) // 🔹 Apenas produtos ativos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id);
                
            return produto?.ToResponseDto();
        }

        public async Task<ProdutoResponseDto?> GetBySlugAsync(string slug)
        {
            var produto = await _context.Produtos
                .Where(p => p.Active == true) // 🔹 Apenas produtos ativos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Slug == slug);
                
            return produto?.ToResponseDto();
        }

        public async Task<ProdutoDetalhadoDto?> GetBySlugDetalhadoAsync(string slug)
        {
            var produto = await _context.Produtos
                .Where(p => p.Active == true)
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Slug == slug);

            if (produto == null) return null;

            return new ProdutoDetalhadoDto
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
                MaxParcelas = produto.MaxParcelas,
                TaxaJuros = produto.TaxaJuros,
                Descricao = produto.Descricao,
                CategoriaId = produto.CategoriaId,
                CategoriaNome = produto.Categoria.Nome,
                CoresDisponiveis = produto.CoresDisponiveis.Select(c => new CorDetalhadaDto
                {
                    Id = c.Id,
                    Nome = c.Nome,
                    Hex1 = c.Hex1,
                    Hex2 = c.Hex2,
                    QuantidadeEstoque = c.QuantidadeEstoque,
                    Imagens = c.Imagens.Select(i => new ImagemCorDto
                    {
                        Id = i.Id,
                        Url = i.Url
                    }).ToList()
                }).ToList()
            };
        }

        public async Task<IEnumerable<ProdutoResponseDto>> GetByCategoriaAsync(int categoriaId)
        {
            var produtos = await _context.Produtos
                .Where(p => p.Active == true && p.CategoriaId == categoriaId) // 🔹 Apenas produtos ativos
                .Include(p => p.CoresDisponiveis)
                    .ThenInclude(c => c.Imagens)
                .Include(p => p.Categoria)
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
                    SKU = produtoDto.SKU,
                    CodigoExterno = produtoDto.CodigoExterno,
                    Fabricante = produtoDto.Fabricante,
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
                    .ThenInclude(c => c.Imagens)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProduto == null) return false;

            existingProduto.Nome = produto.Nome;
            existingProduto.SKU = produto.SKU;
            existingProduto.CodigoExterno = produto.CodigoExterno;
            existingProduto.Fabricante = produto.Fabricante;
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

        public async Task<bool> DeactivateAsync(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return false;
            
            produto.Active = false; // 🔹 Soft delete - desativar produto
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReactivateAsync(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return false;
            
            produto.Active = true; // 🔹 Reativar produto
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCorAsync(int corId)
        {
            var cor = await _context.ProdutosCores.FindAsync(corId);
            if (cor == null) return false;

            // Remover imagens associadas primeiro
            var imagens = await _context.ProdutosCorImagens
                .Where(i => i.ProdutosCorId == corId)
                .ToListAsync();
            
            _context.ProdutosCorImagens.RemoveRange(imagens);
            
            // Remover a cor
            _context.ProdutosCores.Remove(cor);
            
            await _context.SaveChangesAsync();
            return true;
        }

        public void AddCor(ProdutosCor cor)
        {
            _context.ProdutosCores.Add(cor);
        }

        public void AddImagemCor(ProdutosCorImagem imagemCor)
        {
            _context.ProdutosCorImagens.Add(imagemCor);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<List<ProdutosCor>> GetCoresByProdutoIdAsync(int produtoId)
        {
            return await _context.ProdutosCores
                .Where(c => c.ProdutosId == produtoId)
                .Include(c => c.Imagens)
                .ToListAsync();
        }

        public async Task<List<ProdutosCorImagem>> GetImagensByCorIdAsync(int corId)
        {
            return await _context.ProdutosCorImagens
                .Where(i => i.ProdutosCorId == corId)
                .ToListAsync();
        }

        public async Task<Produtos?> GetBySKUAsync(string sku)
        {
            return await _context.Produtos
                .Where(p => p.Active == true) // 🔹 Apenas produtos ativos
                .FirstOrDefaultAsync(p => p.SKU == sku);
        }

        public async Task<Produtos?> GetBySKUIncludingInactiveAsync(string sku)
        {
            return await _context.Produtos
                .FirstOrDefaultAsync(p => p.SKU == sku); // 🔹 Inclui produtos desativados
        }

        public async Task<Produtos?> GetByCodigoExternoAsync(string codigoExterno)
        {
            return await _context.Produtos
                .Where(p => p.Active == true) // 🔹 Apenas produtos ativos
                .FirstOrDefaultAsync(p => p.CodigoExterno == codigoExterno);
        }

        public async Task<Produtos?> GetByCodigoExternoIncludingInactiveAsync(string codigoExterno)
        {
            return await _context.Produtos
                .FirstOrDefaultAsync(p => p.CodigoExterno == codigoExterno); // 🔹 Inclui produtos desativados
        }

        public async Task<IEnumerable<object>> GetCoresDisponiveisAsync()
        {
            var cores = await _context.ProdutosCores
                .Where(pc => pc.Produtos.Active == true)
                .Select(pc => new
                {
                    id = pc.Id,
                    nome = pc.Nome,
                    hex1 = pc.Hex1,
                    hex2 = pc.Hex2
                })
                .Distinct()
                .ToListAsync();

            return cores;
        }
    }
}
