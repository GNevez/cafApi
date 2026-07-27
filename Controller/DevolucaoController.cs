using cafApi.Contexts;
using cafApi.Models;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Controller;

[ApiController]
[Route("api/[controller]")]
public class DevolucaoController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ICorreiosService _correiosService;
    private readonly IPagarmeService _pagarmeService;

    public DevolucaoController(
        ApplicationDbContext context,
        IEmailService emailService,
        ICorreiosService correiosService,
        IPagarmeService pagarmeService)
    {
        _context = context;
        _emailService = emailService;
        _correiosService = correiosService;
        _pagarmeService = pagarmeService;
    }

    public class CriarDevolucaoItemDto
    {
        public int ItemCarrinhoId { get; set; }
        public int Quantidade { get; set; }
    }

    public class CriarDevolucaoDto
    {
        public int PedidoId { get; set; }
        public string Cpf { get; set; } = null!;
        public string NomeCliente { get; set; } = null!;
        public string Email { get; set; } = null!;
        public List<CriarDevolucaoItemDto> Itens { get; set; } = new();
    }

    [HttpPost]
    public async Task<ActionResult<object>> Criar([FromBody] CriarDevolucaoDto dto)
    {
        if (dto == null) return BadRequest("Dados inválidos");
        if (dto.PedidoId <= 0) return BadRequest("PedidoId inválido");
        if (string.IsNullOrWhiteSpace(dto.Cpf)) return BadRequest("CPF é obrigatório");
        if (string.IsNullOrWhiteSpace(dto.NomeCliente)) return BadRequest("Nome é obrigatório");
        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("Email é obrigatório");
        if (dto.Itens == null || dto.Itens.Count == 0) return BadRequest("Selecione ao menos um item");

        var pedido = await _context.Pedidos
            .Include(p => p.Carrinho)
                .ThenInclude(c => c.Itens)
                    .ThenInclude(i => i.Produto)
            .Include(p => p.Carrinho)
                .ThenInclude(c => c.Itens)
                    .ThenInclude(i => i.Cor)
            .FirstOrDefaultAsync(p => p.Id == dto.PedidoId);

        if (pedido == null) return NotFound("Pedido não encontrado");

        // Validação básica do CPF com o cliente do pedido, se existir
        var cpfLimpo = dto.Cpf.Replace(".", "").Replace("-", "").Trim();
        if (pedido.ClienteId != 0)
        {
            var cliente = await _context.Clientes.FindAsync(pedido.ClienteId);
            if (cliente != null && !string.IsNullOrEmpty(cliente.Cpf))
            {
                if (!string.Equals(cliente.Cpf, cpfLimpo, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "CPF não corresponde ao titular do pedido" });
                }
            }
        }

        var devolucao = new Devolucao
        {
            PedidoId = pedido.Id,
            Cpf = cpfLimpo,
            NomeCliente = dto.NomeCliente,
            Email = dto.Email,
            Status = DevolucaoStatus.Solicitado,
            DataCriacao = DateTime.UtcNow
        };

        foreach (var itemDto in dto.Itens)
        {
            var itemPedido = pedido.Carrinho.Itens.FirstOrDefault(i => i.Id == itemDto.ItemCarrinhoId);
            if (itemPedido == null) return BadRequest($"Item {itemDto.ItemCarrinhoId} não pertence ao pedido");
            if (itemDto.Quantidade <= 0 || itemDto.Quantidade > itemPedido.Quantidade)
                return BadRequest($"Quantidade inválida para o item {itemDto.ItemCarrinhoId}");

            devolucao.Itens.Add(new DevolucaoItem
            {
                ItemCarrinhoId = itemPedido.Id,
                ProdutoId = itemPedido.ProdutoId,
                ProdutoNome = itemPedido.Produto.Nome,
                CorId = itemPedido.CorId,
                CorNome = itemPedido.Cor.Nome,
                Quantidade = itemDto.Quantidade
            });
        }

        _context.Devolucoes.Add(devolucao);
        await _context.SaveChangesAsync();

        // Enviar email de confirmação da solicitação
        await _emailService.EnviarEmailStatusDevolucaoAsync(devolucao, DevolucaoStatus.Solicitado);

        return Ok(new { id = devolucao.Id, status = devolucao.Status.ToString() });
    }

    [HttpGet]
    public async Task<ActionResult<object>> Listar(
        [FromQuery] DevolucaoStatus? status,
        [FromQuery] string? busca,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Devolucoes
            .Include(d => d.Itens)
            .Include(d => d.Pedido)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        // Filtro por busca (PedidoId, CodigoPedido ou PagarmeOrderId)
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var buscaTrimmed = busca.Trim();

            // Tenta converter para int para buscar por PedidoId
            if (int.TryParse(buscaTrimmed, out var pedidoId))
            {
                query = query.Where(d =>
                    d.PedidoId == pedidoId ||
                    d.Pedido.CodigoPedido.Contains(buscaTrimmed) ||
                    (d.Pedido.PagarmeOrderId != null && d.Pedido.PagarmeOrderId.Contains(buscaTrimmed))
                );
            }
            else
            {
                query = query.Where(d =>
                    d.Pedido.CodigoPedido.Contains(buscaTrimmed) ||
                    (d.Pedido.PagarmeOrderId != null && d.Pedido.PagarmeOrderId.Contains(buscaTrimmed))
                );
            }
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(d => d.DataCriacao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.PedidoId,
                CodigoPedido = d.Pedido.CodigoPedido,
                PagarmeOrderId = d.Pedido.PagarmeOrderId,
                d.Cpf,
                NomeCliente = d.NomeCliente,
                d.Email,
                d.Status,
                DataCriacao = d.DataCriacao,
                DataAtualizacao = d.DataAtualizacao,
                // Dados do Correios
                CodigoPostagem = d.CodigoPostagem,
                CodigoRastreamento = d.CodigoRastreamento,
                DataLimitePostagem = d.DataLimitePostagem,
                UrlEtiqueta = d.UrlEtiqueta,
                // Dados do Reembolso
                ValorReembolsado = d.ValorReembolsado,
                DataReembolso = d.DataReembolso,
                PagarmeChargeId = d.PagarmeChargeId,
                PagarmeRefundStatus = d.PagarmeRefundStatus,
                Itens = d.Itens.Select(i => new
                {
                    i.Id,
                    DevolucaoId = i.DevolucaoId,
                    ItemCarrinhoId = i.ItemCarrinhoId,
                    ProdutoId = i.ProdutoId,
                    ProdutoNome = i.ProdutoNome,
                    CorId = i.CorId,
                    CorNome = i.CorNome,
                    Quantidade = i.Quantidade
                }).ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalCount = totalCount,
            HasPreviousPage = page > 1,
            HasNextPage = page < totalPages
        });
    }

    public class AtualizarStatusDto
    {
        public DevolucaoStatus Status { get; set; }
        public string? Observacoes { get; set; }
        public bool EnviarEmail { get; set; } = true;
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult> AtualizarStatus(int id, [FromBody] AtualizarStatusDto dto)
    {
        if (dto == null) return BadRequest("Dados inválidos");

        var devolucao = await _context.Devolucoes
            .Include(d => d.Itens)
            .Include(d => d.Pedido)
                .ThenInclude(p => p.Cliente)
            .Include(d => d.Pedido)
                .ThenInclude(p => p.EnderecoEntrega)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (devolucao == null) return NotFound("Devolução não encontrada");

        var statusAntigo = devolucao.Status;
        devolucao.Status = dto.Status;
        devolucao.DataAtualizacao = DateTime.UtcNow;

        // Se mudou para SolicitacaoEnviada, criar pré-postagem de logística reversa nos Correios
        if (dto.Status == DevolucaoStatus.SolicitacaoEnviada && statusAntigo != DevolucaoStatus.SolicitacaoEnviada)
        {
            try
            {
                var resultadoLogisticaReversa = await _correiosService.CriarPrePostagemLogisticaReversaAsync(devolucao.Id);

                if (resultadoLogisticaReversa != null && resultadoLogisticaReversa.Sucesso)
                {
                    // Atualizar dados da devolução com informações dos Correios
                    devolucao.CodigoPostagem = resultadoLogisticaReversa.CodigoObjeto;
                    devolucao.CodigoRastreamento = resultadoLogisticaReversa.CodigoObjeto;
                    devolucao.DataLimitePostagem = resultadoLogisticaReversa.DataValidade;
                }
                else
                {
                    // Se falhou, retornar erro sem mudar o status
                    return BadRequest(new
                    {
                        message = "Erro ao criar autorização de postagem nos Correios",
                        erro = resultadoLogisticaReversa?.MensagemErro ?? "Erro desconhecido"
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Erro ao criar autorização de postagem nos Correios",
                    erro = ex.Message
                });
            }
        }

        // Se mudou para ReembolsoEmitido, processar reembolso no Pagar.me
        if (dto.Status == DevolucaoStatus.ReembolsoEmitido && statusAntigo != DevolucaoStatus.ReembolsoEmitido)
        {
            try
            {
                var valorReembolso = await CalcularValorReembolsoAsync(devolucao);

                if (valorReembolso <= 0)
                {
                    return BadRequest(new
                    {
                        message = "Não foi possível calcular o valor do reembolso",
                        erro = "Valor do reembolso inválido"
                    });
                }

                // Buscar o pedido para obter o PagarmeOrderId
                var pedido = await _context.Pedidos.FindAsync(devolucao.PedidoId);
                if (pedido == null || string.IsNullOrEmpty(pedido.PagarmeOrderId))
                {
                    return BadRequest(new
                    {
                        message = "Não foi possível processar o reembolso",
                        erro = "Pedido não possui identificação de pagamento no Pagar.me"
                    });
                }

                // Buscar a order no Pagar.me para obter o charge_id
                var orderResponse = await _pagarmeService.GetOrderAsync(pedido.PagarmeOrderId);
                if (orderResponse.Charges == null || !orderResponse.Charges.Any())
                {
                    return BadRequest(new
                    {
                        message = "Não foi possível processar o reembolso",
                        erro = "Nenhuma cobrança encontrada para este pedido"
                    });
                }

                var chargeId = orderResponse.Charges.First().Id;

                Console.WriteLine($"[Devolucao] Processando reembolso - DevolucaoId: {devolucao.Id}, ChargeId: {chargeId}, Valor: R$ {valorReembolso:F2}");

                // Executar o reembolso (valor em reais)
                var refundResponse = await _pagarmeService.RefundChargeAsync(chargeId, valorReembolso);

                // Salvar dados do reembolso na devolução
                devolucao.ValorReembolsado = valorReembolso;
                devolucao.DataReembolso = DateTime.UtcNow;
                devolucao.PagarmeChargeId = chargeId;
                devolucao.PagarmeRefundStatus = refundResponse.Status;

                Console.WriteLine($"[Devolucao] Reembolso processado - Status: {refundResponse.Status}, CanceledAmount: {refundResponse.CanceledAmount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Devolucao] Erro ao processar reembolso: {ex.Message}");
                return BadRequest(new
                {
                    message = "Erro ao processar reembolso no Pagar.me",
                    erro = ex.Message
                });
            }
        }

        await _context.SaveChangesAsync();

        // Enviar email se o status mudou e o envio estiver habilitado
        if (dto.EnviarEmail && statusAntigo != dto.Status)
        {
            await _emailService.EnviarEmailStatusDevolucaoAsync(devolucao, dto.Status, dto.Observacoes);
        }

        return Ok(new
        {
            message = "Status atualizado com sucesso",
            status = devolucao.Status.ToString(),
            codigoPostagem = devolucao.CodigoPostagem,
            dataLimitePostagem = devolucao.DataLimitePostagem
        });
    }

    public class AtualizarDadosCorreiosDto
    {
        public string? CodigoPostagem { get; set; }
        public string? CodigoRastreamento { get; set; }
        public DateTime? DataLimitePostagem { get; set; }
        public string? UrlEtiqueta { get; set; }
        public DevolucaoStatus? NovoStatus { get; set; }
        public bool EnviarEmail { get; set; } = true;
        public string? Observacoes { get; set; }
    }

    /// <summary>
    /// Atualiza dados de logística reversa dos Correios (usado pelo webhook ou manualmente)
    /// </summary>
    [HttpPut("{id}/correios")]
    public async Task<ActionResult> AtualizarDadosCorreios(int id, [FromBody] AtualizarDadosCorreiosDto dto)
    {
        if (dto == null) return BadRequest("Dados inválidos");

        var devolucao = await _context.Devolucoes
            .Include(d => d.Itens)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (devolucao == null) return NotFound("Devolução não encontrada");

        // Atualizar dados dos Correios
        if (!string.IsNullOrEmpty(dto.CodigoPostagem))
            devolucao.CodigoPostagem = dto.CodigoPostagem;

        if (!string.IsNullOrEmpty(dto.CodigoRastreamento))
            devolucao.CodigoRastreamento = dto.CodigoRastreamento;

        if (dto.DataLimitePostagem.HasValue)
            devolucao.DataLimitePostagem = dto.DataLimitePostagem;

        if (!string.IsNullOrEmpty(dto.UrlEtiqueta))
            devolucao.UrlEtiqueta = dto.UrlEtiqueta;

        var statusAntigo = devolucao.Status;

        // Atualizar status se fornecido
        if (dto.NovoStatus.HasValue)
        {
            devolucao.Status = dto.NovoStatus.Value;
        }

        devolucao.DataAtualizacao = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Enviar email se o status mudou e o envio estiver habilitado
        if (dto.EnviarEmail && dto.NovoStatus.HasValue && statusAntigo != dto.NovoStatus.Value)
        {
            await _emailService.EnviarEmailStatusDevolucaoAsync(devolucao, dto.NovoStatus.Value, dto.Observacoes);
        }

        return Ok(new
        {
            message = "Dados atualizados com sucesso",
            status = devolucao.Status.ToString(),
            codigoPostagem = devolucao.CodigoPostagem,
            codigoRastreamento = devolucao.CodigoRastreamento
        });
    }

    /// <summary>
    /// Calcula o valor de reembolso considerando apenas os itens devolvidos,
    /// aplicando os descontos proporcionais (cupom e desconto por quantidade).
    /// NÃO inclui frete no reembolso.
    /// </summary>
    private async Task<decimal> CalcularValorReembolsoAsync(Devolucao devolucao)
    {
        // Buscar o pedido com o carrinho e itens
        var pedido = await _context.Pedidos
            .Include(p => p.Carrinho)
                .ThenInclude(c => c.Itens)
                    .ThenInclude(i => i.Produto)
            .FirstOrDefaultAsync(p => p.Id == devolucao.PedidoId);

        if (pedido == null || pedido.Carrinho == null)
            return 0;

        // Calcular o valor total original do pedido (sem descontos, sem frete)
        var valorTotalOriginalPedido = pedido.Carrinho.Itens.Sum(i => i.Produto.Preco * i.Quantidade);

        if (valorTotalOriginalPedido == 0)
            return 0;

        // Calcular desconto total aplicado no pedido (cupom + quantidade)
        var descontoTotalPedido = pedido.DescontoCupom + pedido.DescontoPorUnidade;

        // Calcular percentual de desconto aplicado
        var percentualDesconto = descontoTotalPedido / valorTotalOriginalPedido;

        // Calcular o valor dos itens devolvidos
        decimal valorItensDevolvidos = 0;

        foreach (var itemDevolucao in devolucao.Itens)
        {
            // Buscar o item do carrinho correspondente
            var itemCarrinho = pedido.Carrinho.Itens.FirstOrDefault(i => i.Id == itemDevolucao.ItemCarrinhoId);
            if (itemCarrinho != null)
            {
                // Valor original do item (preço unitário * quantidade devolvida)
                var valorOriginalItem = itemCarrinho.Produto.Preco * itemDevolucao.Quantidade;

                // Aplicar o mesmo percentual de desconto que foi aplicado no pedido
                var descontoItem = valorOriginalItem * percentualDesconto;
                var valorItemComDesconto = valorOriginalItem - descontoItem;

                valorItensDevolvidos += valorItemComDesconto;
            }
        }

        Console.WriteLine($"[Devolucao] Cálculo de reembolso - ValorTotalOriginal: {valorTotalOriginalPedido:F2}, " +
                         $"DescontoTotal: {descontoTotalPedido:F2}, PercentualDesconto: {percentualDesconto:P2}, " +
                         $"ValorItensDevolvidos: {valorItensDevolvidos:F2}");

        // Arredondar para 2 casas decimais
        return Math.Round(valorItensDevolvidos, 2);
    }
}
