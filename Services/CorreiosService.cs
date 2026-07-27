using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services;

public class CorreiosService : ICorreiosService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CorreiosService> _logger;
    
    // Cache do token
    private static string? _cachedToken;
    private static DateTime _tokenExpiration = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public CorreiosService(
        ApplicationDbContext context,
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<CorreiosService> logger)
    {
        _context = context;
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(_configuration["Correios:BaseUrl"] ?? "https://api.correios.com.br");
    }

    #region Autenticação

    public async Task<CorreiosTokenResponse?> ObterTokenAsync()
    {
        await _tokenLock.WaitAsync();
        try
        {
            // Verificar se o token ainda é válido (com margem de 5 minutos)
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
            {
                return new CorreiosTokenResponse
                {
                    Token = _cachedToken,
                    Expira = _tokenExpiration,
                    CartaoPostagem = _configuration["Correios:CartaoPostagem"]
                };
            }

            var usuario = _configuration["Correios:Usuario"];
            var codigoAcesso = _configuration["Correios:CodigoAcesso"];
            var cartaoPostagem = _configuration["Correios:CartaoPostagem"];

            // Criar autenticação Basic
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:{codigoAcesso}"));
            
            var request = new HttpRequestMessage(HttpMethod.Post, "/token/v1/autentica/cartaopostagem");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { numero = cartaoPostagem }),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("[Correios] Solicitando novo token de autenticação...");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[Correios] Erro ao obter token: {StatusCode} - {Content}", response.StatusCode, content);
                return null;
            }

            var tokenData = JsonSerializer.Deserialize<JsonElement>(content);
            
            _cachedToken = tokenData.GetProperty("token").GetString();
            
            // Token dos Correios geralmente expira em 1 hora
            if (tokenData.TryGetProperty("expiraEm", out var expiraEl))
            {
                _tokenExpiration = DateTime.Parse(expiraEl.GetString()!);
            }
            else
            {
                _tokenExpiration = DateTime.UtcNow.AddHours(1);
            }

            _logger.LogInformation("[Correios] Token obtido com sucesso. Expira em: {Expira}", _tokenExpiration);

            return new CorreiosTokenResponse
            {
                Token = _cachedToken,
                Expira = _tokenExpiration,
                CartaoPostagem = cartaoPostagem,
                Contrato = tokenData.TryGetProperty("contrato", out var contratoEl) ? contratoEl.GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao obter token");
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HttpRequestMessage> CriarRequestAutenticadoAsync(HttpMethod method, string endpoint)
    {
        var tokenResponse = await ObterTokenAsync();
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.Token))
        {
            throw new InvalidOperationException("Não foi possível obter token de autenticação dos Correios");
        }

        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.Token);
        return request;
    }

    #endregion

    #region Pré-Postagem

    public async Task<PrePostagemResponseDto?> CriarPrePostagemAsync(CriarPrePostagemDto dto)
    {
        try
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.EnderecoEntrega)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Produto)
                .FirstOrDefaultAsync(p => p.Id == dto.PedidoId);

            if (pedido == null)
            {
                _logger.LogWarning("[Correios] Pedido {PedidoId} não encontrado", dto.PedidoId);
                return null;
            }

            // Verificar se já existe pré-postagem para este pedido
            var prePostagemExistente = await _context.Set<PrePostagem>()
                .FirstOrDefaultAsync(p => p.PedidoId == dto.PedidoId && p.Status != StatusPrePostagem.Cancelada && p.Status != StatusPrePostagem.Erro);

            if (prePostagemExistente != null)
            {
                _logger.LogWarning("[Correios] Já existe pré-postagem para o pedido {PedidoId}", dto.PedidoId);
                return await MapToResponseDto(prePostagemExistente);
            }

            var endereco = pedido.EnderecoEntrega;
            if (endereco == null)
            {
                _logger.LogError("[Correios] Pedido {PedidoId} não possui endereço de entrega", dto.PedidoId);
                return null;
            }

            // Criar objeto da pré-postagem para API dos Correios
            var prePostagemRequest = CriarRequestPrePostagem(pedido, dto);

            var request = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/prepostagem/v1/prepostagens");
            request.Content = new StringContent(
                JsonSerializer.Serialize(prePostagemRequest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("[Correios] Enviando pré-postagem para pedido {PedidoId}...", dto.PedidoId);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta pré-postagem: {StatusCode} - {Content}", response.StatusCode, content);

            // Criar registro no banco
            var prePostagem = new PrePostagem
            {
                PedidoId = dto.PedidoId,
                CodigoServico = dto.CodigoServico,
                NomeServico = ServicosCorreios.GetNome(dto.CodigoServico),
                Peso = dto.Peso ?? 0.3m,
                Altura = dto.Altura ?? 5,
                Largura = dto.Largura ?? 15,
                Comprimento = dto.Comprimento ?? 20,
                ValorDeclarado = dto.ValorDeclarado ?? pedido.TotalPedido,
                RespostaCorreiosJson = content
            };

            if (response.IsSuccessStatusCode)
            {
                var responseData = JsonSerializer.Deserialize<JsonElement>(content);
                
                if (responseData.TryGetProperty("codigoObjeto", out var codigoEl))
                {
                    prePostagem.CodigoRastreamento = codigoEl.GetString();
                }
                if (responseData.TryGetProperty("id", out var idEl))
                {
                    prePostagem.IdPrePostagem = idEl.GetString();
                }
                if (responseData.TryGetProperty("numeroEtiqueta", out var etiquetaEl))
                {
                    prePostagem.NumeroEtiqueta = etiquetaEl.GetString();
                }
                
                prePostagem.Status = StatusPrePostagem.Gerada;
                
                // Atualizar código de rastreamento no pedido
                if (!string.IsNullOrEmpty(prePostagem.CodigoRastreamento))
                {
                    pedido.CodigoRastreamento = prePostagem.CodigoRastreamento;
                }

                _logger.LogInformation("[Correios] Pré-postagem criada com sucesso. Código: {Codigo}", prePostagem.CodigoRastreamento);
            }
            else
            {
                prePostagem.Status = StatusPrePostagem.Erro;
                prePostagem.MensagemErro = $"Erro {response.StatusCode}: {content}";
                _logger.LogError("[Correios] Erro ao criar pré-postagem: {Error}", prePostagem.MensagemErro);
            }

            _context.Set<PrePostagem>().Add(prePostagem);
            await _context.SaveChangesAsync();

            return await MapToResponseDto(prePostagem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao criar pré-postagem para pedido {PedidoId}", dto.PedidoId);
            
            // Salvar erro no banco
            var prePostagemErro = new PrePostagem
            {
                PedidoId = dto.PedidoId,
                CodigoServico = dto.CodigoServico,
                NomeServico = ServicosCorreios.GetNome(dto.CodigoServico),
                Status = StatusPrePostagem.Erro,
                MensagemErro = ex.Message
            };
            
            _context.Set<PrePostagem>().Add(prePostagemErro);
            await _context.SaveChangesAsync();
            
            return await MapToResponseDto(prePostagemErro);
        }
    }

    public async Task<PrePostagemResponseDto?> CriarPrePostagemParaPedidoAsync(int pedidoId, string? codigoServico = null)
    {
        return await CriarPrePostagemAsync(new CriarPrePostagemDto
        {
            PedidoId = pedidoId,
            CodigoServico = codigoServico ?? "03220" // SEDEX padrão
        });
    }

    private object CriarRequestPrePostagem(Pedido pedido, CriarPrePostagemDto dto)
    {
        var endereco = pedido.EnderecoEntrega!;
        var config = _configuration;

        // Telefone do remetente - separar DDD do número
        var telefoneRemetenteRaw = config["Correios:RemetenteTelefone"]?.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "") ?? "";
        var dddRemetente = telefoneRemetenteRaw.Length >= 2 ? telefoneRemetenteRaw.Substring(0, 2) : "";
        var telefoneRemetente = telefoneRemetenteRaw.Length > 2 ? telefoneRemetenteRaw.Substring(2) : "";
        if (telefoneRemetente.Length > 9) telefoneRemetente = telefoneRemetente.Substring(0, 9);

        // Telefone do destinatário - separar DDD do número
        var telefoneDestinatarioRaw = pedido.Cliente?.Telefone?.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "") ?? "";
        var dddDestinatario = telefoneDestinatarioRaw.Length >= 2 ? telefoneDestinatarioRaw.Substring(0, 2) : "";
        var telefoneDestinatario = telefoneDestinatarioRaw.Length > 2 ? telefoneDestinatarioRaw.Substring(2) : "";
        if (telefoneDestinatario.Length > 9) telefoneDestinatario = telefoneDestinatario.Substring(0, 9);

        // Quantidade total de itens e valor dos produtos
        var quantidadeItens = pedido.Carrinho?.Itens?.Sum(i => i.Quantidade) ?? 1;
        var valorProdutos = pedido.Carrinho?.Itens?.Sum(i => i.Quantidade * (i.Produto?.Preco ?? 0)) ?? pedido.TotalPedido;

        return new
        {
            idCorreios = Guid.NewGuid().ToString(),
            remetente = new
            {
                nome = config["Correios:RemetenteNome"],
                cpfCnpj = config["Correios:RemetenteCPFCNPJ"]?.Replace(".", "").Replace("-", "").Replace("/", ""),
                dddCelular = dddRemetente,
                celular = telefoneRemetente,
                email = config["Correios:RemetenteEmail"],
                endereco = new
                {
                    cep = config["Correios:RemetenteCEP"]?.Replace("-", ""),
                    logradouro = config["Correios:RemetenteLogradouro"],
                    numero = config["Correios:RemetenteNumero"],
                    bairro = config["Correios:RemetenteBairro"],
                    cidade = config["Correios:RemetenteCidade"],
                    uf = config["Correios:RemetenteUF"]
                }
            },
            destinatario = new
            {
                nome = pedido.Cliente?.Nome ?? "Cliente",
                cpfCnpj = pedido.Cliente?.Cpf?.Replace(".", "").Replace("-", ""),
                dddCelular = dddDestinatario,
                celular = telefoneDestinatario,
                email = pedido.Cliente?.Email,
                endereco = new
                {
                    cep = endereco.Cep?.Replace("-", ""),
                    logradouro = endereco.Logradouro,
                    numero = endereco.Numero,
                    complemento = endereco.Complemento ?? "",
                    bairro = endereco.Bairro,
                    cidade = endereco.Cidade,
                    uf = endereco.Estado
                }
            },
            codigoServico = dto.CodigoServico,
            numeroCartaoPostagem = config["Correios:CartaoPostagem"],
            pesoInformado = ((int)((dto.Peso ?? 0.3m) * 1000)).ToString(), // Converter para gramas como string
            codigoFormatoObjetoInformado = "2", // 2 = Caixa/Pacote
            alturaInformada = (dto.Altura ?? 5).ToString(),
            larguraInformada = (dto.Largura ?? 15).ToString(),
            comprimentoInformado = (dto.Comprimento ?? 20).ToString(),
            cienteObjetoNaoProibido = 1, 
            itensDeclaracaoConteudo = new[]
            {
                new
                {
                    conteudo = "Oculos e Acessorios",
                    quantidade = quantidadeItens.ToString(),
                    valor = valorProdutos.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                }
            },
            listaServicoAdicional = new[]
            {
                new 
                { 
                    codigoServicoAdicional = "019", // Aviso de Recebimento
                    tipoServicoAdicional = "AR",
                    valorDeclarado = valorProdutos.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                }
            },
            observacao = $"Pedido #{pedido.CodigoPedido}"
        };
    }

    #endregion

    #region Logística Reversa (Devoluções)

    /// <summary>
    /// Cria uma pré-postagem de logística reversa para devolução.
    /// O CLIENTE é o remetente (quem envia) e a LOJA é o destinatário (quem recebe).
    /// Usa o MESMO serviço do envio original (PAC ou SEDEX) com flag logisticaReversa=S
    /// </summary>
    public async Task<LogisticaReversaResponseDto?> CriarPrePostagemLogisticaReversaAsync(int devolucaoId, string? codigoServico = null)
    {
        try
        {
            // Buscar devolução com relacionamentos
            var devolucao = await _context.Devolucoes
                .Include(d => d.Itens)
                .Include(d => d.Pedido)
                    .ThenInclude(p => p.Cliente)
                .Include(d => d.Pedido)
                    .ThenInclude(p => p.EnderecoEntrega)
                .FirstOrDefaultAsync(d => d.Id == devolucaoId);

            if (devolucao == null)
            {
                _logger.LogWarning("[Correios] Devolução {DevolucaoId} não encontrada", devolucaoId);
                return new LogisticaReversaResponseDto { Sucesso = false, MensagemErro = "Devolução não encontrada" };
            }

            // Verificar se já existe código de postagem
            if (!string.IsNullOrEmpty(devolucao.CodigoPostagem))
            {
                _logger.LogWarning("[Correios] Devolução {DevolucaoId} já possui código de postagem: {Codigo}",
                    devolucaoId, devolucao.CodigoPostagem);
                return new LogisticaReversaResponseDto
                {
                    Sucesso = true,
                    CodigoObjeto = devolucao.CodigoPostagem,
                    DataValidade = devolucao.DataLimitePostagem,
                    MensagemErro = "Já existe autorização de postagem para esta devolução"
                };
            }

            var pedido = devolucao.Pedido;
            var enderecoCliente = pedido.EnderecoEntrega;

            if (enderecoCliente == null)
            {
                _logger.LogError("[Correios] Devolução {DevolucaoId} - Pedido não possui endereço", devolucaoId);
                return new LogisticaReversaResponseDto { Sucesso = false, MensagemErro = "Pedido não possui endereço de entrega" };
            }

            // Determinar código do serviço REVERSO:
            // 1. Se foi passado explicitamente (já é reverso), usar ele
            // 2. Buscar serviço da pré-postagem original e converter para o reverso equivalente
            // 3. Default para PAC Reverso (03301)
            var servicoFinal = codigoServico;
            if (string.IsNullOrEmpty(servicoFinal))
            {
                // Buscar a pré-postagem original do pedido para usar o serviço reverso equivalente
                var prePostagemOriginal = await _context.Set<PrePostagem>()
                    .Where(p => p.PedidoId == pedido.Id && p.Status != StatusPrePostagem.Cancelada && p.Status != StatusPrePostagem.Erro)
                    .OrderByDescending(p => p.DataCriacao)
                    .FirstOrDefaultAsync();

                if (prePostagemOriginal != null)
                {
                    // Converter serviço normal para reverso equivalente
                    servicoFinal = ServicosCorreios.GetCodigoReverso(prePostagemOriginal.CodigoServico);
                    _logger.LogInformation("[Correios] Serviço original: {Original} ({Nome}) → Reverso: {Reverso} ({NomeReverso})", 
                        prePostagemOriginal.CodigoServico, prePostagemOriginal.NomeServico,
                        servicoFinal, ServicosCorreios.GetNome(servicoFinal));
                }
                else
                {
                    // Default para PAC Reverso se não encontrar o original
                    servicoFinal = "03301";
                    _logger.LogInformation("[Correios] Pré-postagem original não encontrada, usando PAC Reverso (03301)");
                }
            }

            // Criar objeto da pré-postagem para Logística Reversa
            var prePostagemRequest = CriarRequestPrePostagemLogisticaReversa(devolucao, servicoFinal);

            var request = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/prepostagem/v1/prepostagens");
            request.Content = new StringContent(
                JsonSerializer.Serialize(prePostagemRequest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("[Correios] Enviando pré-postagem LOGÍSTICA REVERSA para devolução {DevolucaoId} (Serviço: {Servico})...", 
                devolucaoId, servicoFinal);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta logística reversa: {StatusCode} - {Content}", response.StatusCode, content);

            var resultado = new LogisticaReversaResponseDto
            {
                RespostaJson = content,
                DataEmissao = DateTime.UtcNow,
                NomeServico = ServicosCorreios.GetNome(servicoFinal) + " (Reverso)",
                QuantidadeObjetos = 1
            };

            if (response.IsSuccessStatusCode)
            {
                var responseData = JsonSerializer.Deserialize<JsonElement>(content);

                if (responseData.TryGetProperty("codigoObjeto", out var codigoEl))
                {
                    resultado.CodigoObjeto = codigoEl.GetString();
                }
                if (responseData.TryGetProperty("id", out var idEl))
                {
                    resultado.IdPrePostagem = idEl.GetString();
                }

                // Data de validade: 30 dias a partir de hoje
                resultado.DataValidade = DateTime.UtcNow.AddDays(30);
                resultado.Sucesso = true;

                _logger.LogInformation("[Correios] Logística reversa criada com sucesso. Código: {Codigo}", resultado.CodigoObjeto);
            }
            else
            {
                resultado.Sucesso = false;
                resultado.MensagemErro = $"Erro {response.StatusCode}: {content}";
                _logger.LogError("[Correios] Erro ao criar logística reversa: {Error}", resultado.MensagemErro);
            }

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao criar logística reversa para devolução {DevolucaoId}", devolucaoId);
            return new LogisticaReversaResponseDto
            {
                Sucesso = false,
                MensagemErro = ex.Message
            };
        }
    }

    /// <summary>
    /// Cria o request de pré-postagem para logística reversa.
    /// Na logística reversa: CLIENTE é remetente, LOJA é destinatário (inverso do envio normal)
    /// </summary>
    private object CriarRequestPrePostagemLogisticaReversa(Devolucao devolucao, string codigoServico)
    {
        var pedido = devolucao.Pedido;
        var enderecoCliente = pedido.EnderecoEntrega!;
        var config = _configuration;

        // Data de validade: 30 dias a partir de hoje
        var dataValidade = DateTime.UtcNow.AddDays(30).ToString("dd/MM/yyyy");

        // Telefone do cliente (REMETENTE na logística reversa) - separar DDD do número
        var telefoneClienteRaw = pedido.Cliente?.Telefone?.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "") ?? "";
        var dddCliente = telefoneClienteRaw.Length >= 2 ? telefoneClienteRaw.Substring(0, 2) : "";
        var telefoneCliente = telefoneClienteRaw.Length > 2 ? telefoneClienteRaw.Substring(2) : "";
        if (telefoneCliente.Length > 9) telefoneCliente = telefoneCliente.Substring(0, 9);

        // Telefone da loja (DESTINATÁRIO na logística reversa) - separar DDD do número
        var telefoneLojaRaw = config["Correios:RemetenteTelefone"]?.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "") ?? "";
        var dddLoja = telefoneLojaRaw.Length >= 2 ? telefoneLojaRaw.Substring(0, 2) : "";
        var telefoneLoja = telefoneLojaRaw.Length > 2 ? telefoneLojaRaw.Substring(2) : "";
        if (telefoneLoja.Length > 9) telefoneLoja = telefoneLoja.Substring(0, 9);

        // Quantidade e valor dos itens
        var quantidadeItens = devolucao.Itens?.Sum(i => i.Quantidade) ?? 1;

        return new
        {
            idCorreios = Guid.NewGuid().ToString(),

            // REMETENTE = CLIENTE (quem vai enviar o produto de volta)
            remetente = new
            {
                nome = devolucao.NomeCliente ?? pedido.Cliente?.Nome ?? "Cliente",
                cpfCnpj = devolucao.Cpf?.Replace(".", "").Replace("-", "") ?? pedido.Cliente?.Cpf?.Replace(".", "").Replace("-", ""),
                dddCelular = dddCliente,
                celular = telefoneCliente,
                email = devolucao.Email ?? pedido.Cliente?.Email,
                endereco = new
                {
                    cep = enderecoCliente.Cep?.Replace("-", ""),
                    logradouro = enderecoCliente.Logradouro,
                    numero = enderecoCliente.Numero,
                    complemento = enderecoCliente.Complemento ?? "",
                    bairro = enderecoCliente.Bairro,
                    cidade = enderecoCliente.Cidade,
                    uf = enderecoCliente.Estado
                }
            },

            // DESTINATÁRIO = LOJA (quem vai receber o produto de volta)
            destinatario = new
            {
                nome = config["Correios:RemetenteNome"],
                cpfCnpj = config["Correios:RemetenteCPFCNPJ"]?.Replace(".", "").Replace("-", "").Replace("/", ""),
                dddCelular = dddLoja,
                celular = telefoneLoja,
                email = config["Correios:RemetenteEmail"],
                endereco = new
                {
                    cep = config["Correios:RemetenteCEP"]?.Replace("-", ""),
                    logradouro = config["Correios:RemetenteLogradouro"],
                    numero = config["Correios:RemetenteNumero"],
                    bairro = config["Correios:RemetenteBairro"],
                    cidade = config["Correios:RemetenteCidade"],
                    uf = config["Correios:RemetenteUF"]
                }
            },

            codigoServico = codigoServico, // 04677 = PAC Reverso
            numeroCartaoPostagem = config["Correios:CartaoPostagem"],
            pesoInformado = "300", // 300g padrão para devolução
            codigoFormatoObjetoInformado = "2", // 2 = Caixa/Pacote
            alturaInformada = "5",
            larguraInformada = "15",
            comprimentoInformado = "20",
            cienteObjetoNaoProibido = 1,

            itensDeclaracaoConteudo = new[]
            {
                new
                {
                    conteudo = "Oculos e Acessorios - Devolucao",
                    quantidade = quantidadeItens.ToString(),
                    valor = "0.00" // Valor zero para devolução
                }
            },

            observacao = $"Devolução #{devolucao.Id} - Pedido #{pedido.CodigoPedido}",

            // CAMPOS ESPECÍFICOS DE LOGÍSTICA REVERSA
            logisticaReversa = "S",
            dataValidadeLogReversa = dataValidade,
            prazoPostagem = dataValidade
        };
    }

    #endregion

    #region Pré-Postagem Consultas

    public async Task<PrePostagemResponseDto?> GetPrePostagemByIdAsync(int id)
    {
        var prePostagem = await _context.Set<PrePostagem>()
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Cliente)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.EnderecoEntrega)
            .FirstOrDefaultAsync(p => p.Id == id);

        return prePostagem != null ? await MapToResponseDto(prePostagem) : null;
    }

    public async Task<PrePostagemResponseDto?> GetPrePostagemByPedidoIdAsync(int pedidoId)
    {
        var prePostagem = await _context.Set<PrePostagem>()
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Cliente)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.EnderecoEntrega)
            .FirstOrDefaultAsync(p => p.PedidoId == pedidoId && p.Status != StatusPrePostagem.Cancelada);

        return prePostagem != null ? await MapToResponseDto(prePostagem) : null;
    }

    public async Task<PrePostagemPaginadoDto> GetPrePostagensAsync(PrePostagemFiltroDto filtro)
    {
        var query = _context.Set<PrePostagem>()
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Cliente)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.EnderecoEntrega)
            .AsQueryable();

        if (filtro.Status.HasValue)
        {
            query = query.Where(p => p.Status == filtro.Status.Value);
        }

        if (filtro.DataInicio.HasValue)
        {
            query = query.Where(p => p.DataCriacao >= filtro.DataInicio.Value);
        }

        if (filtro.DataFim.HasValue)
        {
            query = query.Where(p => p.DataCriacao <= filtro.DataFim.Value);
        }

        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(p => p.DataCriacao)
            .Skip((filtro.PageNumber - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .ToListAsync();

        var itemsDto = new List<PrePostagemResponseDto>();
        foreach (var item in items)
        {
            itemsDto.Add(await MapToResponseDto(item));
        }

        return new PrePostagemPaginadoDto
        {
            Items = itemsDto,
            TotalCount = totalCount,
            PageNumber = filtro.PageNumber,
            PageSize = filtro.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filtro.PageSize)
        };
    }

    public async Task<bool> CancelarPrePostagemAsync(int id)
    {
        var prePostagem = await _context.Set<PrePostagem>().FindAsync(id);
        if (prePostagem == null) return false;

        // Só pode cancelar se ainda não foi postada
        if (prePostagem.Status != StatusPrePostagem.Pendente && prePostagem.Status != StatusPrePostagem.Gerada)
        {
            return false;
        }

        // Se tem código de rastreamento, tentar cancelar nos Correios
        if (!string.IsNullOrEmpty(prePostagem.IdPrePostagem))
        {
            try
            {
                var request = await CriarRequestAutenticadoAsync(HttpMethod.Delete, $"/prepostagem/v1/prepostagens/{prePostagem.IdPrePostagem}");
                var response = await _httpClient.SendAsync(request);
                
                _logger.LogInformation("[Correios] Cancelamento pré-postagem {Id}: {Status}", prePostagem.IdPrePostagem, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Correios] Erro ao cancelar pré-postagem nos Correios");
            }
        }

        prePostagem.Status = StatusPrePostagem.Cancelada;
        prePostagem.Observacoes = $"Cancelada em {DateTime.UtcNow:dd/MM/yyyy HH:mm}";
        await _context.SaveChangesAsync();

        return true;
    }

    private async Task<PrePostagemResponseDto> MapToResponseDto(PrePostagem prePostagem)
    {
        // Carregar dados relacionados se necessário
        if (prePostagem.Pedido == null)
        {
            await _context.Entry(prePostagem)
                .Reference(p => p.Pedido)
                .LoadAsync();
            
            if (prePostagem.Pedido != null)
            {
                await _context.Entry(prePostagem.Pedido)
                    .Reference(p => p.Cliente)
                    .LoadAsync();
                await _context.Entry(prePostagem.Pedido)
                    .Reference(p => p.EnderecoEntrega)
                    .LoadAsync();
            }
        }

        var endereco = prePostagem.Pedido?.EnderecoEntrega;

        return new PrePostagemResponseDto
        {
            Id = prePostagem.Id,
            PedidoId = prePostagem.PedidoId,
            CodigoPedido = prePostagem.Pedido?.CodigoPedido,
            ClienteNome = prePostagem.Pedido?.Cliente?.Nome,
            CodigoRastreamento = prePostagem.CodigoRastreamento,
            IdPrePostagem = prePostagem.IdPrePostagem,
            NumeroEtiqueta = prePostagem.NumeroEtiqueta,
            CodigoServico = prePostagem.CodigoServico,
            NomeServico = prePostagem.NomeServico,
            Peso = prePostagem.Peso,
            Altura = prePostagem.Altura,
            Largura = prePostagem.Largura,
            Comprimento = prePostagem.Comprimento,
            ValorDeclarado = prePostagem.ValorDeclarado,
            Status = prePostagem.Status,
            StatusNome = prePostagem.Status.ToString(),
            DataCriacao = prePostagem.DataCriacao,
            DataPostagem = prePostagem.DataPostagem,
            DataEntrega = prePostagem.DataEntrega,
            Observacoes = prePostagem.Observacoes,
            MensagemErro = prePostagem.MensagemErro,
            Destinatario = endereco != null ? new DestinatarioDto
            {
                Nome = prePostagem.Pedido?.Cliente?.Nome,
                Logradouro = endereco.Logradouro,
                Numero = endereco.Numero,
                Complemento = endereco.Complemento,
                Bairro = endereco.Bairro,
                Cidade = endereco.Cidade,
                UF = endereco.Estado,
                CEP = endereco.Cep
            } : null
        };
    }

    #endregion

    #region Rastreamento (SRO - Rastro)

    public async Task<RastreamentoResponseDto?> RastrearObjetoAsync(string codigoRastreamento)
    {
        try
        {
            var request = await CriarRequestAutenticadoAsync(HttpMethod.Get, $"/srorastro/v1/objetos/{codigoRastreamento}?resultado=T");
            
            _logger.LogInformation("[Correios] Rastreando objeto {Codigo}...", codigoRastreamento);
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta rastreamento: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                return new RastreamentoResponseDto
                {
                    CodigoObjeto = codigoRastreamento,
                    Mensagem = $"Erro ao rastrear: {response.StatusCode}"
                };
            }

            var data = JsonSerializer.Deserialize<JsonElement>(content);
            var resultado = new RastreamentoResponseDto
            {
                CodigoObjeto = codigoRastreamento
            };

            if (data.TryGetProperty("objetos", out var objetosEl) && objetosEl.GetArrayLength() > 0)
            {
                var objeto = objetosEl[0];
                
                if (objeto.TryGetProperty("tipoPostal", out var tipoEl))
                {
                    if (tipoEl.TryGetProperty("descricao", out var descEl))
                    {
                        resultado.TipoPostal = descEl.GetString();
                    }
                }

                if (objeto.TryGetProperty("eventos", out var eventosEl))
                {
                    foreach (var evento in eventosEl.EnumerateArray())
                    {
                        var eventoDto = new EventoRastreamentoDto();
                        
                        if (evento.TryGetProperty("dtHrCriado", out var dtEl))
                        {
                            eventoDto.DataHora = DateTime.Parse(dtEl.GetString()!);
                        }
                        if (evento.TryGetProperty("descricao", out var descEvEl))
                        {
                            eventoDto.Descricao = descEvEl.GetString();
                        }
                        if (evento.TryGetProperty("tipo", out var tipoEvEl))
                        {
                            eventoDto.Tipo = tipoEvEl.GetString();
                        }
                        if (evento.TryGetProperty("unidade", out var unidadeEl))
                        {
                            if (unidadeEl.TryGetProperty("nome", out var nomeUnEl))
                            {
                                eventoDto.Unidade = nomeUnEl.GetString();
                            }
                            if (unidadeEl.TryGetProperty("endereco", out var endUnEl))
                            {
                                if (endUnEl.TryGetProperty("cidade", out var cidadeEl))
                                {
                                    eventoDto.Cidade = cidadeEl.GetString();
                                }
                                if (endUnEl.TryGetProperty("uf", out var ufEl))
                                {
                                    eventoDto.UF = ufEl.GetString();
                                }
                            }
                        }

                        resultado.Eventos.Add(eventoDto);
                    }
                }
            }
            else
            {
                resultado.Mensagem = "Objeto não encontrado";
            }

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao rastrear objeto {Codigo}", codigoRastreamento);
            return new RastreamentoResponseDto
            {
                CodigoObjeto = codigoRastreamento,
                Mensagem = $"Erro: {ex.Message}"
            };
        }
    }

    public async Task<List<RastreamentoResponseDto>> RastrearMultiplosObjetosAsync(List<string> codigos)
    {
        var resultados = new List<RastreamentoResponseDto>();
        
        foreach (var codigo in codigos.Take(50)) // Limite de 50 objetos
        {
            var resultado = await RastrearObjetoAsync(codigo);
            if (resultado != null)
            {
                resultados.Add(resultado);
            }
        }

        return resultados;
    }

    #endregion

    #region Suspensão de Entrega (SRO - Interatividade)

    public async Task<SuspenderEntregaResponseDto> SuspenderEntregaAsync(SuspenderEntregaRequestDto dto)
    {
        try
        {
            var request = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/srointeratividade/v1/solicitacoes/suspender");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    codigoObjeto = dto.CodigoRastreamento,
                    motivoSuspensao = dto.Motivo,
                    numeroCartaoPostagem = _configuration["Correios:CartaoPostagem"]
                }),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("[Correios] Suspendendo entrega {Codigo}...", dto.CodigoRastreamento);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta suspensão: {StatusCode} - {Content}", response.StatusCode, content);

            if (response.IsSuccessStatusCode)
            {
                return new SuspenderEntregaResponseDto
                {
                    Sucesso = true,
                    CodigoRastreamento = dto.CodigoRastreamento,
                    Mensagem = "Entrega suspensa com sucesso",
                    DataSuspensao = DateTime.UtcNow
                };
            }

            return new SuspenderEntregaResponseDto
            {
                Sucesso = false,
                CodigoRastreamento = dto.CodigoRastreamento,
                Mensagem = $"Erro ao suspender: {content}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao suspender entrega {Codigo}", dto.CodigoRastreamento);
            return new SuspenderEntregaResponseDto
            {
                Sucesso = false,
                CodigoRastreamento = dto.CodigoRastreamento,
                Mensagem = $"Erro: {ex.Message}"
            };
        }
    }

    public async Task<SuspenderEntregaResponseDto> ReativarEntregaAsync(string codigoRastreamento)
    {
        try
        {
            var request = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/srointeratividade/v1/solicitacoes/reativar");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    codigoObjeto = codigoRastreamento,
                    numeroCartaoPostagem = _configuration["Correios:CartaoPostagem"]
                }),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("[Correios] Reativando entrega {Codigo}...", codigoRastreamento);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return new SuspenderEntregaResponseDto
                {
                    Sucesso = true,
                    CodigoRastreamento = codigoRastreamento,
                    Mensagem = "Entrega reativada com sucesso"
                };
            }

            return new SuspenderEntregaResponseDto
            {
                Sucesso = false,
                CodigoRastreamento = codigoRastreamento,
                Mensagem = $"Erro ao reativar: {content}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao reativar entrega {Codigo}", codigoRastreamento);
            return new SuspenderEntregaResponseDto
            {
                Sucesso = false,
                CodigoRastreamento = codigoRastreamento,
                Mensagem = $"Erro: {ex.Message}"
            };
        }
    }

    #endregion

    #region Serviços

    public List<ServicoCorreiosDto> GetServicosDisponiveis()
    {
        return ServicosCorreios.Lista;
    }

    #endregion

    #region Atualização de Status

    public async Task AtualizarStatusPrePostagensAsync()
    {
        var prePostagens = await _context.Set<PrePostagem>()
            .Where(p => !string.IsNullOrEmpty(p.CodigoRastreamento) &&
                       (p.Status == StatusPrePostagem.Gerada || 
                        p.Status == StatusPrePostagem.Postada || 
                        p.Status == StatusPrePostagem.EmTransito))
            .ToListAsync();

        foreach (var prePostagem in prePostagens)
        {
            try
            {
                var rastreamento = await RastrearObjetoAsync(prePostagem.CodigoRastreamento!);
                if (rastreamento?.Eventos.Count > 0)
                {
                    var ultimoEvento = rastreamento.Eventos.First();
                    
                    // Atualizar status baseado no tipo do evento
                    if (ultimoEvento.Tipo == "BDE" || ultimoEvento.Descricao?.Contains("Entregue") == true)
                    {
                        prePostagem.Status = StatusPrePostagem.Entregue;
                        prePostagem.DataEntrega = ultimoEvento.DataHora;
                    }
                    else if (ultimoEvento.Tipo == "BDI" || ultimoEvento.Descricao?.Contains("Devolvido") == true)
                    {
                        prePostagem.Status = StatusPrePostagem.Devolvido;
                    }
                    else if (ultimoEvento.Descricao?.Contains("postado") == true)
                    {
                        prePostagem.Status = StatusPrePostagem.Postada;
                        prePostagem.DataPostagem = ultimoEvento.DataHora;
                    }
                    else if (prePostagem.Status == StatusPrePostagem.Gerada || prePostagem.Status == StatusPrePostagem.Postada)
                    {
                        prePostagem.Status = StatusPrePostagem.EmTransito;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Correios] Erro ao atualizar status da pré-postagem {Id}", prePostagem.Id);
            }
        }

        await _context.SaveChangesAsync();
    }

    #endregion

    #region Cálculo de Frete

    public async Task<List<CalcularFreteResponseDto>> CalcularFreteAsync(CalcularFreteRequestDto dto)
    {
        try
        {
            var cepOrigem = _configuration["Correios:RemetenteCEP"]?.Replace("-", "");
            var cepDestino = dto.CepDestino.Replace("-", "");
            
            var servicosCalcular = dto.CodigosServico ?? ServicosCorreios.Lista.Select(s => s.Codigo).ToList();
            var resultados = new List<CalcularFreteResponseDto>();

            // Usar carrinhoId se disponível, senão gerar ID aleatório
            var idLote = dto.CarrinhoId.HasValue 
                ? $"CAR{dto.CarrinhoId.Value}" 
                : Guid.NewGuid().ToString("N").Substring(0, 10);

            foreach (var codigoServico in servicosCalcular)
            {
                try
                {
                    decimal preco = 0m;
                    int prazo = 0;
                    DateTime? dataPrevisao = null;
                    string? mensagemErro = null;

                    // ============ CHAMAR API DE PREÇO ============
                    var requestPreco = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/preco/v1/nacional");
                    
                    var payloadPreco = new
                    {
                        idLote,
                        parametrosProduto = new[]
                        {
                            new
                            {
                                coProduto = codigoServico,
                                nuRequisicao = $"{idLote}_{codigoServico}_P",
                                cepOrigem,
                                cepDestino,
                                psObjeto = (int)(dto.Peso * 1000),
                                tpObjeto = 2,
                                comprimento = dto.Comprimento,
                                largura = dto.Largura,
                                altura = dto.Altura
                            }
                        }
                    };

                    requestPreco.Content = new StringContent(
                        JsonSerializer.Serialize(payloadPreco, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                        Encoding.UTF8,
                        "application/json"
                    );

                    var responsePreco = await _httpClient.SendAsync(requestPreco);
                    var contentPreco = await responsePreco.Content.ReadAsStringAsync();

                    _logger.LogInformation("[Correios] Resposta PREÇO {Servico}: {StatusCode} - {Content}", 
                        codigoServico, responsePreco.StatusCode, contentPreco);

                    if (responsePreco.IsSuccessStatusCode)
                    {
                        var responseData = JsonSerializer.Deserialize<JsonElement>(contentPreco);
                        
                        if (responseData.ValueKind == JsonValueKind.Array && responseData.GetArrayLength() > 0)
                        {
                            var primeiro = responseData[0];
                            
                            if (primeiro.TryGetProperty("pcFinal", out var precoEl))
                            {
                                var precoStr = precoEl.ValueKind == JsonValueKind.String 
                                    ? precoEl.GetString() 
                                    : precoEl.ToString();
                                decimal.TryParse(precoStr?.Replace(",", "."), 
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out preco);
                            }
                        }
                    }
                    else
                    {
                        mensagemErro = $"Erro preço: {contentPreco}";
                    }

                    // ============ CHAMAR API DE PRAZO ============
                    var requestPrazo = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/prazo/v1/nacional");
                    
                    var dataPostagem = DateTime.Now.ToString("yyyy-MM-dd");
                    var payloadPrazo = new
                    {
                        idLote,
                        parametrosPrazo = new[]
                        {
                            new
                            {
                                coProduto = codigoServico,
                                nuRequisicao = $"{idLote}_{codigoServico}_D",
                                cepOrigem,
                                cepDestino,
                                dataPostagem
                            }
                        }
                    };

                    requestPrazo.Content = new StringContent(
                        JsonSerializer.Serialize(payloadPrazo, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                        Encoding.UTF8,
                        "application/json"
                    );

                    var responsePrazo = await _httpClient.SendAsync(requestPrazo);
                    var contentPrazo = await responsePrazo.Content.ReadAsStringAsync();

                    _logger.LogInformation("[Correios] Resposta PRAZO {Servico}: {StatusCode} - {Content}", 
                        codigoServico, responsePrazo.StatusCode, contentPrazo);

                    if (responsePrazo.IsSuccessStatusCode)
                    {
                        var responseData = JsonSerializer.Deserialize<JsonElement>(contentPrazo);
                        
                        if (responseData.ValueKind == JsonValueKind.Array && responseData.GetArrayLength() > 0)
                        {
                            var primeiro = responseData[0];
                            
                            // Tentar pegar prazoEntrega
                            if (primeiro.TryGetProperty("prazoEntrega", out var prazoEl))
                            {
                                prazo = prazoEl.ValueKind == JsonValueKind.Number 
                                    ? prazoEl.GetInt32() 
                                    : int.TryParse(prazoEl.GetString(), out var p) ? p : 0;
                            }
                            
                            // Tentar pegar dataMaxima (data prevista de entrega)
                            if (primeiro.TryGetProperty("dataMaxima", out var dataEl))
                            {
                                var dataStr = dataEl.GetString();
                                if (DateTime.TryParse(dataStr, out var dt))
                                {
                                    dataPrevisao = dt;
                                }
                            }
                        }
                    }

                    // Se não conseguiu data prevista, calcular manualmente
                    if (!dataPrevisao.HasValue && prazo > 0)
                    {
                        dataPrevisao = DateTime.Now.AddDays(prazo);
                        // Ajustar para não contar fins de semana
                        while (dataPrevisao.Value.DayOfWeek == DayOfWeek.Saturday || dataPrevisao.Value.DayOfWeek == DayOfWeek.Sunday)
                        {
                            dataPrevisao = dataPrevisao.Value.AddDays(1);
                        }
                    }

                    resultados.Add(new CalcularFreteResponseDto
                    {
                        CodigoServico = codigoServico,
                        NomeServico = ServicosCorreios.GetNome(codigoServico),
                        Preco = preco,
                        PrazoEntrega = prazo,
                        DataPrevistaEntrega = dataPrevisao,
                        Mensagem = mensagemErro,
                        Erro = preco == 0 || !string.IsNullOrEmpty(mensagemErro)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Correios] Erro ao calcular frete para serviço {Servico}", codigoServico);
                    resultados.Add(new CalcularFreteResponseDto
                    {
                        CodigoServico = codigoServico,
                        NomeServico = ServicosCorreios.GetNome(codigoServico),
                        Mensagem = ex.Message,
                        Erro = true
                    });
                }
            }

            return resultados;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao calcular frete");
            return new List<CalcularFreteResponseDto>
            {
                new()
                {
                    Mensagem = ex.Message,
                    Erro = true
                }
            };
        }
    }

    #endregion

    #region Listagem Pré-Postagens API Correios (v2)

    public async Task<PrePostagemCorreiosPaginadoDto> ListarPrePostagensCorreiosAsync(PrePostagemCorreiosFiltroDto filtro)
    {
        try
        {
            var queryParams = new List<string>();
            
            if (!string.IsNullOrEmpty(filtro.Id)) queryParams.Add($"id={filtro.Id}");
            if (!string.IsNullOrEmpty(filtro.CodigoObjeto)) queryParams.Add($"codigoObjeto={filtro.CodigoObjeto}");
            if (!string.IsNullOrEmpty(filtro.ETicket)) queryParams.Add($"eTicket={filtro.ETicket}");
            if (!string.IsNullOrEmpty(filtro.CodigoEstampa2D)) queryParams.Add($"codigoEstampa2D={filtro.CodigoEstampa2D}");
            if (!string.IsNullOrEmpty(filtro.IdCorreios)) queryParams.Add($"idCorreios={filtro.IdCorreios}");
            if (!string.IsNullOrEmpty(filtro.Status)) queryParams.Add($"status={filtro.Status}");
            if (!string.IsNullOrEmpty(filtro.LogisticaReversa)) queryParams.Add($"logisticaReversa={filtro.LogisticaReversa}");
            if (!string.IsNullOrEmpty(filtro.TipoObjeto)) queryParams.Add($"tipoObjeto={filtro.TipoObjeto}");
            if (!string.IsNullOrEmpty(filtro.ModalidadePagamento)) queryParams.Add($"modalidadePagamento={filtro.ModalidadePagamento}");
            if (!string.IsNullOrEmpty(filtro.ObjetoCargo)) queryParams.Add($"objetoCargo={filtro.ObjetoCargo}");
            if (filtro.DataInicialCriacaoPrePostagem.HasValue) 
                queryParams.Add($"dataInicialCriacaoPrePostagem={filtro.DataInicialCriacaoPrePostagem.Value:yyyy-MM-dd}");
            if (filtro.DataFinalCriacaoPrePostagem.HasValue) 
                queryParams.Add($"dataFinalCriacaoPrePostagem={filtro.DataFinalCriacaoPrePostagem.Value:yyyy-MM-dd}");
            
            queryParams.Add($"page={filtro.Page}");
            queryParams.Add($"size={filtro.Size}");

            var queryString = string.Join("&", queryParams);
            var endpoint = $"/prepostagem/v2/prepostagens?{queryString}";

            var request = await CriarRequestAutenticadoAsync(HttpMethod.Get, endpoint);

            _logger.LogInformation("[Correios] Listando pré-postagens: {Endpoint}", endpoint);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta listagem: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[Correios] Erro ao listar pré-postagens: {Content}", content);
                return new PrePostagemCorreiosPaginadoDto();
            }

            var data = JsonSerializer.Deserialize<JsonElement>(content);
            var resultado = new PrePostagemCorreiosPaginadoDto
            {
                Page = filtro.Page,
                Size = filtro.Size
            };

            // Processar resposta
            if (data.TryGetProperty("itens", out var itensEl) || data.TryGetProperty("content", out itensEl))
            {
                foreach (var item in itensEl.EnumerateArray())
                {
                    var prePostagem = new PrePostagemCorreiosItemDto();
                    
                    if (item.TryGetProperty("id", out var idEl)) prePostagem.Id = idEl.GetString();
                    if (item.TryGetProperty("idCorreios", out var idCorreiosEl)) prePostagem.IdCorreios = idCorreiosEl.GetString();
                    if (item.TryGetProperty("codigoObjeto", out var codObjEl)) prePostagem.CodigoObjeto = codObjEl.GetString();
                    if (item.TryGetProperty("codigoServico", out var codServEl)) prePostagem.CodigoServico = codServEl.GetString();
                    if (item.TryGetProperty("status", out var statusEl)) prePostagem.Status = statusEl.GetString();
                    if (item.TryGetProperty("dataCriacao", out var dataCriacaoEl) && DateTime.TryParse(dataCriacaoEl.GetString(), out var dtCriacao))
                        prePostagem.DataCriacao = dtCriacao;
                    if (item.TryGetProperty("dataPostagem", out var dataPostEl) && DateTime.TryParse(dataPostEl.GetString(), out var dtPost))
                        prePostagem.DataPostagem = dtPost;
                    if (item.TryGetProperty("pesoInformado", out var pesoEl))
                        prePostagem.Peso = pesoEl.ValueKind == JsonValueKind.Number ? pesoEl.GetDecimal() : 0;
                    if (item.TryGetProperty("precoServico", out var precoEl))
                        prePostagem.PrecoServico = precoEl.ValueKind == JsonValueKind.Number ? precoEl.GetDecimal() : 0;

                    // Destinatário
                    if (item.TryGetProperty("destinatario", out var destEl))
                    {
                        prePostagem.Destinatario = ParseRemetenteDestinatario(destEl);
                    }

                    // Remetente
                    if (item.TryGetProperty("remetente", out var remEl))
                    {
                        prePostagem.Remetente = ParseRemetenteDestinatario(remEl);
                    }

                    resultado.Itens.Add(prePostagem);
                }
            }

            // Dados de paginação
            if (data.TryGetProperty("totalElements", out var totalEl))
                resultado.TotalElements = totalEl.GetInt32();
            if (data.TryGetProperty("totalPages", out var pagesEl))
                resultado.TotalPages = pagesEl.GetInt32();
            if (data.TryGetProperty("first", out var firstEl))
                resultado.First = firstEl.GetBoolean();
            if (data.TryGetProperty("last", out var lastEl))
                resultado.Last = lastEl.GetBoolean();

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao listar pré-postagens");
            return new PrePostagemCorreiosPaginadoDto();
        }
    }

    private RemetenteDestinatarioDto ParseRemetenteDestinatario(JsonElement el)
    {
        var dto = new RemetenteDestinatarioDto();
        
        if (el.TryGetProperty("nome", out var nomeEl)) dto.Nome = nomeEl.GetString();
        if (el.TryGetProperty("cpfCnpj", out var cpfEl)) dto.CpfCnpj = cpfEl.GetString();
        if (el.TryGetProperty("email", out var emailEl)) dto.Email = emailEl.GetString();
        if (el.TryGetProperty("telefone", out var telEl)) dto.Telefone = telEl.GetString();
        if (el.TryGetProperty("celular", out var celEl)) dto.Telefone = celEl.GetString();
        
        if (el.TryGetProperty("endereco", out var endEl))
        {
            dto.Endereco = new EnderecoCorreiosDto();
            if (endEl.TryGetProperty("cep", out var cepEl)) dto.Endereco.Cep = cepEl.GetString();
            if (endEl.TryGetProperty("logradouro", out var logEl)) dto.Endereco.Logradouro = logEl.GetString();
            if (endEl.TryGetProperty("numero", out var numEl)) dto.Endereco.Numero = numEl.GetString();
            if (endEl.TryGetProperty("complemento", out var compEl)) dto.Endereco.Complemento = compEl.GetString();
            if (endEl.TryGetProperty("bairro", out var bairroEl)) dto.Endereco.Bairro = bairroEl.GetString();
            if (endEl.TryGetProperty("cidade", out var cidEl)) dto.Endereco.Cidade = cidEl.GetString();
            if (endEl.TryGetProperty("uf", out var ufEl)) dto.Endereco.Uf = ufEl.GetString();
        }

        return dto;
    }

    public async Task<PrePostagemPostadaDetalhesDto?> ConsultarPrePostagemPostadaAsync(string codigoObjeto)
    {
        try
        {
            var request = await CriarRequestAutenticadoAsync(HttpMethod.Get, $"/prepostagem/v1/prepostagens/postada/{codigoObjeto}");

            _logger.LogInformation("[Correios] Consultando pré-postagem postada: {Codigo}", codigoObjeto);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta consulta postada: {StatusCode} - {Content}", response.StatusCode, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Correios] ⚠️ Pré-postagem não encontrada ou não está postada ainda: {Codigo}", codigoObjeto);
                
                // Tentar buscar por ID ao invés de código
                try
                {
                    var requestById = await CriarRequestAutenticadoAsync(HttpMethod.Get, $"/prepostagem/v2/prepostagens?codigoObjeto={codigoObjeto}&size=1");
                    var responseById = await _httpClient.SendAsync(requestById);
                    var contentById = await responseById.Content.ReadAsStringAsync();
                    
                    _logger.LogInformation("[Correios] Tentativa v2: {StatusCode} - {Content}", responseById.StatusCode, contentById);
                    
                    if (responseById.IsSuccessStatusCode)
                    {
                        var dataV2 = JsonSerializer.Deserialize<JsonElement>(contentById);
                        if (dataV2.TryGetProperty("itens", out var itensEl) && itensEl.GetArrayLength() > 0)
                        {
                            var item = itensEl[0];
                            return ParsePrePostagemPostadaV2(item, codigoObjeto, contentById);
                        }
                    }
                }
                catch (Exception exV2)
                {
                    _logger.LogError(exV2, "[Correios] Erro ao tentar buscar por v2");
                }
                
                return null;
            }

            var data = JsonSerializer.Deserialize<JsonElement>(content);
            return ParsePrePostagemPostada(data, codigoObjeto, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao consultar pré-postagem postada: {Codigo}", codigoObjeto);
            return null;
        }
    }

    private PrePostagemPostadaDetalhesDto ParsePrePostagemPostada(JsonElement data, string codigoObjeto, string jsonOriginal)
    {
        var resultado = new PrePostagemPostadaDetalhesDto
        {
            CodigoObjeto = codigoObjeto,
            RespostaJson = jsonOriginal
        };

            if (data.TryGetProperty("id", out var idEl)) resultado.IdPrePostagem = idEl.GetString();
            if (data.TryGetProperty("codigoServico", out var servEl)) 
            {
                resultado.CodigoServico = servEl.GetString();
                resultado.NomeServico = ServicosCorreios.GetNome(resultado.CodigoServico ?? "");
            }
            if (data.TryGetProperty("status", out var statusEl)) resultado.Status = statusEl.GetString();
            if (data.TryGetProperty("dataCriacao", out var dataCrEl) && DateTime.TryParse(dataCrEl.GetString(), out var dtCr))
                resultado.DataCriacao = dtCr;
            if (data.TryGetProperty("dataPostagem", out var dataPoEl) && DateTime.TryParse(dataPoEl.GetString(), out var dtPo))
                resultado.DataPostagem = dtPo;
            if (data.TryGetProperty("pesoInformado", out var pesoEl))
            {
                var pesoGramas = decimal.TryParse(pesoEl.GetString() ?? pesoEl.ToString(), out var p) ? p : 0;
                resultado.Peso = pesoGramas / 1000; // API retorna em gramas, converter para kg
            }
            if (data.TryGetProperty("alturaInformada", out var altEl))
                resultado.Altura = decimal.TryParse(altEl.GetString() ?? altEl.ToString(), out var a) ? a : 0;
            if (data.TryGetProperty("larguraInformada", out var largEl))
                resultado.Largura = decimal.TryParse(largEl.GetString() ?? largEl.ToString(), out var l) ? l : 0;
            if (data.TryGetProperty("comprimentoInformado", out var compEl))
                resultado.Comprimento = decimal.TryParse(compEl.GetString() ?? compEl.ToString(), out var c) ? c : 0;
            if (data.TryGetProperty("precoServico", out var precoEl))
                resultado.PrecoServico = decimal.TryParse(precoEl.GetString() ?? precoEl.ToString(), out var pr) ? pr : 0;
            
            // Campos adicionais
            if (data.TryGetProperty("numeroNotaFiscal", out var nfEl)) 
                resultado.NumeroNotaFiscal = nfEl.GetString();
            if (data.TryGetProperty("numeroCartaoPostagem", out var cartaoEl)) 
                resultado.NumeroCartaoPostagem = cartaoEl.GetString();
            if (data.TryGetProperty("idAtendimento", out var atendEl)) 
                resultado.IdAtendimento = atendEl.GetString();
            if (data.TryGetProperty("eticket", out var eticketEl)) 
                resultado.Eticket = eticketEl.GetString();
            if (data.TryGetProperty("dataEticket", out var dtEticketEl) && DateTime.TryParse(dtEticketEl.GetString(), out var dtEtick))
                resultado.DataEticket = dtEtick;
            if (data.TryGetProperty("prazoPostagem", out var prazoPostEl) && DateTime.TryParse(prazoPostEl.GetString(), out var dtPrazo))
                resultado.PrazoPostagem = dtPrazo;
            if (data.TryGetProperty("modalidadePagamento", out var modPagEl))
                resultado.ModalidadePagamento = modPagEl.ValueKind == JsonValueKind.Number ? modPagEl.GetInt32() : (int?)null;
            if (data.TryGetProperty("observacao", out var obsEl)) 
                resultado.Observacao = obsEl.GetString();

            if (data.TryGetProperty("remetente", out var remEl))
                resultado.Remetente = ParseRemetenteDestinatario(remEl);
            if (data.TryGetProperty("destinatario", out var destEl))
                resultado.Destinatario = ParseRemetenteDestinatario(destEl);

            // Itens da declaração de conteúdo
            if (data.TryGetProperty("itensDeclaracaoConteudo", out var itensEl))
            {
                resultado.ItensDeclaracao = new List<ItemDeclaracaoConteudoDto>();
                foreach (var item in itensEl.EnumerateArray())
                {
                    var itemDto = new ItemDeclaracaoConteudoDto();
                    if (item.TryGetProperty("conteudo", out var contEl)) itemDto.Conteudo = contEl.GetString();
                    if (item.TryGetProperty("quantidade", out var qtdEl)) itemDto.Quantidade = qtdEl.GetString();
                    if (item.TryGetProperty("valor", out var valEl)) itemDto.Valor = valEl.GetString();
                    resultado.ItensDeclaracao.Add(itemDto);
                }
            }

            // Serviços adicionais
            if (data.TryGetProperty("listaServicoAdicional", out var servicosEl))
            {
                resultado.ServicosAdicionais = new List<ServicoAdicionalDto>();
                foreach (var serv in servicosEl.EnumerateArray())
                {
                    var servDto = new ServicoAdicionalDto();
                    if (serv.TryGetProperty("codigoServicoAdicional", out var codEl)) servDto.Codigo = codEl.GetString();
                    if (serv.TryGetProperty("nomeServicoAdicional", out var nomeEl)) servDto.Nome = nomeEl.GetString();
                    if (serv.TryGetProperty("valorServicoAdicional", out var valEl))
                        servDto.Valor = decimal.TryParse(valEl.GetString() ?? valEl.ToString(), out var v) ? v : 0;
                    if (serv.TryGetProperty("valorDeclarado", out var valDeclEl))
                    {
                        var valorDeclCentavos = decimal.TryParse(valDeclEl.GetString() ?? valDeclEl.ToString(), out var vd) ? vd : 0;
                        var valorDecl = valorDeclCentavos / 100; // API retorna em centavos
                        servDto.ValorDeclarado = valorDecl;
                        if (!resultado.ValorDeclarado.HasValue || valorDecl > resultado.ValorDeclarado.Value)
                            resultado.ValorDeclarado = valorDecl;
                    }
                    resultado.ServicosAdicionais.Add(servDto);
                }
            }

            return resultado;
    }

    private PrePostagemPostadaDetalhesDto ParsePrePostagemPostadaV2(JsonElement data, string codigoObjeto, string jsonOriginal)
    {
        var resultado = new PrePostagemPostadaDetalhesDto
        {
            CodigoObjeto = codigoObjeto,
            RespostaJson = jsonOriginal
        };

        if (data.TryGetProperty("id", out var idEl)) resultado.IdPrePostagem = idEl.GetString();
        if (data.TryGetProperty("codigoServico", out var servEl)) 
        {
            resultado.CodigoServico = servEl.GetString();
            // No v2 tem o campo 'servico' com o nome completo
            if (data.TryGetProperty("servico", out var nomeServEl))
            {
                resultado.NomeServico = nomeServEl.GetString();
            }
            else
            {
                resultado.NomeServico = ServicosCorreios.GetNome(resultado.CodigoServico ?? "");
            }
        }
        
        // v2 usa statusAtual (número) e descStatusAtual (texto)
        if (data.TryGetProperty("descStatusAtual", out var descStatusEl))
        {
            resultado.Status = descStatusEl.GetString();
        }
        else if (data.TryGetProperty("statusAtual", out var statusNumEl))
        {
            resultado.Status = statusNumEl.GetInt32().ToString();
        }
        
        // v2 usa dataHora ao invés de dataCriacao
        if (data.TryGetProperty("dataHora", out var dataHoraEl) && DateTime.TryParse(dataHoraEl.GetString(), out var dtHora))
        {
            resultado.DataCriacao = dtHora;
        }
        
        // dataPostagem não existe no v2, mas pode ter dataHoraStatusAtual
        if (data.TryGetProperty("dataHoraStatusAtual", out var dataStatusEl) && DateTime.TryParse(dataStatusEl.GetString(), out var dtStatus))
        {
            resultado.DataPostagem = dtStatus;
        }
        
        // Dimensões e peso
        if (data.TryGetProperty("pesoInformado", out var pesoEl))
        {
            var pesoGramas = decimal.TryParse(pesoEl.GetString() ?? pesoEl.ToString(), out var p) ? p : 0;
            resultado.Peso = pesoGramas / 1000; // API retorna em gramas, converter para kg
        }
        if (data.TryGetProperty("alturaInformada", out var altEl))
            resultado.Altura = decimal.TryParse(altEl.GetString() ?? altEl.ToString(), out var a) ? a : 0;
        if (data.TryGetProperty("larguraInformada", out var largEl))
            resultado.Largura = decimal.TryParse(largEl.GetString() ?? largEl.ToString(), out var l) ? l : 0;
        if (data.TryGetProperty("comprimentoInformado", out var compEl))
            resultado.Comprimento = decimal.TryParse(compEl.GetString() ?? compEl.ToString(), out var c) ? c : 0;
        
        // Preços - campos do root (v2)
        if (data.TryGetProperty("precoServico", out var precoServEl))
            resultado.PrecoServico = precoServEl.ValueKind == JsonValueKind.Number ? precoServEl.GetDecimal() : 0;
        if (data.TryGetProperty("precoPrePostagem", out var precoPreEl))
            resultado.PrecoPrePostagem = precoPreEl.ValueKind == JsonValueKind.Number ? precoPreEl.GetDecimal() : 0;
        
        // Campos adicionais úteis
        if (data.TryGetProperty("numeroNotaFiscal", out var nfEl)) 
            resultado.NumeroNotaFiscal = nfEl.GetString();
        if (data.TryGetProperty("numeroCartaoPostagem", out var cartaoEl)) 
            resultado.NumeroCartaoPostagem = cartaoEl.GetString();
        if (data.TryGetProperty("idAtendimento", out var atendEl)) 
            resultado.IdAtendimento = atendEl.GetString();
        if (data.TryGetProperty("eticket", out var eticketEl)) 
            resultado.Eticket = eticketEl.GetString();
        if (data.TryGetProperty("dataEticket", out var dtEticketEl) && DateTime.TryParse(dtEticketEl.GetString(), out var dtEtick))
            resultado.DataEticket = dtEtick;
        if (data.TryGetProperty("prazoPostagem", out var prazoPostEl) && DateTime.TryParse(prazoPostEl.GetString(), out var dtPrazo))
            resultado.PrazoPostagem = dtPrazo;
        if (data.TryGetProperty("modalidadePagamento", out var modPagEl))
            resultado.ModalidadePagamento = modPagEl.ValueKind == JsonValueKind.Number ? modPagEl.GetInt32() : (int?)null;
        if (data.TryGetProperty("observacao", out var obsEl)) 
            resultado.Observacao = obsEl.GetString();

        if (data.TryGetProperty("remetente", out var remEl))
            resultado.Remetente = ParseRemetenteDestinatario(remEl);
        if (data.TryGetProperty("destinatario", out var destEl))
            resultado.Destinatario = ParseRemetenteDestinatario(destEl);

        // Itens da declaração de conteúdo
        if (data.TryGetProperty("itensDeclaracaoConteudo", out var itensEl))
        {
            resultado.ItensDeclaracao = new List<ItemDeclaracaoConteudoDto>();
            foreach (var item in itensEl.EnumerateArray())
            {
                var itemDto = new ItemDeclaracaoConteudoDto();
                if (item.TryGetProperty("conteudo", out var contEl)) itemDto.Conteudo = contEl.GetString();
                if (item.TryGetProperty("quantidade", out var qtdEl)) itemDto.Quantidade = qtdEl.GetString();
                if (item.TryGetProperty("valor", out var valEl)) itemDto.Valor = valEl.GetString();
                resultado.ItensDeclaracao.Add(itemDto);
            }
        }

        // Serviços adicionais no v2
        if (data.TryGetProperty("listaServicoAdicional", out var servicosEl))
        {
            resultado.ServicosAdicionais = new List<ServicoAdicionalDto>();
            foreach (var serv in servicosEl.EnumerateArray())
            {
                var servDto = new ServicoAdicionalDto();
                if (serv.TryGetProperty("codigoServicoAdicional", out var codEl)) 
                    servDto.Codigo = codEl.GetString();
                
                // No v2 pode ter 'tipoServicoAdicional' ao invés de 'nomeServicoAdicional'
                if (serv.TryGetProperty("tipoServicoAdicional", out var tipoEl))
                {
                    servDto.Nome = tipoEl.GetString();
                }
                else if (serv.TryGetProperty("nomeServicoAdicional", out var nomeEl))
                {
                    servDto.Nome = nomeEl.GetString();
                }
                
                // valorServicoAdicional é o preço do serviço adicional
                if (serv.TryGetProperty("valorServicoAdicional", out var valServEl))
                {
                    servDto.Valor = decimal.TryParse(valServEl.GetString() ?? valServEl.ToString(), out var vs) ? vs : 0;
                }
                
                // valorDeclarado é o valor declarado da mercadoria
                if (serv.TryGetProperty("valorDeclarado", out var valDeclEl))
                {
                    var valorDeclCentavos = decimal.TryParse(valDeclEl.GetString() ?? valDeclEl.ToString(), out var vd) ? vd : 0;
                    var valorDecl = valorDeclCentavos / 100; // API retorna em centavos
                    servDto.ValorDeclarado = valorDecl;
                    // Pegar o maior valor declarado para o resultado principal
                    if (!resultado.ValorDeclarado.HasValue || valorDecl > resultado.ValorDeclarado.Value)
                        resultado.ValorDeclarado = valorDecl;
                }
                
                resultado.ServicosAdicionais.Add(servDto);
            }
        }

        return resultado;
    }

    public async Task<bool> CancelarPrePostagemCorreiosAsync(string idPrePostagem)
    {
        try
        {
            var request = await CriarRequestAutenticadoAsync(HttpMethod.Delete, $"/prepostagem/v1/prepostagens/{idPrePostagem}");

            _logger.LogInformation("[Correios] Cancelando pré-postagem nos Correios: {Id}", idPrePostagem);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta cancelamento: {StatusCode} - {Content}", response.StatusCode, content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao cancelar pré-postagem nos Correios: {Id}", idPrePostagem);
            return false;
        }
    }

    #endregion

    #region Geração de Rótulos

    public async Task<GerarRotuloResponseDto> GerarRotuloRangeAsync(GerarRotuloRangeRequestDto dto)
    {
        try
        {
            var request = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/prepostagem/v1/prepostagens/rotulo/range");
            
            var payload = new
            {
                codigoServico = dto.CodigoServico,
                quantidade = dto.Quantidade
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("[Correios] Gerando rótulos por range: {Servico} x {Qtd}", dto.CodigoServico, dto.Quantidade);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta geração rótulo range: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                // Verificar se a resposta é um PDF
                if (response.Content.Headers.ContentType?.MediaType == "application/pdf")
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                    return new GerarRotuloResponseDto
                    {
                        Sucesso = true,
                        PdfBytes = pdfBytes,
                        Mensagem = "Rótulos gerados com sucesso"
                    };
                }

                var data = JsonSerializer.Deserialize<JsonElement>(content);
                return new GerarRotuloResponseDto
                {
                    Sucesso = true,
                    IdRecibo = data.TryGetProperty("idRecibo", out var idEl) ? idEl.GetString() : null,
                    UrlRotulo = data.TryGetProperty("urlRotulo", out var urlEl) ? urlEl.GetString() : null,
                    Mensagem = "Rótulos gerados com sucesso"
                };
            }

            return new GerarRotuloResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro ao gerar rótulos: {content}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao gerar rótulos por range");
            return new GerarRotuloResponseDto
            {
                Sucesso = false,
                Mensagem = ex.Message
            };
        }
    }

    public async Task<GerarRotuloResponseDto> GerarRotuloLoteAsyncAsync(GerarRotuloLoteAsyncRequestDto dto)
    {
        try
        {
            var request = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/prepostagem/v1/prepostagens/rotulo/lote/assincrono/pdf");
            
            var payload = new
            {
                idCorreios = dto.IdCorreios ?? Guid.NewGuid().ToString(),
                numeroCartaoPostagem = dto.NumeroCartaoPostagem ?? _configuration["Correios:CartaoPostagem"],
                tipoRotulo = dto.TipoRotulo,
                formatoRotulo = dto.FormatoRotulo,
                idAtendimento = dto.IdAtendimento,
                imprimeRemetente = dto.ImprimeRemetente,
                idsLotePrePostagem = dto.IdsLotePrePostagem.Select(i => new
                {
                    idPrePostagem = i.IdPrePostagem,
                    codigoObjeto = i.CodigoObjeto,
                    sequencial = i.Sequencial
                }).ToList(),
                layoutImpressao = dto.LayoutImpressao
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("[Correios] Gerando rótulos em lote assíncrono (simples): {Count} itens", dto.IdsLotePrePostagem.Count);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Resposta geração rótulo lote: {StatusCode} - {Content}", response.StatusCode, content);

            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                return new GerarRotuloResponseDto
                {
                    Sucesso = true,
                    IdRecibo = data.TryGetProperty("idRecibo", out var idEl) ? idEl.GetString() : null,
                    UrlRotulo = data.TryGetProperty("urlRotulo", out var urlEl) ? urlEl.GetString() : null,
                    Mensagem = "Solicitação de rótulos em lote enviada. Aguarde processamento."
                };
            }

            return new GerarRotuloResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro ao gerar rótulos em lote: {content}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao gerar rótulos em lote assíncrono");
            return new GerarRotuloResponseDto
            {
                Sucesso = false,
                Mensagem = ex.Message
            };
        }
    }

    public async Task<GerarRotuloResponseDto> GerarRotuloRegistradoAsyncAsync(GerarRotuloRegistradoAsyncRequestDto dto)
    {
        try
        {
            // Validações básicas
            if (dto.CodigosObjeto == null || dto.CodigosObjeto.Count == 0)
            {
                return new GerarRotuloResponseDto
                {
                    Sucesso = false,
                    Mensagem = "É necessário informar pelo menos um código de objeto"
                };
            }

            if (dto.IdsPrePostagem == null || dto.IdsPrePostagem.Count == 0)
            {
                _logger.LogWarning("[Correios] IdsPrePostagem não informado. Isso pode causar falha silenciosa na API.");
            }

            if (dto.IdsPrePostagem != null && dto.CodigosObjeto.Count != dto.IdsPrePostagem.Count)
            {
                _logger.LogWarning("[Correios] Quantidade de CodigosObjeto ({CodigosCount}) difere de IdsPrePostagem ({IdsCount})", 
                    dto.CodigosObjeto.Count, dto.IdsPrePostagem.Count);
            }

            var request = await CriarRequestAutenticadoAsync(HttpMethod.Post, "/prepostagem/v1/prepostagens/rotulo/assincrono/pdf");
            
            var idCorreios = dto.IdCorreios ?? Guid.NewGuid().ToString();
            var numeroCartao = dto.NumeroCartaoPostagem ?? _configuration["Correios:CartaoPostagem"];
            
            // IMPORTANTE: A API aceita SOMENTE UM dos três: idsPrePostagem OU codigosObjeto OU idAtendimento
            // Prioridade: 1) idAtendimento, 2) idsPrePostagem, 3) codigosObjeto
            object payload;
            
            if (!string.IsNullOrEmpty(dto.IdAtendimento))
            {
                payload = new
                {
                    idAtendimento = dto.IdAtendimento,
                    idCorreios = idCorreios,
                    numeroCartaoPostagem = numeroCartao,
                    tipoRotulo = dto.TipoRotulo ?? "P",
                    formatoRotulo = dto.FormatoRotulo ?? "ET",
                    imprimeRemetente = dto.ImprimeRemetente ?? "S",
                    layoutImpressao = dto.LayoutImpressao ?? "PADRAO"
                };
                _logger.LogInformation("[Correios] Usando idAtendimento: {IdAtendimento}", dto.IdAtendimento);
            }
            else if (dto.IdsPrePostagem != null && dto.IdsPrePostagem.Count > 0)
            {
                payload = new
                {
                    idsPrePostagem = dto.IdsPrePostagem,
                    idCorreios = idCorreios,
                    numeroCartaoPostagem = numeroCartao,
                    tipoRotulo = dto.TipoRotulo ?? "P",
                    formatoRotulo = dto.FormatoRotulo ?? "ET",
                    imprimeRemetente = dto.ImprimeRemetente ?? "S",
                    layoutImpressao = dto.LayoutImpressao ?? "PADRAO"
                };
                _logger.LogInformation("[Correios] Usando idsPrePostagem: {Ids}", string.Join(", ", dto.IdsPrePostagem));
            }
            else
            {
                payload = new
                {
                    codigosObjeto = dto.CodigosObjeto,
                    idCorreios = idCorreios,
                    numeroCartaoPostagem = numeroCartao,
                    tipoRotulo = dto.TipoRotulo ?? "P",
                    formatoRotulo = dto.FormatoRotulo ?? "ET",
                    imprimeRemetente = dto.ImprimeRemetente ?? "S",
                    layoutImpressao = dto.LayoutImpressao ?? "PADRAO"
                };
                _logger.LogInformation("[Correios] Usando codigosObjeto: {Codigos}", string.Join(", ", dto.CodigosObjeto));
            }

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true 
            });
            
            _logger.LogInformation("[Correios] ===== GERAÇÃO DE RÓTULO REGISTRADO =====");
            _logger.LogInformation("[Correios] Quantidade: {Count} itens", 
                dto.IdsPrePostagem?.Count ?? dto.CodigosObjeto?.Count ?? 1);
            _logger.LogInformation("[Correios] IdCorreios: {IdCorreios}", idCorreios);
            _logger.LogInformation("[Correios] Cartão Postagem: {Cartao}", numeroCartao);
            if (dto.IdPedido.HasValue)
            {
                _logger.LogInformation("[Correios] ID Pedido: {IdPedido}", dto.IdPedido.Value);
            }
            _logger.LogInformation("[Correios] Payload completo:\n{Payload}", jsonPayload);

            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[Correios] Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("[Correios] Resposta completa:\n{Content}", content);

            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                var idRecibo = data.TryGetProperty("idRecibo", out var idEl) ? idEl.GetString() : null;
                var urlRotulo = data.TryGetProperty("urlRotulo", out var urlEl) ? urlEl.GetString() : null;
                
                if (string.IsNullOrEmpty(idRecibo))
                {
                    _logger.LogError("[Correios] ⚠️ API retornou sucesso mas SEM idRecibo! Resposta: {Content}", content);
                    return new GerarRotuloResponseDto
                    {
                        Sucesso = false,
                        Mensagem = "API dos Correios não retornou idRecibo. Verifique os dados enviados."
                    };
                }
                
                _logger.LogInformation("[Correios] ✓ IdRecibo gerado: {IdRecibo}", idRecibo);
                if (!string.IsNullOrEmpty(urlRotulo))
                {
                    _logger.LogInformation("[Correios] ✓ URL do rótulo: {UrlRotulo}", urlRotulo);
                }
                
                // Retornar o contexto para que o frontend possa repassar na consulta
                return new GerarRotuloResponseDto
                {
                    Sucesso = true,
                    IdRecibo = idRecibo,
                    UrlRotulo = urlRotulo,
                    Mensagem = $"Rótulo em processamento. IdRecibo: {idRecibo}",
                    IdPedido = dto.IdPedido,
                    IdAtendimento = dto.IdAtendimento,
                    CodigosObjeto = dto.CodigosObjeto,
                    IdsPrePostagem = dto.IdsPrePostagem,
                    Observacao = dto.Observacao,
                    TipoRotulo = dto.TipoRotulo,
                    FormatoRotulo = dto.FormatoRotulo
                };
            }

            _logger.LogError("[Correios] ✗ Erro ao gerar rótulos - Status: {Status}, Resposta: {Content}", response.StatusCode, content);

            return new GerarRotuloResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro HTTP {response.StatusCode}: {content}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] ✗ Exceção ao gerar rótulos assíncrono");
            return new GerarRotuloResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro: {ex.Message}"
            };
        }
    }

    public async Task<GerarRotuloResponseDto> ConsultarRotuloAsync(string idRecibo)
    {
        return await ConsultarRotuloAsync(idRecibo, null);
    }

    public async Task<GerarRotuloResponseDto> ConsultarRotuloAsync(string idRecibo, GerarRotuloRegistradoAsyncRequestDto? contexto)
    {
        try
        {
            var request = await CriarRequestAutenticadoAsync(HttpMethod.Get, $"/prepostagem/v1/prepostagens/rotulo/download/assincrono/{idRecibo}");

            _logger.LogInformation("[Correios] Consultando/baixando rótulo assíncrono: {IdRecibo}", idRecibo);

            var response = await _httpClient.SendAsync(request);

            _logger.LogInformation("[Correios] Resposta consulta rótulo: {StatusCode} - ContentType: {ContentType}", 
                response.StatusCode, response.Content.Headers.ContentType?.MediaType);

            if (response.IsSuccessStatusCode)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType;
                
                // Se a resposta for um PDF, retornar os bytes
                if (contentType == "application/pdf")
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                    return new GerarRotuloResponseDto
                    {
                        Sucesso = true,
                        IdRecibo = idRecibo,
                        PdfBytes = pdfBytes,
                        Mensagem = "Rótulo gerado com sucesso"
                    };
                }

                // Senão, pode ser JSON com status, URL ou dados em base64
                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation("[Correios] 📄 Resposta JSON recebida (primeiros 500 chars): {Content}", 
                    content.Length > 500 ? content.Substring(0, 500) + "..." : content);
                
                var data = JsonSerializer.Deserialize<JsonElement>(content);

                if (data.TryGetProperty("dados", out var dadosEl) && !string.IsNullOrEmpty(dadosEl.GetString()))
                {
                    var base64Data = dadosEl.GetString();
                    if (!string.IsNullOrEmpty(base64Data))
                    {
                        var pdfBytes = Convert.FromBase64String(base64Data);
                        
                        var nome = data.TryGetProperty("nome", out var nomeEl) ? nomeEl.GetString() : $"rotulo_{idRecibo}.pdf";
                        
                        _logger.LogInformation("[Correios] ✓ PDF recebido em base64: {Nome} ({Size} bytes)", nome, pdfBytes.Length);
                        
                        // Salvar o PDF no servidor com contexto completo se disponível
                        if (contexto != null)
                        {
                            await SalvarRotuloAsync(
                                idRecibo, 
                                pdfBytes, 
                                nome,
                                contexto.IdPedido,
                                contexto.IdAtendimento,
                                contexto.CodigosObjeto,
                                contexto.IdsPrePostagem,
                                contexto.Observacao,
                                contexto.TipoRotulo,
                                contexto.FormatoRotulo
                            );
                        }
                        else
                        {
                            await SalvarRotuloAsync(idRecibo, pdfBytes, nome);
                        }
                        
                        return new GerarRotuloResponseDto
                        {
                            Sucesso = true,
                            IdRecibo = idRecibo,
                            PdfBytes = pdfBytes,
                            Mensagem = $"Rótulo gerado: {nome}"
                        };
                    }
                }

                if (data.TryGetProperty("urlRotulo", out var urlEl) && !string.IsNullOrEmpty(urlEl.GetString()))
                {
                    var urlRotulo = urlEl.GetString();
                    
                    // Baixar o PDF da URL
                    try
                    {
                        using var pdfRequest = new HttpRequestMessage(HttpMethod.Get, urlRotulo);
                        var pdfResponse = await _httpClient.SendAsync(pdfRequest);
                        
                        if (pdfResponse.IsSuccessStatusCode)
                        {
                            var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
                            return new GerarRotuloResponseDto
                            {
                                Sucesso = true,
                                IdRecibo = idRecibo,
                                UrlRotulo = urlRotulo,
                                PdfBytes = pdfBytes,
                                Mensagem = "Rótulo gerado com sucesso"
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Correios] Erro ao baixar PDF da URL: {Url}", urlRotulo);
                    }
                    
                    return new GerarRotuloResponseDto
                    {
                        Sucesso = true,
                        IdRecibo = idRecibo,
                        UrlRotulo = urlRotulo,
                        Mensagem = "Rótulo disponível para download"
                    };
                }

                // Verificar status de processamento
                if (data.TryGetProperty("status", out var statusEl))
                {
                    var status = statusEl.GetString();
                    return new GerarRotuloResponseDto
                    {
                        Sucesso = status?.ToUpper() == "PROCESSADO" || status?.ToUpper() == "CONCLUIDO",
                        IdRecibo = idRecibo,
                        Mensagem = $"Status: {status}. Aguarde processamento ou tente novamente."
                    };
                }

                return new GerarRotuloResponseDto
                {
                    Sucesso = true,
                    IdRecibo = idRecibo,
                    Mensagem = "Aguardando processamento. Tente novamente em alguns segundos."
                };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                return new GerarRotuloResponseDto
                {
                    Sucesso = false,
                    IdRecibo = idRecibo,
                    Mensagem = "Rótulo ainda em processamento. Tente novamente em alguns segundos."
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new GerarRotuloResponseDto
            {
                Sucesso = false,
                IdRecibo = idRecibo,
                Mensagem = $"Erro ao consultar rótulo: {response.StatusCode} - {errorContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao consultar rótulo assíncrono: {IdRecibo}", idRecibo);
            return new GerarRotuloResponseDto
            {
                Sucesso = false,
                IdRecibo = idRecibo,
                Mensagem = ex.Message
            };
        }
    }

    private async Task SalvarRotuloAsync(
        string idRecibo, 
        byte[] pdfBytes, 
        string? nomeArquivo = null,
        int? idPedido = null,
        string? idAtendimento = null,
        List<string>? codigosObjeto = null,
        List<string>? idsPrePostagem = null,
        string? observacao = null,
        string tipoRotulo = "P",
        string formatoRotulo = "ET")
    {
        try
        {
            // Criar diretório rotulos se não existir
            var rotulosPath = Path.Combine("wwwroot", "rotulos");
            if (!Directory.Exists(rotulosPath))
            {
                Directory.CreateDirectory(rotulosPath);
                _logger.LogInformation("[Correios] Diretório de rótulos criado: {Path}", rotulosPath);
            }

            // Nome do arquivo
            var nome = nomeArquivo ?? $"rotulo_{idRecibo}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            if (!nome.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                nome += ".pdf";
            }

            var caminhoCompleto = Path.Combine(rotulosPath, nome);

            // Salvar o arquivo
            await File.WriteAllBytesAsync(caminhoCompleto, pdfBytes);
            _logger.LogInformation("[Correios] 💾 Rótulo salvo: {Path} ({Size} bytes)", caminhoCompleto, pdfBytes.Length);

            // Salvar no banco de dados
            var rotulo = new Rotulo
            {
                IdPedido = idPedido,
                IdRecibo = idRecibo,
                IdAtendimento = idAtendimento,
                NomeArquivo = nome,
                CaminhoArquivo = $"/rotulos/{nome}",
                DataGeracao = DateTime.UtcNow,
                QuantidadeRotulos = codigosObjeto?.Count ?? idsPrePostagem?.Count ?? 1,
                CodigosObjeto = codigosObjeto != null && codigosObjeto.Any() 
                    ? JsonSerializer.Serialize(codigosObjeto) 
                    : null,
                IdsPrePostagem = idsPrePostagem != null && idsPrePostagem.Any() 
                    ? JsonSerializer.Serialize(idsPrePostagem) 
                    : null,
                TipoRotulo = tipoRotulo,
                FormatoRotulo = formatoRotulo,
                TamanhoBytes = pdfBytes.Length,
                Observacao = observacao
            };

            _context.Rotulos.Add(rotulo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[Correios] ✓ Registro de rótulo salvo no banco: ID {Id}", rotulo.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Correios] Erro ao salvar rótulo no servidor");
        }
    }

    #endregion
}
