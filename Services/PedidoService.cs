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
        private readonly IEmailService _emailService;

        public PedidoService(
            ApplicationDbContext context,
            ICartService cartService,
            IEmailService emailService)
        {
            _context = context;
            _cartService = cartService;
            _emailService = emailService;
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

        public async Task<PedidoDto?> GetByCodigoPedidoAsync(string codigoPedido)
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
                .FirstOrDefaultAsync(p => p.CodigoPedido == codigoPedido);

            if (pedido == null) return null;

            return await MapPedidoToDto(pedido);
        }

        public async Task<(List<PedidoDto> pedidos, int totalCount)> GetAllAsync(int pageNumber, int pageSize, string? busca = null)
        {
            var baseQuery = _context.Pedidos
                .AsNoTracking()
                .AsQueryable();

            // Filtro por busca (Id, CodigoPedido ou PagarmeOrderId)
            if (!string.IsNullOrWhiteSpace(busca))
            {
                var buscaTrimmed = busca.Trim();
                
                if (int.TryParse(buscaTrimmed, out var pedidoId))
                {
                    baseQuery = baseQuery.Where(p => 
                        p.Id == pedidoId ||
                        p.CodigoPedido.Contains(buscaTrimmed) ||
                        (p.PagarmeOrderId != null && p.PagarmeOrderId.Contains(buscaTrimmed))
                    );
                }
                else
                {
                    baseQuery = baseQuery.Where(p => 
                        p.CodigoPedido.Contains(buscaTrimmed) ||
                        (p.PagarmeOrderId != null && p.PagarmeOrderId.Contains(buscaTrimmed))
                    );
                }
            }

            baseQuery = baseQuery
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

        public async Task<(List<PedidoDto> pedidos, int totalCount)> GetByStatusAsync(StatusPedido status, int pageNumber, int pageSize, string? busca = null)
        {
            var baseQuery = _context.Pedidos
                .AsNoTracking()
                .Where(p => p.Status == status)
                .AsQueryable();

            // Filtro por busca (Id, CodigoPedido ou PagarmeOrderId)
            if (!string.IsNullOrWhiteSpace(busca))
            {
                var buscaTrimmed = busca.Trim();
                
                if (int.TryParse(buscaTrimmed, out var pedidoId))
                {
                    baseQuery = baseQuery.Where(p => 
                        p.Id == pedidoId ||
                        p.CodigoPedido.Contains(buscaTrimmed) ||
                        (p.PagarmeOrderId != null && p.PagarmeOrderId.Contains(buscaTrimmed))
                    );
                }
                else
                {
                    baseQuery = baseQuery.Where(p => 
                        p.CodigoPedido.Contains(buscaTrimmed) ||
                        (p.PagarmeOrderId != null && p.PagarmeOrderId.Contains(buscaTrimmed))
                    );
                }
            }

            baseQuery = baseQuery
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

        public async Task<List<PedidoDto>> GetByCpfAsync(string cpf)
        {
            var cpfLimpo = cpf.Replace(".", "").Replace("-", "").Trim();

            var pedidos = await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.EnderecoEntrega)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Produto)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Cor)
                .Where(p => p.Cliente.Cpf == cpfLimpo)
                .OrderByDescending(p => p.DataPedido)
                .ToListAsync();

            var pedidosDto = new List<PedidoDto>();
            foreach (var pedido in pedidos)
            {
                pedidosDto.Add(await MapPedidoToDto(pedido));
            }

            return pedidosDto;
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

            // Criar endereço de entrega (snapshot do pedido, não marca como principal)
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
                IsPrincipal = false, // Não marca como principal automaticamente
                DataCriacao = DateTime.UtcNow
            };
            _context.Enderecos.Add(endereco);
            await _context.SaveChangesAsync();

            // Calcular total do pedido
            var subtotal = carrinho.Itens.Sum(i => i.Quantidade * i.Produto.Preco);
            // Incluir frete e subtrair descontos (cupom / promoção) para obter o total final
            var descontoPorUnidade = criarPedidoDto.DescontoPorUnidade ?? 0m;
            var descontoCupom = criarPedidoDto.DescontoCupom ?? 0m;
            var precoFrete = criarPedidoDto.PrecoFrete ?? 0m;

            // Calcular juros (se aplicável) usando a maior taxa dos produtos do carrinho
            var parcelasNum = criarPedidoDto.ParcelasNum ?? 1;
            var cartMaxTaxa = carrinho.Itens.Any() ? carrinho.Itens.Max(i => i.Produto.TaxaJuros) : 0m;
            var taxaUsada = parcelasNum > 1 ? cartMaxTaxa : 0m;

            // Aplicar juros sobre: subtotal - desconto de promoção (antes do frete)
            var subtotalComPromo = subtotal - descontoPorUnidade;
            var totalComJurosSobreItens = subtotalComPromo * (1 + taxaUsada);

            var totalPedido = totalComJurosSobreItens + precoFrete - descontoCupom;

            // Garantir que o total não fique negativo
            if (totalPedido < 0) totalPedido = 0m;

            // Validação: verificar se o frontend enviou o total e se bate com o cálculo do servidor
            if (!criarPedidoDto.TotalEnviado.HasValue)
            {
                throw new ArgumentException("Total enviado ausente no payload.");
            }

            // Comparar arredondando para 2 casas (centavos)
            var esperado = Math.Round(totalPedido, 2);
            var enviado = Math.Round(criarPedidoDto.TotalEnviado.Value, 2);
            if (esperado != enviado)
            {
                throw new ArgumentException($"Total informado ({enviado:C2}) não confere com o cálculo do servidor ({esperado:C2}).");
            }

            // Criar pedido com snapshot dos dados do cliente
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
                Observacoes = criarPedidoDto.Observacoes,
                // Snapshot: gravar dados do cliente no momento da compra
                NomeCliente = criarPedidoDto.Nome,
                EmailCliente = criarPedidoDto.Email,
                TelefoneCliente = criarPedidoDto.Telefone,
                CodigoPedido = GenerateCodigoPedido()
            };
            _context.Pedidos.Add(pedido);

            carrinho.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(pedido.Id) ?? throw new InvalidOperationException("Erro ao criar pedido");
        }

        // Calcula o total esperado para um checkout (mesma lógica usada em CreateAsync)
        public async Task<decimal> CalculateTotalAsync(CriarPedidoDto criarPedidoDto, string cartToken)
        {
            var carrinho = await _context.Carrinhos
                .Include(c => c.Itens)
                    .ThenInclude(i => i.Produto)
                .Include(c => c.Itens)
                    .ThenInclude(i => i.Cor)
                .Include(c => c.Cupom)
                .FirstOrDefaultAsync(c => c.Token == cartToken && c.Ativo);

            if (carrinho == null)
                throw new InvalidOperationException("Carrinho não encontrado ou já finalizado");

            var subtotal = carrinho.Itens.Sum(i => i.Quantidade * i.Produto.Preco);
            var descontoPorUnidade = criarPedidoDto.DescontoPorUnidade ?? 0m;
            var descontoCupom = criarPedidoDto.DescontoCupom ?? 0m;
            var precoFrete = criarPedidoDto.PrecoFrete ?? 0m;

            var parcelasNum = criarPedidoDto.ParcelasNum ?? 1;
            var cartMaxTaxa = carrinho.Itens.Any() ? carrinho.Itens.Max(i => i.Produto.TaxaJuros) : 0m;
            var taxaUsada = parcelasNum > 1 ? cartMaxTaxa : 0m;

            var subtotalComPromo = subtotal - descontoPorUnidade;
            var totalComJurosSobreItens = subtotalComPromo * (1 + taxaUsada);
            var totalPedido = totalComJurosSobreItens + precoFrete - descontoCupom;
            if (totalPedido < 0) totalPedido = 0m;

            return Math.Round(totalPedido, 2);
        }

        public async Task<PedidoDto?> UpdateStatusAsync(int id, AtualizarStatusPedidoDto updateDto)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido == null) return null;

            var statusAnterior = pedido.Status;
            pedido.Status = updateDto.Status;
            pedido.CodigoRastreamento = updateDto.CodigoRastreamento;
            pedido.Observacoes = updateDto.Observacoes;
            pedido.MotivoCancelamento = updateDto.MotivoCancelamento;
            pedido.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Enviar email de atualização de status somente se solicitado
            if (statusAnterior != updateDto.Status && updateDto.EnviarEmail)
            {
                await _emailService.EnviarEmailStatusPedidoAsync(
                    pedido,
                    updateDto.Status,
                    updateDto.Observacoes ?? updateDto.MotivoCancelamento
                );
            }

            // Se mudou para EmSeparacao e ainda não registramos uma ENTRADA de venda para esse pedido, cria a transação
            if (statusAnterior != StatusPedido.EmSeparacao && updateDto.Status == StatusPedido.EmSeparacao)
            {
                var jaRegistrado = await _context.Transacoes
                    .AsNoTracking()
                    .AnyAsync(t => t.PedidoId == pedido.Id && t.Tipo == TipoTransacao.Entrada && t.Descricao.StartsWith("Entrada de venda do pedido #"));

                if (!jaRegistrado)
                {
                    var transacao = new Transacao
                    {
                        Tipo = TipoTransacao.Entrada,
                        Valor = pedido.TotalPedido,
                        Descricao = $"Entrada de venda do pedido #{pedido.Id}",
                        MetodoPagamento = "venda",
                        PedidoId = pedido.Id,
                        DataTransacao = DateTime.UtcNow,
                        DataCriacao = DateTime.UtcNow
                    };

                    _context.Transacoes.Add(transacao);
                    await _context.SaveChangesAsync();
                }
            }
            return await GetByIdAsync(id);
        }

        public async Task<PedidoDto?> UpdateStatusByCodigoPedidoAsync(string codigoPedido, AtualizarStatusPedidoDto updateDto)
        {
            var pedido = await _context.Pedidos.FirstOrDefaultAsync(p => p.CodigoPedido == codigoPedido);
            if (pedido == null) return null;

            var statusAnterior = pedido.Status;
            pedido.Status = updateDto.Status;
            pedido.CodigoRastreamento = updateDto.CodigoRastreamento;
            pedido.Observacoes = updateDto.Observacoes;
            pedido.MotivoCancelamento = updateDto.MotivoCancelamento;
            pedido.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (statusAnterior != updateDto.Status && updateDto.EnviarEmail)
            {
                await _emailService.EnviarEmailStatusPedidoAsync(
                    pedido,
                    updateDto.Status,
                    updateDto.Observacoes ?? updateDto.MotivoCancelamento
                );
            }

            if (statusAnterior != StatusPedido.EmSeparacao && updateDto.Status == StatusPedido.EmSeparacao)
            {
                var jaRegistrado = await _context.Transacoes
                    .AsNoTracking()
                    .AnyAsync(t => t.PedidoId == pedido.Id && t.Tipo == TipoTransacao.Entrada && t.Descricao.StartsWith("Entrada de venda do pedido #"));

                if (!jaRegistrado)
                {
                    var transacao = new Transacao
                    {
                        Tipo = TipoTransacao.Entrada,
                        Valor = pedido.TotalPedido,
                        Descricao = $"Entrada de venda do pedido #{pedido.CodigoPedido}",
                        MetodoPagamento = "venda",
                        PedidoId = pedido.Id,
                        DataTransacao = DateTime.UtcNow,
                        DataCriacao = DateTime.UtcNow
                    };

                    _context.Transacoes.Add(transacao);
                    await _context.SaveChangesAsync();
                }
            }
            return await GetByCodigoPedidoAsync(codigoPedido);
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
                CodigoPedido = pedido.CodigoPedido,
                ClienteId = pedido.ClienteId,
                ClienteNome = pedido.NomeCliente,
                ClienteEmail = pedido.EmailCliente,
                ClienteTelefone = pedido.TelefoneCliente,
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

        private string GenerateCodigoPedido()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = new Random().Next(100, 999);
            return $"CAF-{timestamp}{random}";
        }
    }
}
