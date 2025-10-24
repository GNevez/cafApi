using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services;

public class CartService : ICartService
{
    private readonly ApplicationDbContext _context;

    public CartService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CarrinhoDto> GetOrCreateCartAsync(string? cartToken)
    {
        Carrinho? cart;

        if (string.IsNullOrEmpty(cartToken))
        {
            // Criar novo carrinho
            cart = new Carrinho
            {
                Token = Guid.NewGuid().ToString(),
                DataCriacao = DateTime.UtcNow,
                Ativo = true
            };
            _context.Carrinhos.Add(cart);
            await _context.SaveChangesAsync();
        }
        else
        {
            // Buscar carrinho existente
            cart = await _context.Carrinhos
                .Include(c => c.Itens)
                    .ThenInclude(i => i.Produto)
                .Include(c => c.Itens)
                    .ThenInclude(i => i.Cor)
                .Include(c => c.Cupom)
                .FirstOrDefaultAsync(c => c.Token == cartToken && c.Ativo);

            if (cart == null)
            {
                // Token inválido, criar novo carrinho
                cart = new Carrinho
                {
                    Token = Guid.NewGuid().ToString(),
                    DataCriacao = DateTime.UtcNow,
                    Ativo = true
                };
                _context.Carrinhos.Add(cart);
                await _context.SaveChangesAsync();
            }
        }

        return await MapToDtoAsync(cart);
    }

    public async Task<CarrinhoDto> AddItemAsync(string? cartToken, AdicionarItemCarrinhoDto item)
    {
        var cart = await GetOrCreateCartEntityAsync(cartToken);

        // Verificar se o item já existe (mesmo produto e cor)
        var existingItem = cart.Itens.FirstOrDefault(i => 
            i.ProdutoId == item.ProdutoId && i.CorId == item.CorId);

        if (existingItem != null)
        {
            // Atualizar quantidade
            existingItem.Quantidade += item.Quantidade;
            existingItem.DataAtualizacao = DateTime.UtcNow;
        }
        else
        {
            // Adicionar novo item
            var newItem = new ItemCarrinho
            {
                CarrinhoId = cart.Id,
                ProdutoId = item.ProdutoId,
                CorId = item.CorId,
                Quantidade = item.Quantidade,
                DataAdicao = DateTime.UtcNow
            };
            cart.Itens.Add(newItem);
        }

        cart.DataAtualizacao = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await MapToDtoAsync(cart);
    }

    public async Task<CarrinhoDto> UpdateItemQuantityAsync(string? cartToken, AtualizarItemCarrinhoDto item)
    {
        var cart = await GetOrCreateCartEntityAsync(cartToken);
        var cartItem = cart.Itens.FirstOrDefault(i => i.Id == item.ItemId);

        if (cartItem == null)
            throw new ArgumentException("Item não encontrado no carrinho");

        if (item.Quantidade <= 0)
        {
            cart.Itens.Remove(cartItem);
            _context.ItensCarrinho.Remove(cartItem);
        }
        else
        {
            cartItem.Quantidade = item.Quantidade;
            cartItem.DataAtualizacao = DateTime.UtcNow;
        }

        cart.DataAtualizacao = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await MapToDtoAsync(cart);
    }

