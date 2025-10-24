using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services
{
    public class PedidoService : IPedidoService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICartService _cartService;

        public PedidoService(ApplicationDbContext context, ICartService cartService)
        {
            _context = context;
            _cartService = cartService;
        }

        public async Task<PedidoDto?> GetByIdAsync(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.EnderecoEntrega)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Produto)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Cor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null) return null;

            return await MapPedidoToDto(pedido);
        }

        public async Task<(List<PedidoDto> pedidos, int totalCount)> GetAllAsync(int pageNumber, int pageSize)
        {
            var baseQuery = _context.Pedidos
                .AsNoTracking()
                .OrderByDescending(p => p.DataPedido)
                .ThenByDescending(p => p.Id); // desempate estável

            var totalCount = await baseQuery.CountAsync();

            var pagedIds = await baseQuery
                .Select(p => p.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (pagedIds.Count == 0)
            {
                return (new List<PedidoDto>(), totalCount);
            }

            var pedidosPage = await _context.Pedidos
                .AsNoTracking()
                .Where(p => pagedIds.Contains(p.Id))
                .Include(p => p.Cliente)
                .Include(p => p.EnderecoEntrega)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Produto)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Cor)
                .ToListAsync();

            // Reordenar para respeitar a ordem de pagedIds
            pedidosPage = pedidosPage
                .OrderBy(p => pagedIds.IndexOf(p.Id))
                .ToList();

            var pedidosDto = new List<PedidoDto>();
            foreach (var pedido in pedidosPage)
            {
                pedidosDto.Add(await MapPedidoToDto(pedido));
            }

            return (pedidosDto, totalCount);
        }

        public async Task<List<PedidoDto>> GetByClienteIdAsync(int clienteId)
        {
            var pedidos = await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.EnderecoEntrega)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Produto)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Cor)
                .Where(p => p.ClienteId == clienteId)
                .OrderByDescending(p => p.DataPedido)
                .ToListAsync();

            var pedidosDto = new List<PedidoDto>();
            foreach (var pedido in pedidos)
            {
                pedidosDto.Add(await MapPedidoToDto(pedido));
            }

            return pedidosDto;
        }

        public async Task<(List<PedidoDto> pedidos, int totalCount)> GetByStatusAsync(StatusPedido status, int pageNumber, int pageSize)
        {
            var baseQuery = _context.Pedidos
                .AsNoTracking()
                .Where(p => p.Status == status)
                .OrderByDescending(p => p.DataPedido)
                .ThenByDescending(p => p.Id); // desempate estável

            var totalCount = await baseQuery.CountAsync();

            var pagedIds = await baseQuery
                .Select(p => p.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (pagedIds.Count == 0)
            {
                return (new List<PedidoDto>(), totalCount);
            }

            var pedidosPage = await _context.Pedidos
                .AsNoTracking()
                .Where(p => pagedIds.Contains(p.Id))
                .Include(p => p.Cliente)
                .Include(p => p.EnderecoEntrega)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Produto)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Cor)
                .ToListAsync();

            // Reordenar para respeitar a ordem de pagedIds
            pedidosPage = pedidosPage
                .OrderBy(p => pagedIds.IndexOf(p.Id))
                .ToList();

            var pedidosDto = new List<PedidoDto>();
            foreach (var pedido in pedidosPage)
            {
                pedidosDto.Add(await MapPedidoToDto(pedido));
            }

            return (pedidosDto, totalCount);
        }

        public async Task<PedidoDto> CreateAsync(CriarPedidoDto criarPedidoDto, string cartToken)
        {
            // Buscar carrinho
            var carrinho = await _context.Carrinhos
                .Include(c => c.Itens)
                    .ThenInclude(i => i.Produto)
                .Include(c => c.Itens)
                    .ThenInclude(i => i.Cor)
                .Include(c => c.Cupom)
                .FirstOrDefaultAsync(c => c.Token == cartToken && c.Ativo);

            if (carrinho == null)
                throw new InvalidOperationException("Carrinho não encontrado ou já finalizado");

            if (carrinho.Itens.Count == 0)
                throw new InvalidOperationException("Carrinho está vazio");

            // Buscar cliente por CPF (se fornecido) ou email
            Cliente? cliente = null;
            string? cpfLimpo = null;
            if (!string.IsNullOrEmpty(criarPedidoDto.Cpf))
            {
                cpfLimpo = criarPedidoDto.Cpf.Replace(".", "").Replace("-", "").Trim();
                cliente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.Cpf == cpfLimpo && c.Ativo);
            }

            if (cliente == null)
            {
                cliente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.Email == criarPedidoDto.Email && c.Ativo);
            }

            if (cliente == null)
            {
                // Criar novo cliente
                cliente = new Cliente
                {
                    Nome = criarPedidoDto.Nome,
                    Email = criarPedidoDto.Email,
                    Telefone = criarPedidoDto.Telefone,
                    Cpf = cpfLimpo, // Salvar CPF sem formatação
                    DataCriacao = DateTime.UtcNow,
                    Ativo = true
                };
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();
            }
            else if (criarPedidoDto.AtualizarCliente ?? false)
            {
                // Atualizar dados do cliente existente
                cliente.Nome = criarPedidoDto.Nome;
                cliente.Email = criarPedidoDto.Email;
                cliente.Telefone = criarPedidoDto.Telefone;
                if (!string.IsNullOrEmpty(cpfLimpo))
                {
                    cliente.Cpf = cpfLimpo; // Salvar CPF sem formatação
                }
                cliente.DataAtualizacao = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Criar endereço de entrega
            var endereco = new Endereco
            {
                ClienteId = cliente.Id,
                Cep = criarPedidoDto.Cep,
                Logradouro = criarPedidoDto.Logradouro,
                Numero = criarPedidoDto.Numero,
                Complemento = criarPedidoDto.Complemento,
                Bairro = criarPedidoDto.Bairro,
                Cidade = criarPedidoDto.Cidade,
                Estado = criarPedidoDto.Estado,
                IsPrincipal = true, // Primeiro endereço é principal
                DataCriacao = DateTime.UtcNow
            };
            _context.Enderecos.Add(endereco);
            await _context.SaveChangesAsync();

            // Calcular total do pedido
            var subtotal = carrinho.Itens.Sum(i => i.Quantidade * i.Produto.Preco);
            // TotalPedido armazena o valor bruto (sem descontos)
            var totalPedido = subtotal + (criarPedidoDto.PrecoFrete ?? 0);

            // Criar pedido
            var pedido = new Pedido
            {
                ClienteId = cliente.Id,
                CarrinhoId = carrinho.Id,
                EnderecoEntregaId = endereco.Id,
                Status = StatusPedido.AguardandoConfirmacao,
                PrecoFrete = criarPedidoDto.PrecoFrete,
                TotalPedido = totalPedido,
                DescontoPorUnidade = criarPedidoDto.DescontoPorUnidade ?? 0m,
                DescontoCupom = criarPedidoDto.DescontoCupom ?? 0m,
                DataPedido = DateTime.UtcNow,
                MetodoPagamento = criarPedidoDto.MetodoPagamento,
                Observacoes = criarPedidoDto.Observacoes
            };
            _context.Pedidos.Add(pedido);

            // Atualizar status do carrinho
            carrinho.Status = StatusCarrinho.Finalizado;
            carrinho.Ativo = false; // impedir reutilização deste carrinho após o checkout
            carrinho.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Retornar pedido criado
            return await GetByIdAsync(pedido.Id) ?? throw new InvalidOperationException("Erro ao criar pedido");
        }

        public async Task<PedidoDto?> UpdateStatusAsync(int id, AtualizarStatusPedidoDto updateDto)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido == null) return null;

            pedido.Status = updateDto.Status;
            pedido.CodigoRastreamento = updateDto.CodigoRastreamento;
            pedido.Observacoes = updateDto.Observacoes;
            pedido.MotivoCancelamento = updateDto.MotivoCancelamento;
            pedido.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetByIdAsync(id);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido == null) return false;

            _context.Pedidos.Remove(pedido);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<PedidoDto> MapPedidoToDto(Pedido pedido)
        {
            var itensDto = pedido.Carrinho.Itens.Select(item => new ItemPedidoDto
            {
                Id = item.Id,
                ProdutoId = item.ProdutoId,
                ProdutoNome = item.Produto.Nome,
                ProdutoSlug = item.Produto.Slug,
                ProdutoPreco = item.Produto.Preco,
                ProdutoImagem = item.Produto.ImagemPrincipal,
                CorId = item.CorId,
                CorNome = item.Cor.Nome,
                CorHex1 = item.Cor.Hex1,
                CorHex2 = item.Cor.Hex2,
                Quantidade = item.Quantidade,
                PrecoTotalItem = item.Quantidade * item.Produto.Preco
            }).ToList();

            return new PedidoDto
            {
                Id = pedido.Id,
                ClienteId = pedido.ClienteId,
                ClienteNome = pedido.Cliente.Nome,
                ClienteEmail = pedido.Cliente.Email,
                Status = pedido.Status,
                PrecoFrete = pedido.PrecoFrete,
                TotalPedido = pedido.TotalPedido,
                DescontoPorUnidade = pedido.DescontoPorUnidade,
                DescontoCupom = pedido.DescontoCupom,
                DataPedido = pedido.DataPedido,
                DataAtualizacao = pedido.DataAtualizacao,
                CodigoRastreamento = pedido.CodigoRastreamento,
                MetodoPagamento = pedido.MetodoPagamento,
                Observacoes = pedido.Observacoes,
                MotivoCancelamento = pedido.MotivoCancelamento,
                EnderecoEntrega = new EnderecoDto
                {
                    Id = pedido.EnderecoEntrega.Id,
                    Cep = pedido.EnderecoEntrega.Cep,
                    Logradouro = pedido.EnderecoEntrega.Logradouro,
                    Numero = pedido.EnderecoEntrega.Numero,
                    Complemento = pedido.EnderecoEntrega.Complemento,
                    Bairro = pedido.EnderecoEntrega.Bairro,
                    Cidade = pedido.EnderecoEntrega.Cidade,
                    Estado = pedido.EnderecoEntrega.Estado
                },
                Itens = itensDto
            };
        }
    }
}