    public async Task<CarrinhoDto> RemoveItemAsync(string? cartToken, int itemId)
    {
        var cart = await GetOrCreateCartEntityAsync(cartToken);
        var cartItem = cart.Itens.FirstOrDefault(i => i.Id == itemId);

        if (cartItem != null)
        {
            cart.Itens.Remove(cartItem);
            _context.ItensCarrinho.Remove(cartItem);
            cart.DataAtualizacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return await MapToDtoAsync(cart);
    }

    public async Task<CarrinhoDto> ClearCartAsync(string? cartToken)
    {
        var cart = await GetOrCreateCartEntityAsync(cartToken);
        
        _context.ItensCarrinho.RemoveRange(cart.Itens);
        cart.Itens.Clear();
        cart.DataAtualizacao = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        return await MapToDtoAsync(cart);
    }

    public async Task<bool> ValidateCartTokenAsync(string? cartToken)
    {
        if (string.IsNullOrEmpty(cartToken))
            return false;

        return await _context.Carrinhos
            .AnyAsync(c => c.Token == cartToken && c.Ativo);
    }

    private async Task<Carrinho> GetOrCreateCartEntityAsync(string? cartToken)
    {
        Carrinho? cart;

        if (string.IsNullOrEmpty(cartToken))
        {
            cart = new Carrinho
            {
                Token = Guid.NewGuid().ToString(),
                DataCriacao = DateTime.UtcNow,
                Ativo = true
            };
            _context.Carrinhos.Add(cart);
            await _context.SaveChangesAsync();
        }
        else
        {
            cart = await _context.Carrinhos
                .Include(c => c.Itens)
                    .ThenInclude(i => i.Produto)
                .Include(c => c.Itens)
                    .ThenInclude(i => i.Cor)
                .Include(c => c.Cupom)
                .FirstOrDefaultAsync(c => c.Token == cartToken && c.Ativo);

            if (cart == null)
            {
                cart = new Carrinho
                {
                    Token = Guid.NewGuid().ToString(),
                    DataCriacao = DateTime.UtcNow,
                    Ativo = true
                };
                _context.Carrinhos.Add(cart);
                await _context.SaveChangesAsync();
            }
        }

        return cart;
    }

    private async Task<CarrinhoDto> MapToDtoAsync(Carrinho cart)
    {
        cart = await _context.Carrinhos
            .Include(c => c.Itens)
                .ThenInclude(i => i.Produto)
            .Include(c => c.Itens)
                .ThenInclude(i => i.Cor)
            .Include(c => c.Cupom)
            .FirstAsync(c => c.Id == cart.Id);

        var items = cart.Itens
            .Where(i => i.Produto != null && i.Cor != null) 
            .Select(i => new ItemCarrinhoDto
            {
                Id = i.Id,
                ProdutoId = i.ProdutoId,
                CorId = i.CorId,
                Quantidade = i.Quantidade,
                DataAdicao = i.DataAdicao,
                ProdutoNome = i.Produto.Nome,
                ProdutoSlug = i.Produto.Slug,
                ProdutoSKU = i.Produto.SKU,
                ProdutoPreco = i.Produto.Preco,
                ProdutoImagem = i.Produto.ImagemPrincipal,
                CorNome = i.Cor.Nome,
                CorHex1 = i.Cor.Hex1,
                CorHex2 = i.Cor.Hex2
            }).ToList();

        // Calcular desconto de cupom (se houver)
        decimal cupomDesconto = 0m;
        string? cupomCodigo = null;
        if (cart.CupomId.HasValue && cart.Cupom != null)
        {
            var cupom = cart.Cupom;
            cupomCodigo = cupom.Codigo;
            var subtotalAtual = items.Sum(i => i.Subtotal);
            var agora = DateTime.UtcNow;
            var valido = cupom.Ativo && (cupom.DataInicio <= agora) && (!cupom.DataExpiracao.HasValue || cupom.DataExpiracao.Value >= agora) && (!cupom.QuantidadeMaximaUsos.HasValue || cupom.QuantidadeUsosAtual < cupom.QuantidadeMaximaUsos.Value);
            if (valido)
            {
                if (cupom.TipoDesconto == "percentual")
                {
                    cupomDesconto = subtotalAtual * (cupom.ValorDesconto / 100);
                    if (cupom.ValorMaximoDesconto.HasValue && cupomDesconto > cupom.ValorMaximoDesconto.Value)
                    {
                        cupomDesconto = cupom.ValorMaximoDesconto.Value;
                    }
                }
                else
                {
                    cupomDesconto = cupom.ValorDesconto;
                }
                if (cupomDesconto > subtotalAtual) cupomDesconto = subtotalAtual;
            }
        }

        return new CarrinhoDto
        {
            Token = cart.Token,
            DataCriacao = cart.DataCriacao,
            DataAtualizacao = cart.DataAtualizacao,
            Itens = items,
            Subtotal = items.Sum(i => i.Subtotal),
            TotalItens = items.Sum(i => i.Quantidade),
            CupomId = cart.CupomId,
            CupomCodigo = cupomCodigo,
            CupomValorDesconto = cupomDesconto
        };
    }
    
    public async Task<CarrinhoDto> ApplyCouponAsync(string? cartToken, string codigo)
    {
        var cart = await GetOrCreateCartEntityAsync(cartToken);
        var cupom = await _context.Cupons.FirstOrDefaultAsync(c => c.Codigo.ToUpper() == codigo.ToUpper());
        if (cupom == null) throw new ArgumentException("Cupom não encontrado");

        var agora = DateTime.UtcNow;
        if (!cupom.Ativo) throw new ArgumentException("Cupom inativo");
        if (cupom.DataInicio > agora) throw new ArgumentException("Cupom ainda não está disponível");
        if (cupom.DataExpiracao.HasValue && cupom.DataExpiracao.Value < agora) throw new ArgumentException("Cupom expirado");
        if (cupom.QuantidadeMaximaUsos.HasValue && cupom.QuantidadeUsosAtual >= cupom.QuantidadeMaximaUsos.Value) throw new ArgumentException("Cupom esgotado");

        // Valor mínimo de compra com base no subtotal atual
        var subtotal = cart.Itens.Sum(i => i.Quantidade * i.Produto.Preco);
        if (cupom.ValorMinimoCompra.HasValue && subtotal < cupom.ValorMinimoCompra.Value)
            throw new ArgumentException($"Valor mínimo de compra de R$ {cupom.ValorMinimoCompra.Value:F2} não atingido");

        cart.CupomId = cupom.Id;
        cart.DataAtualizacao = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return await MapToDtoAsync(cart);
    }

    public async Task<CarrinhoDto> RemoveCouponAsync(string? cartToken)
    {
        var cart = await GetOrCreateCartEntityAsync(cartToken);
        cart.CupomId = null;
        cart.DataAtualizacao = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return await MapToDtoAsync(cart);
    }
}
