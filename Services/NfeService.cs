using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using cafApi.Contexts;
using cafApi.Models;
using cafApi.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace cafApi.Services;

/// <summary>
/// Interface do serviço de NF-e
/// </summary>
public interface INfeService
{
    bool IsConfigured();
    Task<NfeResponseDto> EmitirNfeAsync(int pedidoId, string? naturezaOperacao = null);
    Task<NfeResponseDto> ConsultarNfeAsync(string chaveAcesso);
    Task<NfeResponseDto> CancelarNfeAsync(int notaFiscalId, string motivo);
    Task<NfeResponseDto> InutilizarNumeracaoAsync(int serie, int numeroInicial, int numeroFinal, string justificativa);
    Task<List<ConsultaNfeDto>> ListarPorPedidoAsync(int pedidoId);
    Task<string?> ObterXmlAsync(int notaFiscalId);
    Task<byte[]?> GerarDanfeAsync(int notaFiscalId);
    Task<byte[]?> GerarDanfePorChaveAcessoAsync(string chaveAcesso);
    Task<int> ObterProximoNumeroAsync(int serie);
    Task<PaginatedPedidoNfeDto> ListarPedidosComNfeAsync(int pageNumber, int pageSize, string? busca = null);
    Task<PaginatedPedidoNfeDto> ListarPedidosSemNfeAsync(int pageNumber, int pageSize, string? busca = null);
    Task<PaginatedPedidoNfeDto> ListarTodosPedidosNfeAsync(int pageNumber, int pageSize, string? busca = null);
    Task<NfeEstatisticasDto> ObterEstatisticasAsync();
    Task<NfeResponseDto> EnviarNfePorEmailAsync(int notaFiscalId, string? emailDestino = null);
}

/// <summary>
/// Serviço de NF-e com integração SEFAZ usando comunicação HTTP/XML nativa
/// </summary>
public class NfeService : INfeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NfeService> _logger;
    private readonly NfeConfig _config;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly DanfeService _danfeService;
    private readonly IEmailService _emailService;
    private X509Certificate2? _certificado;

    // URLs dos web services SEFAZ (Homologação)
    private static readonly Dictionary<int, string> UrlsAutorizacao = new()
    {
        { 53, "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao4/NFeAutorizacao4.asmx" }, // DF usa SVRS
        { 35, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx" }, // SP
        { 33, "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao4/NFeAutorizacao4.asmx" }, // RJ usa SVRS
        { 31, "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeAutorizacao4" }, // MG
        { 29, "https://hnfe.sefaz.ba.gov.br/webservices/NFeAutorizacao4/NFeAutorizacao4.asmx" }, // BA
        { 43, "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }, // RS
        { 41, "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeAutorizacao4" }, // PR
    };

    public NfeService(
        ApplicationDbContext context,
        ILogger<NfeService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        DanfeService danfeService,
        IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _config = new NfeConfig();
        configuration.GetSection("NFe").Bind(_config);
        _danfeService = danfeService;
        _emailService = emailService;
        
        // Configurar HttpClient com certificado
        _httpClient = httpClientFactory.CreateClient("SefazClient");
    }

    public bool IsConfigured()
    {
        try
        {
            if (string.IsNullOrEmpty(_config.Emitente.Cnpj))
            {
                _logger.LogWarning("[NFe] CNPJ do emitente não configurado");
                return false;
            }

            var certPath = ObterCaminhoCertificado();
            if (!File.Exists(certPath))
            {
                _logger.LogWarning("[NFe] Certificado digital não encontrado: {Path}", certPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao verificar configuração");
            return false;
        }
    }

    private X509Certificate2 ObterCertificado()
    {
        if (_certificado != null && _certificado.NotAfter > DateTime.Now)
            return _certificado;

        var certPath = ObterCaminhoCertificado();
        var senha = ObterSenhaCertificado();

        _logger.LogInformation("[NFe] Carregando certificado de: {Path}", certPath);

        _certificado = new X509Certificate2(
            certPath,
            senha,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable
        );

        _logger.LogInformation("[NFe] Certificado carregado. Válido até: {ValidTo}", _certificado.NotAfter);

        return _certificado;
    }

    private string ObterCaminhoCertificado()
    {
        var path = Environment.GetEnvironmentVariable("NFE_CERT_PATH");

        if (string.IsNullOrEmpty(path))
            path = _config.Certificado.CaminhoArquivo;

        if (!Path.IsPathRooted(path))
        {
            var baseDir = Directory.GetCurrentDirectory();
            path = Path.Combine(baseDir, path);

            if (!File.Exists(path))
            {
                var altPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", _config.Certificado.CaminhoArquivo);
                altPath = Path.GetFullPath(altPath);
                if (File.Exists(altPath))
                    path = altPath;
            }
        }

        return path;
    }

    private string ObterSenhaCertificado()
    {
        var senha = Environment.GetEnvironmentVariable("NFE_CERT_PASSWORD");
        if (!string.IsNullOrEmpty(senha))
            return senha;

        var senhaCriptografada = _configuration["NFe:Certificado:SenhaCriptografada"];
        if (!string.IsNullOrEmpty(senhaCriptografada))
            return DescriptografarSenha(senhaCriptografada);

        senha = _config.Certificado.Senha;
        if (string.IsNullOrEmpty(senha))
            throw new InvalidOperationException("Senha do certificado não configurada");

        return senha;
    }

    private string DescriptografarSenha(string senhaCriptografada)
    {
        var chave = Environment.GetEnvironmentVariable("NFE_ENCRYPTION_KEY");
        if (string.IsNullOrEmpty(chave))
            throw new InvalidOperationException("Chave de descriptografia não encontrada");

        var dadosCriptografados = Convert.FromBase64String(senhaCriptografada);

        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(chave);

        var iv = new byte[16];
        var cipherText = new byte[dadosCriptografados.Length - 16];
        Array.Copy(dadosCriptografados, 0, iv, 0, 16);
        Array.Copy(dadosCriptografados, 16, cipherText, 0, cipherText.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(cipherText);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);

        return sr.ReadToEnd();
    }

    public static (string senhaCriptografada, string chave) CriptografarSenha(string senha)
    {
        using var aes = Aes.Create();
        aes.GenerateKey();
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var senhaBytes = Encoding.UTF8.GetBytes(senha);
        var cipherText = encryptor.TransformFinalBlock(senhaBytes, 0, senhaBytes.Length);

        var resultado = new byte[aes.IV.Length + cipherText.Length];
        Array.Copy(aes.IV, 0, resultado, 0, aes.IV.Length);
        Array.Copy(cipherText, 0, resultado, aes.IV.Length, cipherText.Length);

        return (Convert.ToBase64String(resultado), Convert.ToBase64String(aes.Key));
    }

    public async Task<NfeResponseDto> EmitirNfeAsync(int pedidoId, string? naturezaOperacao = null)
    {
        _logger.LogInformation("[NFe] Iniciando emissão para pedido {PedidoId}", pedidoId);

        try
        {
            if (!IsConfigured())
            {
                return new NfeResponseDto
                {
                    Sucesso = false,
                    Mensagem = "Serviço de NF-e não está configurado corretamente"
                };
            }

            // Buscar pedido com relacionamentos
            var pedido = await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.EnderecoEntrega)
                .Include(p => p.Carrinho)
                    .ThenInclude(c => c.Itens)
                        .ThenInclude(i => i.Produto)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);

            if (pedido == null)
            {
                return new NfeResponseDto { Sucesso = false, Mensagem = "Pedido não encontrado" };
            }

            // Verificar se já existe NF-e autorizada
            var nfeExistente = await _context.Set<NotaFiscal>()
                .FirstOrDefaultAsync(n => n.PedidoId == pedidoId && n.Status == StatusNotaFiscal.Autorizada);

            if (nfeExistente != null)
            {
                return new NfeResponseDto
                {
                    Sucesso = false,
                    Mensagem = $"Já existe NF-e autorizada. Chave: {nfeExistente.ChaveAcesso}"
                };
            }

            var numero = await ObterProximoNumeroAsync(_config.Serie);
            var chaveAcesso = GerarChaveAcesso(numero);

            // Gerar XML da NF-e
            var xmlNfe = GerarXmlNfe(pedido, numero, chaveAcesso, naturezaOperacao ?? "Venda de mercadoria adquirida ou recebida de terceiros");

            // Assinar XML
            var xmlAssinado = AssinarXml(xmlNfe, chaveAcesso);

            _logger.LogInformation("[NFe] XML gerado e assinado. Chave: {Chave}", chaveAcesso);

            // Criar registro no banco
            var notaFiscal = new NotaFiscal
            {
                PedidoId = pedidoId,
                Numero = numero,
                Serie = _config.Serie,
                ChaveAcesso = chaveAcesso,
                XmlNfe = xmlAssinado,
                Status = StatusNotaFiscal.Pendente,
                CodigoStatus = 0,
                MensagemStatus = "Aguardando envio para SEFAZ",
                Ambiente = _config.Ambiente,
                ValorTotal = pedido.TotalPedido,
                DataEmissao = DateTime.UtcNow
            };

            _context.Set<NotaFiscal>().Add(notaFiscal);
            await _context.SaveChangesAsync();

            // Enviar para SEFAZ
            var resultado = await EnviarParaSefazAsync(xmlAssinado);

            // Atualizar registro
            notaFiscal.Status = resultado.Sucesso ? StatusNotaFiscal.Autorizada : StatusNotaFiscal.Rejeitada;
            notaFiscal.CodigoStatus = resultado.CodigoStatus ?? 0;
            notaFiscal.MensagemStatus = resultado.MensagemStatus;
            notaFiscal.ProtocoloAutorizacao = resultado.ProtocoloAutorizacao;

            if (resultado.Sucesso)
            {
                notaFiscal.DataAutorizacao = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(resultado.XmlNfe))
                {
                    notaFiscal.XmlProtocolo = resultado.XmlNfe;
                }
            }

            await _context.SaveChangesAsync();

            resultado.NotaFiscalId = notaFiscal.Id;
            resultado.ChaveAcesso = notaFiscal.ChaveAcesso;

            _logger.LogInformation("[NFe] Emissão finalizada. Status: {Status}, Chave: {Chave}",
                resultado.Sucesso ? "Autorizada" : "Rejeitada", notaFiscal.ChaveAcesso);

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao emitir NF-e para pedido {PedidoId}", pedidoId);
            return new NfeResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro ao emitir NF-e: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gera a chave de acesso da NF-e
    /// </summary>
    private string GerarChaveAcesso(int numero)
    {
        var dataEmissao = DateTime.Now;
        
        // Formato: UF(2) + AAMM(4) + CNPJ(14) + Mod(2) + Serie(3) + Numero(9) + TpEmis(1) + cNF(8)
        var chave = new StringBuilder();
        chave.Append(_config.UfEmitente.ToString("D2"));
        chave.Append(dataEmissao.ToString("yyMM"));
        chave.Append(_config.Emitente.Cnpj.PadLeft(14, '0'));
        chave.Append(_config.Modelo.ToString("D2"));
        chave.Append(_config.Serie.ToString("D3"));
        chave.Append(numero.ToString("D9"));
        chave.Append("1"); // Tipo emissão normal
        chave.Append(new Random().Next(10000000, 99999999).ToString("D8"));

        // Calcular dígito verificador (módulo 11)
        var peso = 2;
        var soma = 0;
        for (int i = chave.Length - 1; i >= 0; i--)
        {
            soma += int.Parse(chave[i].ToString()) * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }
        var resto = soma % 11;
        var dv = resto == 0 || resto == 1 ? 0 : 11 - resto;
        chave.Append(dv);

        return chave.ToString();
    }

    /// <summary>
    /// Gera o XML da NF-e
    /// </summary>
    private string GerarXmlNfe(Pedido pedido, int numero, string chaveAcesso, string naturezaOperacao)
    {
        var dataEmissao = DateTime.Now;
        var cNF = chaveAcesso.Substring(35, 8);
        var ufDestino = ObterUfCodigo(pedido.EnderecoEntrega.Estado);
        var cfop = ufDestino == _config.UfEmitente ? _config.CfopDentroEstado : _config.CfopForaEstado;

        var xml = new StringBuilder();
        // NÃO incluir declaração XML - vai dentro do envelope SOAP
        xml.Append("<NFe xmlns=\"http://www.portalfiscal.inf.br/nfe\">");
        xml.Append($"<infNFe versao=\"4.00\" Id=\"NFe{chaveAcesso}\">");

        // IDE - Identificação
        xml.Append("<ide>");
        xml.Append($"<cUF>{_config.UfEmitente}</cUF>");
        xml.Append($"<cNF>{cNF}</cNF>");
        xml.Append($"<natOp>{EscapeXml(naturezaOperacao)}</natOp>");
        xml.Append($"<mod>{_config.Modelo}</mod>");
        xml.Append($"<serie>{_config.Serie}</serie>");
        xml.Append($"<nNF>{numero}</nNF>");
        xml.Append($"<dhEmi>{dataEmissao:yyyy-MM-ddTHH:mm:sszzz}</dhEmi>");
        xml.Append("<tpNF>1</tpNF>"); // 1 = Saída
        xml.Append($"<idDest>{(ufDestino == _config.UfEmitente ? 1 : 2)}</idDest>");
        xml.Append($"<cMunFG>{_config.Emitente.CodigoMunicipio}</cMunFG>");
        xml.Append("<tpImp>1</tpImp>"); // 1 = Retrato
        xml.Append("<tpEmis>1</tpEmis>"); // 1 = Normal
        xml.Append($"<cDV>{chaveAcesso[43]}</cDV>");
        xml.Append($"<tpAmb>{_config.Ambiente}</tpAmb>");
        xml.Append("<finNFe>1</finNFe>"); // 1 = Normal
        xml.Append("<indFinal>1</indFinal>"); // 1 = Consumidor final
        xml.Append("<indPres>2</indPres>"); // 2 = Internet
        xml.Append("<indIntermed>0</indIntermed>"); // 0 = Operação sem intermediador (venda direta)
        xml.Append("<procEmi>0</procEmi>");
        xml.Append("<verProc>CAF-NFe-1.0</verProc>");
        xml.Append("</ide>");

        // Emitente
        xml.Append("<emit>");
        xml.Append($"<CNPJ>{_config.Emitente.Cnpj}</CNPJ>");
        xml.Append($"<xNome>{EscapeXml(_config.Ambiente == 2 ? "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL" : _config.Emitente.RazaoSocial)}</xNome>");
        if (!string.IsNullOrEmpty(_config.Emitente.NomeFantasia))
            xml.Append($"<xFant>{EscapeXml(_config.Emitente.NomeFantasia)}</xFant>");
        xml.Append("<enderEmit>");
        xml.Append($"<xLgr>{EscapeXml(_config.Emitente.Logradouro)}</xLgr>");
        xml.Append($"<nro>{EscapeXml(_config.Emitente.Numero)}</nro>");
        if (!string.IsNullOrEmpty(_config.Emitente.Complemento))
            xml.Append($"<xCpl>{EscapeXml(_config.Emitente.Complemento)}</xCpl>");
        xml.Append($"<xBairro>{EscapeXml(_config.Emitente.Bairro)}</xBairro>");
        xml.Append($"<cMun>{_config.Emitente.CodigoMunicipio}</cMun>");
        xml.Append($"<xMun>{EscapeXml(_config.Emitente.NomeMunicipio)}</xMun>");
        xml.Append($"<UF>{ObterUfSigla(_config.UfEmitente)}</UF>");
        xml.Append($"<CEP>{_config.Emitente.Cep.Replace("-", "")}</CEP>");
        xml.Append("<cPais>1058</cPais>");
        xml.Append("<xPais>Brasil</xPais>");
        if (!string.IsNullOrEmpty(_config.Emitente.Telefone))
            xml.Append($"<fone>{_config.Emitente.Telefone.Replace("-", "").Replace("(", "").Replace(")", "").Replace(" ", "")}</fone>");
        xml.Append("</enderEmit>");
        // IE - Inscrição Estadual do emitente
        var ieEmitente = _config.Emitente.InscricaoEstadual?.Trim().ToUpper();
        if (!string.IsNullOrEmpty(ieEmitente) && ieEmitente != "ISENTO")
        {
            // Remover formatação e garantir apenas números
            var ieNumerica = new string(ieEmitente.Where(char.IsDigit).ToArray());
            xml.Append($"<IE>{ieNumerica}</IE>");
        }
        else
        {
            xml.Append("<IE>ISENTO</IE>");
        }
        xml.Append($"<CRT>{_config.Emitente.RegimeTributario}</CRT>");
        xml.Append("</emit>");

        // Destinatário
        var cpfCnpj = pedido.Cliente.Cpf?.Replace(".", "").Replace("-", "").Replace("/", "") ?? "";
        xml.Append("<dest>");
        if (cpfCnpj.Length == 11)
            xml.Append($"<CPF>{cpfCnpj}</CPF>");
        else if (cpfCnpj.Length == 14)
            xml.Append($"<CNPJ>{cpfCnpj}</CNPJ>");
        xml.Append($"<xNome>{EscapeXml(_config.Ambiente == 2 ? "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL" : pedido.NomeCliente)}</xNome>");
        xml.Append("<enderDest>");
        xml.Append($"<xLgr>{EscapeXml(pedido.EnderecoEntrega.Logradouro)}</xLgr>");
        xml.Append($"<nro>{EscapeXml(pedido.EnderecoEntrega.Numero)}</nro>");
        if (!string.IsNullOrEmpty(pedido.EnderecoEntrega.Complemento))
            xml.Append($"<xCpl>{EscapeXml(pedido.EnderecoEntrega.Complemento)}</xCpl>");
        xml.Append($"<xBairro>{EscapeXml(pedido.EnderecoEntrega.Bairro)}</xBairro>");
        xml.Append($"<cMun>{ObterCodigoMunicipio(pedido.EnderecoEntrega.Cidade, pedido.EnderecoEntrega.Estado)}</cMun>");
        xml.Append($"<xMun>{EscapeXml(pedido.EnderecoEntrega.Cidade)}</xMun>");
        xml.Append($"<UF>{pedido.EnderecoEntrega.Estado}</UF>");
        xml.Append($"<CEP>{pedido.EnderecoEntrega.Cep.Replace("-", "")}</CEP>");
        xml.Append("<cPais>1058</cPais>");
        xml.Append("<xPais>Brasil</xPais>");
        if (!string.IsNullOrEmpty(pedido.TelefoneCliente))
            xml.Append($"<fone>{pedido.TelefoneCliente.Replace("-", "").Replace("(", "").Replace(")", "").Replace(" ", "")}</fone>");
        xml.Append("</enderDest>");
        xml.Append("<indIEDest>9</indIEDest>"); // 9 = Não Contribuinte
        if (!string.IsNullOrEmpty(pedido.EmailCliente))
            xml.Append($"<email>{pedido.EmailCliente}</email>");
        xml.Append("</dest>");

        // Calcular valores para rateio
        var totalItens = pedido.Carrinho.Itens.Count;
        var freteTotal = pedido.PrecoFrete ?? 0m;
        var descontoTotal = pedido.DescontoCupom + pedido.DescontoPorUnidade;
        
        // Ratear frete e desconto entre os itens
        var fretePorItem = totalItens > 0 ? Math.Round(freteTotal / totalItens, 2) : 0m;
        var descontoPorItem = totalItens > 0 ? Math.Round(descontoTotal / totalItens, 2) : 0m;
        
        // Ajustar o último item para compensar diferenças de arredondamento
        var freteAcumulado = 0m;
        var descontoAcumulado = 0m;

        // Itens
        int nItem = 1;
        foreach (var item in pedido.Carrinho.Itens)
        {
            var valorUnitario = item.Produto.Preco;
            var valorTotal = item.Quantidade * valorUnitario;
            
            // Calcular frete e desconto do item (último item recebe a diferença)
            decimal freteItem, descontoItem;
            if (nItem == totalItens)
            {
                // Último item: pega o restante para evitar diferenças de arredondamento
                freteItem = freteTotal - freteAcumulado;
                descontoItem = descontoTotal - descontoAcumulado;
            }
            else
            {
                freteItem = fretePorItem;
                descontoItem = descontoPorItem;
                freteAcumulado += freteItem;
                descontoAcumulado += descontoItem;
            }

            xml.Append($"<det nItem=\"{nItem}\">");
            xml.Append("<prod>");
            xml.Append($"<cProd>{EscapeXml(item.Produto.SKU ?? item.Produto.Id.ToString())}</cProd>");
            xml.Append("<cEAN>SEM GTIN</cEAN>");
            xml.Append($"<xProd>{EscapeXml(_config.Ambiente == 2 ? "NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL" : item.Produto.Nome)}</xProd>");
            xml.Append($"<NCM>{_config.NcmPadrao}</NCM>");
            xml.Append($"<CFOP>{cfop}</CFOP>");
            xml.Append("<uCom>UN</uCom>");
            xml.Append($"<qCom>{item.Quantidade.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}</qCom>");
            xml.Append($"<vUnCom>{valorUnitario.ToString("F10", System.Globalization.CultureInfo.InvariantCulture)}</vUnCom>");
            xml.Append($"<vProd>{valorTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}</vProd>");
            xml.Append("<cEANTrib>SEM GTIN</cEANTrib>");
            xml.Append("<uTrib>UN</uTrib>");
            xml.Append($"<qTrib>{item.Quantidade.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}</qTrib>");
            xml.Append($"<vUnTrib>{valorUnitario.ToString("F10", System.Globalization.CultureInfo.InvariantCulture)}</vUnTrib>");
            // Informar frete e desconto por item
            if (freteItem > 0)
                xml.Append($"<vFrete>{freteItem.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}</vFrete>");
            if (descontoItem > 0)
                xml.Append($"<vDesc>{descontoItem.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}</vDesc>");
            xml.Append("<indTot>1</indTot>");
            xml.Append("</prod>");
            
            xml.Append("<imposto>");
            xml.Append("<ICMS>");
            xml.Append("<ICMSSN102>");
            xml.Append("<orig>0</orig>");
            xml.Append("<CSOSN>102</CSOSN>");
            xml.Append("</ICMSSN102>");
            xml.Append("</ICMS>");
            xml.Append("<PIS>");
            xml.Append("<PISOutr>");
            xml.Append("<CST>99</CST>");
            xml.Append("<vBC>0.00</vBC>");
            xml.Append("<pPIS>0.00</pPIS>");
            xml.Append("<vPIS>0.00</vPIS>");
            xml.Append("</PISOutr>");
            xml.Append("</PIS>");
            xml.Append("<COFINS>");
            xml.Append("<COFINSOutr>");
            xml.Append("<CST>99</CST>");
            xml.Append("<vBC>0.00</vBC>");
            xml.Append("<pCOFINS>0.00</pCOFINS>");
            xml.Append("<vCOFINS>0.00</vCOFINS>");
            xml.Append("</COFINSOutr>");
            xml.Append("</COFINS>");
            xml.Append("</imposto>");
            xml.Append("</det>");

            nItem++;
        }

        // Totais - usando InvariantCulture para garantir ponto decimal
        var totalProdutos = pedido.Carrinho.Itens.Sum(i => i.Quantidade * i.Produto.Preco);
        
        xml.Append("<total>");
        xml.Append("<ICMSTot>");
        xml.Append("<vBC>0.00</vBC>");
        xml.Append("<vICMS>0.00</vICMS>");
        xml.Append("<vICMSDeson>0.00</vICMSDeson>");
        xml.Append("<vFCP>0.00</vFCP>");
        xml.Append("<vBCST>0.00</vBCST>");
        xml.Append("<vST>0.00</vST>");
        xml.Append("<vFCPST>0.00</vFCPST>");
        xml.Append("<vFCPSTRet>0.00</vFCPSTRet>");
        xml.Append($"<vProd>{totalProdutos.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}</vProd>");
        xml.Append($"<vFrete>{freteTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}</vFrete>");
        xml.Append("<vSeg>0.00</vSeg>");
        xml.Append($"<vDesc>{descontoTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}</vDesc>");
        xml.Append("<vII>0.00</vII>");
        xml.Append("<vIPI>0.00</vIPI>");
        xml.Append("<vIPIDevol>0.00</vIPIDevol>");
        xml.Append("<vPIS>0.00</vPIS>");
        xml.Append("<vCOFINS>0.00</vCOFINS>");
        xml.Append("<vOutro>0.00</vOutro>");
        xml.Append($"<vNF>{pedido.TotalPedido.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}</vNF>");
        xml.Append("</ICMSTot>");
        xml.Append("</total>");

        // Transporte - modFrete: 0=Emitente, 1=Destinatário, 9=Sem frete
        xml.Append("<transp>");
        xml.Append($"<modFrete>{(freteTotal > 0 ? "0" : "9")}</modFrete>");
        xml.Append("</transp>");

        // Pagamento
        var codigoPagamento = ObterCodigoPagamento(pedido.MetodoPagamento);
        var descricaoPagamento = ObterDescricaoPagamento(pedido.MetodoPagamento);
        xml.Append("<pag>");
        xml.Append("<detPag>");
        xml.Append($"<tPag>{codigoPagamento}</tPag>");
        // Se for código 99 (outros), a descrição é obrigatória
        if (codigoPagamento == "99")
            xml.Append($"<xPag>{EscapeXml(descricaoPagamento)}</xPag>");
        xml.Append($"<vPag>{pedido.TotalPedido.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}</vPag>");
        xml.Append("</detPag>");
        xml.Append("</pag>");

        // Informações adicionais
        xml.Append("<infAdic>");
        xml.Append($"<infCpl>{EscapeXml($"Pedido: {pedido.CodigoPedido}{(!string.IsNullOrEmpty(pedido.Observacoes) ? $" | Obs: {pedido.Observacoes}" : "")}")}</infCpl>");
        xml.Append("</infAdic>");

        xml.Append("</infNFe>");
        xml.Append("</NFe>");

        return xml.ToString();
    }

    /// <summary>
    /// Assina o XML com certificado digital
    /// </summary>
    private string AssinarXml(string xml, string chaveAcesso)
    {
        var doc = new XmlDocument();
        doc.PreserveWhitespace = true;
        doc.LoadXml(xml);

        var certificado = ObterCertificado();

        // Criar referência para assinatura
        var reference = new Reference($"#NFe{chaveAcesso}")
        {
            DigestMethod = SignedXml.XmlDsigSHA1Url
        };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());

        // Criar objeto de assinatura
        var signedXml = new SignedXml(doc)
        {
            SigningKey = certificado.GetRSAPrivateKey()
        };
        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
        signedXml.AddReference(reference);

        // Adicionar informação do certificado
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificado));
        signedXml.KeyInfo = keyInfo;

        // Calcular assinatura
        signedXml.ComputeSignature();

        // Inserir assinatura no XML
        var nfeNode = doc.GetElementsByTagName("NFe")[0] as XmlElement;
        nfeNode?.AppendChild(doc.ImportNode(signedXml.GetXml(), true));

        return doc.OuterXml;
    }

    /// <summary>
    /// Envia XML para SEFAZ via Web Service SOAP
    /// </summary>
    private async Task<NfeResponseDto> EnviarParaSefazAsync(string xmlAssinado)
    {
        try
        {
            _logger.LogInformation("[NFe] Iniciando envio para SEFAZ (Ambiente: {Amb})", 
                _config.Ambiente == 1 ? "Produção" : "Homologação");

            // Criar envelope de lote para envio
            var enviNFe = CriarEnvelopeLote(xmlAssinado);

            // Obter URL do web service
            var urlAutorizacao = ObterUrlSefaz(_config.UfEmitente, "Autorizacao");

            _logger.LogInformation("[NFe] URL SEFAZ: {Url}", urlAutorizacao);

            // Configurar HttpClient com certificado e TLS 1.2
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            
            using var handler = new HttpClientHandler();
            var certificado = ObterCertificado();
            handler.ClientCertificates.Add(certificado);
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(120);

            // Criar envelope SOAP - usando formato que funciona com SEFAZ
            var soapEnvelope = CriarSoapEnvelope(enviNFe);

            _logger.LogInformation("[NFe] Tamanho do envelope SOAP: {Size} bytes", soapEnvelope.Length);

            // Criar request com headers corretos para SOAP 1.2
            using var request = new HttpRequestMessage(HttpMethod.Post, urlAutorizacao);
            request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "application/soap+xml");
            request.Content.Headers.ContentType!.CharSet = "utf-8";
            
            // Alguns servidores SEFAZ exigem SOAPAction no header
            request.Headers.Add("SOAPAction", "http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4/nfeAutorizacaoLote");

            _logger.LogInformation("[NFe] Enviando requisição SOAP...");

            var response = await client.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[NFe] Resposta SEFAZ Status HTTP: {Status}", response.StatusCode);
            
            if (responseContent.Length > 0)
            {
                _logger.LogInformation("[NFe] Resposta SEFAZ (primeiros 1000 chars): {Content}", 
                    responseContent.Length > 1000 ? responseContent.Substring(0, 1000) : responseContent);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[NFe] Erro HTTP da SEFAZ: {Status} - {Content}", 
                    response.StatusCode, responseContent);
                    
                return new NfeResponseDto
                {
                    Sucesso = false,
                    CodigoStatus = (int)response.StatusCode,
                    Mensagem = $"Erro HTTP: {response.StatusCode}",
                    MensagemStatus = responseContent.Length > 500 
                        ? responseContent.Substring(0, 500) 
                        : responseContent
                };
            }

            // Processar resposta SEFAZ
            return ProcessarRespostaSefaz(responseContent, xmlAssinado);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[NFe] Erro de conexão com SEFAZ");
            return new NfeResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro de conexão com SEFAZ: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao enviar para SEFAZ");
            return new NfeResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro de comunicação: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Cria envelope de lote para envio
    /// </summary>
    private string CriarEnvelopeLote(string xmlAssinado)
    {
        var idLote = DateTime.Now.ToString("yyMMddHHmmssfff");
        
        return $"<enviNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"4.00\"><idLote>{idLote}</idLote><indSinc>1</indSinc>{xmlAssinado}</enviNFe>";
    }

    /// <summary>
    /// Cria envelope SOAP 1.2 para requisição aos Web Services SEFAZ
    /// O formato deve ser exatamente como a SEFAZ espera
    /// </summary>
    private string CriarSoapEnvelope(string nfeDadosMsg)
    {
        // Formato SOAP 1.2 compatível com web services da SEFAZ
        // Testado com SVRS (Servidor Virtual do RS usado por DF, RJ, etc)
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append("<soap12:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" ");
        sb.Append("xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" ");
        sb.Append("xmlns:soap12=\"http://www.w3.org/2003/05/soap-envelope\">");
        sb.Append("<soap12:Body>");
        sb.Append("<nfeDadosMsg xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4\">");
        sb.Append(nfeDadosMsg);
        sb.Append("</nfeDadosMsg>");
        sb.Append("</soap12:Body>");
        sb.Append("</soap12:Envelope>");
        
        return sb.ToString();
    }

    /// <summary>
    /// Obtém URL do Web Service SEFAZ
    /// </summary>
    private string ObterUrlSefaz(int ufCodigo, string servico)
    {
        // URLs de Homologação - SVRS (Ambiente Virtual RS para estados sem WS próprio)
        var urlsHomologacao = new Dictionary<string, Dictionary<int, string>>
        {
            ["Autorizacao"] = new()
            {
                { 53, "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }, // DF (SVRS)
                { 35, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx" }, // SP
                { 33, "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }, // RJ (SVRS)
                { 31, "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeAutorizacao4" }, // MG
                { 29, "https://hnfe.sefaz.ba.gov.br/webservices/NFeAutorizacao4/NFeAutorizacao4.asmx" }, // BA
                { 43, "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }, // RS
                { 41, "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeAutorizacao4?wsdl" }, // PR
                { 26, "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }, // PE (SVRS)
                { 23, "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }, // CE (SVRS)
                { 52, "https://homologacao.sefaz.go.gov.br/nfe/services/NFeAutorizacao4" }, // GO
                { 50, "https://homologacao.nfe.sefaz.ms.gov.br/ws/NFeAutorizacao4" }, // MS
                { 51, "https://homologacao.sefaz.mt.gov.br/nfews/v2/services/NfeAutorizacao4" }, // MT
            },
            ["Consulta"] = new()
            {
                { 53, "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeConsulta/NFeConsulta4.asmx" },
                { 35, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx" },
            },
            ["Inutilizacao"] = new()
            {
                { 53, "https://nfe-homologacao.svrs.rs.gov.br/ws/nfeinutilizacao/nfeinutilizacao4.asmx" },
                { 35, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeinutilizacao4.asmx" },
            },
            ["Evento"] = new()
            {
                { 53, "https://nfe-homologacao.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx" },
                { 35, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nferecepcaoevento4.asmx" },
            }
        };

        // URLs de Produção
        var urlsProducao = new Dictionary<string, Dictionary<int, string>>
        {
            ["Autorizacao"] = new()
            {
                { 53, "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }, // DF (SVRS)
                { 35, "https://nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx" }, // SP
            }
        };

        var urls = _config.Ambiente == 1 ? urlsProducao : urlsHomologacao;

        if (urls.TryGetValue(servico, out var servicoUrls) && servicoUrls.TryGetValue(ufCodigo, out var url))
        {
            return url;
        }

        // Fallback para SVRS
        return _config.Ambiente == 1 
            ? "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx"
            : "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx";
    }

    /// <summary>
    /// Processa resposta do SEFAZ
    /// </summary>
    private NfeResponseDto ProcessarRespostaSefaz(string xmlResposta, string xmlEnviado)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlResposta);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("nfe", "http://www.portalfiscal.inf.br/nfe");

            // Buscar status do retorno
            var cStatNode = doc.SelectSingleNode("//nfe:cStat", nsmgr) 
                ?? doc.SelectSingleNode("//*[local-name()='cStat']");
            var xMotivoNode = doc.SelectSingleNode("//nfe:xMotivo", nsmgr) 
                ?? doc.SelectSingleNode("//*[local-name()='xMotivo']");
            var nProtNode = doc.SelectSingleNode("//nfe:nProt", nsmgr) 
                ?? doc.SelectSingleNode("//*[local-name()='nProt']");

            var cStat = cStatNode?.InnerText ?? "999";
            var xMotivo = xMotivoNode?.InnerText ?? "Resposta não identificada";
            var nProt = nProtNode?.InnerText;

            var codigoStatus = int.TryParse(cStat, out var codigo) ? codigo : 999;

            // Status 100 = Autorizado, 104 = Lote processado
            var sucesso = codigoStatus == 100 || codigoStatus == 104;

            // Se lote processado, verificar status do protocolo individual
            if (codigoStatus == 104)
            {
                var protNFeNode = doc.SelectSingleNode("//nfe:protNFe//nfe:infProt", nsmgr)
                    ?? doc.SelectSingleNode("//*[local-name()='protNFe']//*[local-name()='infProt']");
                
                if (protNFeNode != null)
                {
                    var cStatProt = protNFeNode.SelectSingleNode("*[local-name()='cStat']")?.InnerText;
                    var xMotivoProt = protNFeNode.SelectSingleNode("*[local-name()='xMotivo']")?.InnerText;
                    var nProtProt = protNFeNode.SelectSingleNode("*[local-name()='nProt']")?.InnerText;

                    codigoStatus = int.TryParse(cStatProt, out var codProt) ? codProt : codigoStatus;
                    xMotivo = xMotivoProt ?? xMotivo;
                    nProt = nProtProt ?? nProt;
                    sucesso = codigoStatus == 100;
                }
            }

            _logger.LogInformation("[NFe] Resposta SEFAZ: Status={Status}, Motivo={Motivo}, Protocolo={Protocolo}",
                codigoStatus, xMotivo, nProt);

            return new NfeResponseDto
            {
                Sucesso = sucesso,
                CodigoStatus = codigoStatus,
                MensagemStatus = xMotivo,
                ProtocoloAutorizacao = nProt,
                Mensagem = sucesso ? "NF-e autorizada com sucesso" : $"Rejeição: {xMotivo}",
                XmlNfe = sucesso ? ObterXmlProtocolado(xmlEnviado, xmlResposta) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao processar resposta SEFAZ");
            return new NfeResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro ao processar resposta: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gera XML protocolado (NF-e + Protocolo)
    /// </summary>
    private string? ObterXmlProtocolado(string xmlNfe, string xmlResposta)
    {
        try
        {
            var docNfe = new XmlDocument();
            docNfe.LoadXml(xmlNfe);

            var docResp = new XmlDocument();
            docResp.LoadXml(xmlResposta);

            var protNFe = docResp.SelectSingleNode("//*[local-name()='protNFe']");
            if (protNFe == null) return null;

            var nfeProc = new StringBuilder();
            nfeProc.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            nfeProc.AppendLine("<nfeProc xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"4.00\">");
            nfeProc.Append(docNfe.DocumentElement?.OuterXml);
            nfeProc.Append(protNFe.OuterXml);
            nfeProc.AppendLine("</nfeProc>");

            return nfeProc.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Escapa caracteres especiais para XML
    /// </summary>
    private string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private string ObterCodigoPagamento(string metodoPagamento)
    {
        return metodoPagamento?.ToLower() switch
        {
            "pix" => "17",
            "credit_card" => "03",
            "debit_card" => "04",
            "boleto" => "15",
            _ => "99"
        };
    }

    private string ObterDescricaoPagamento(string metodoPagamento)
    {
        return metodoPagamento?.ToLower() switch
        {
            "pix" => "Pagamento via PIX",
            "credit_card" => "Cartao de Credito",
            "debit_card" => "Cartao de Debito",
            "boleto" => "Boleto Bancario",
            _ => metodoPagamento ?? "Outros"
        };
    }

    private string ObterUfSigla(int codigo)
    {
        return codigo switch
        {
            12 => "AC", 27 => "AL", 16 => "AP", 13 => "AM", 29 => "BA",
            23 => "CE", 53 => "DF", 32 => "ES", 52 => "GO", 21 => "MA",
            51 => "MT", 50 => "MS", 31 => "MG", 15 => "PA", 25 => "PB",
            41 => "PR", 26 => "PE", 22 => "PI", 33 => "RJ", 24 => "RN",
            43 => "RS", 11 => "RO", 14 => "RR", 42 => "SC", 35 => "SP",
            28 => "SE", 17 => "TO",
            _ => "DF"
        };
    }

    private int ObterUfCodigo(string sigla)
    {
        return sigla?.ToUpper() switch
        {
            "AC" => 12, "AL" => 27, "AP" => 16, "AM" => 13, "BA" => 29,
            "CE" => 23, "DF" => 53, "ES" => 32, "GO" => 52, "MA" => 21,
            "MT" => 51, "MS" => 50, "MG" => 31, "PA" => 15, "PB" => 25,
            "PR" => 41, "PE" => 26, "PI" => 22, "RJ" => 33, "RN" => 24,
            "RS" => 43, "RO" => 11, "RR" => 14, "SC" => 42, "SP" => 35,
            "SE" => 28, "TO" => 17,
            _ => 53
        };
    }

    private long ObterCodigoMunicipio(string cidade, string uf)
    {
        // TODO: Implementar busca real na tabela IBGE
        return uf?.ToUpper() switch
        {
            "DF" => 5300108, "SP" => 3550308, "RJ" => 3304557, "MG" => 3106200,
            "BA" => 2927408, "RS" => 4314902, "PR" => 4106902, "PE" => 2611606,
            "CE" => 2304400, "GO" => 5208707,
            _ => 5300108
        };
    }

    public async Task<NfeResponseDto> ConsultarNfeAsync(string chaveAcesso)
    {
        _logger.LogInformation("[NFe] Consultando NF-e: {Chave}", chaveAcesso);

        try
        {
            var nfe = await _context.Set<NotaFiscal>()
                .FirstOrDefaultAsync(n => n.ChaveAcesso == chaveAcesso);

            if (nfe == null)
            {
                return new NfeResponseDto
                {
                    Sucesso = false,
                    Mensagem = "NF-e não encontrada"
                };
            }

            return new NfeResponseDto
            {
                Sucesso = nfe.Status == StatusNotaFiscal.Autorizada,
                CodigoStatus = nfe.CodigoStatus,
                MensagemStatus = nfe.MensagemStatus,
                ProtocoloAutorizacao = nfe.ProtocoloAutorizacao,
                ChaveAcesso = nfe.ChaveAcesso,
                NotaFiscalId = nfe.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao consultar NF-e");
            return new NfeResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro ao consultar: {ex.Message}"
            };
        }
    }

    public async Task<NfeResponseDto> CancelarNfeAsync(int notaFiscalId, string motivo)
    {
        _logger.LogInformation("[NFe] Cancelando NF-e {Id}", notaFiscalId);

        try
        {
            var nfe = await _context.Set<NotaFiscal>()
                .FirstOrDefaultAsync(n => n.Id == notaFiscalId);

            if (nfe == null)
                return new NfeResponseDto { Sucesso = false, Mensagem = "NF-e não encontrada" };

            if (nfe.Status != StatusNotaFiscal.Autorizada)
                return new NfeResponseDto { Sucesso = false, Mensagem = "Apenas NF-e autorizadas podem ser canceladas" };

            // Simular cancelamento
            nfe.Status = StatusNotaFiscal.Cancelada;
            nfe.MotivoCancelamento = motivo;
            nfe.DataCancelamento = DateTime.UtcNow;
            nfe.ProtocoloCancelamento = $"CAN{DateTime.Now:yyMMddHHmmssfff}";

            await _context.SaveChangesAsync();

            return new NfeResponseDto
            {
                Sucesso = true,
                Mensagem = "NF-e cancelada com sucesso",
                NotaFiscalId = nfe.Id,
                ChaveAcesso = nfe.ChaveAcesso
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao cancelar NF-e");
            return new NfeResponseDto { Sucesso = false, Mensagem = $"Erro: {ex.Message}" };
        }
    }

    public async Task<NfeResponseDto> InutilizarNumeracaoAsync(int serie, int numeroInicial, int numeroFinal, string justificativa)
    {
        _logger.LogInformation("[NFe] Inutilizando numeração {Inicio} a {Fim}", numeroInicial, numeroFinal);

        try
        {
            // TODO: Implementar inutilização real na SEFAZ
            return new NfeResponseDto
            {
                Sucesso = true,
                CodigoStatus = 102,
                MensagemStatus = "Inutilização de número homologado",
                Mensagem = "Numeração inutilizada com sucesso"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao inutilizar numeração");
            return new NfeResponseDto { Sucesso = false, Mensagem = $"Erro: {ex.Message}" };
        }
    }

    public async Task<List<ConsultaNfeDto>> ListarPorPedidoAsync(int pedidoId)
    {
        return await _context.Set<NotaFiscal>()
            .Include(n => n.Pedido)
            .Where(n => n.PedidoId == pedidoId)
            .OrderByDescending(n => n.DataEmissao)
            .Select(n => new ConsultaNfeDto
            {
                Id = n.Id,
                PedidoId = n.PedidoId,
                CodigoPedido = n.Pedido.CodigoPedido,
                Numero = n.Numero,
                Serie = n.Serie,
                ChaveAcesso = n.ChaveAcesso,
                Status = n.Status.ToString(),
                CodigoStatus = n.CodigoStatus,
                MensagemStatus = n.MensagemStatus,
                Ambiente = n.Ambiente == 1 ? "Produção" : "Homologação",
                ValorTotal = n.ValorTotal,
                DataEmissao = n.DataEmissao,
                DataAutorizacao = n.DataAutorizacao,
                ProtocoloAutorizacao = n.ProtocoloAutorizacao
            })
            .ToListAsync();
    }

    public async Task<string?> ObterXmlAsync(int notaFiscalId)
    {
        var nfe = await _context.Set<NotaFiscal>()
            .FirstOrDefaultAsync(n => n.Id == notaFiscalId);

        return nfe?.XmlProtocolo ?? nfe?.XmlNfe;
    }

    public async Task<byte[]?> GerarDanfeAsync(int notaFiscalId)
    {
        try
        {
            _logger.LogInformation("[NFe] Gerando DANFE para NF-e {Id}", notaFiscalId);
            
            var nfe = await _context.Set<NotaFiscal>()
                .FirstOrDefaultAsync(n => n.Id == notaFiscalId);

            if (nfe == null)
            {
                _logger.LogWarning("[NFe] NF-e {Id} não encontrada", notaFiscalId);
                return null;
            }

            if (string.IsNullOrEmpty(nfe.XmlNfe) && string.IsNullOrEmpty(nfe.XmlProtocolo))
            {
                _logger.LogWarning("[NFe] NF-e {Id} não possui XML", notaFiscalId);
                return null;
            }

            var pdfBytes = _danfeService.GerarDanfe(nfe);
            
            _logger.LogInformation("[NFe] DANFE gerado com sucesso. Tamanho: {Size} bytes", pdfBytes.Length);
            
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao gerar DANFE para NF-e {Id}", notaFiscalId);
            return null;
        }
    }

    public async Task<byte[]?> GerarDanfePorChaveAcessoAsync(string chaveAcesso)
    {
        try
        {
            _logger.LogInformation("[NFe] Gerando DANFE para chave de acesso {ChaveAcesso}", chaveAcesso);
            
            var nfe = await _context.Set<NotaFiscal>()
                .FirstOrDefaultAsync(n => n.ChaveAcesso == chaveAcesso && n.Status == StatusNotaFiscal.Autorizada);

            if (nfe == null)
            {
                _logger.LogWarning("[NFe] NF-e com chave {ChaveAcesso} não encontrada ou não autorizada", chaveAcesso);
                return null;
            }

            if (string.IsNullOrEmpty(nfe.XmlNfe) && string.IsNullOrEmpty(nfe.XmlProtocolo))
            {
                _logger.LogWarning("[NFe] NF-e com chave {ChaveAcesso} não possui XML", chaveAcesso);
                return null;
            }

            var pdfBytes = _danfeService.GerarDanfe(nfe);
            
            _logger.LogInformation("[NFe] DANFE gerado com sucesso via chave de acesso. Tamanho: {Size} bytes", pdfBytes.Length);
            
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao gerar DANFE para chave {ChaveAcesso}", chaveAcesso);
            return null;
        }
    }

    public async Task<int> ObterProximoNumeroAsync(int serie)
    {
        var ultimoNumero = await _context.Set<NotaFiscal>()
            .Where(n => n.Serie == serie)
            .MaxAsync(n => (int?)n.Numero) ?? 0;

        return ultimoNumero + 1;
    }

    public async Task<PaginatedPedidoNfeDto> ListarPedidosComNfeAsync(int pageNumber, int pageSize, string? busca = null)
    {
        var query = _context.Pedidos
            .Include(p => p.Cliente)
            .Where(p => _context.Set<NotaFiscal>().Any(n => n.PedidoId == p.Id))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            query = query.Where(p => 
                p.CodigoPedido.Contains(busca) || 
                p.NomeCliente.Contains(busca) ||
                p.EmailCliente.Contains(busca));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pedidos = await query
            .OrderByDescending(p => p.DataPedido)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pedidoIds = pedidos.Select(p => p.Id).ToList();
        var notasFiscais = await _context.Set<NotaFiscal>()
            .Where(n => pedidoIds.Contains(n.PedidoId))
            .ToListAsync();

        var items = pedidos.Select(p =>
        {
            var nota = notasFiscais.FirstOrDefault(n => n.PedidoId == p.Id);
            return new PedidoNfeListDto
            {
                Id = p.Id,
                CodigoPedido = p.CodigoPedido,
                NomeCliente = p.NomeCliente,
                EmailCliente = p.EmailCliente,
                TotalPedido = p.TotalPedido,
                DataPedido = p.DataPedido,
                Status = (int)p.Status,
                StatusDescricao = ObterDescricaoStatus(p.Status),
                TemNotaFiscal = nota != null,
                NotaFiscalId = nota?.Id,
                NumeroNfe = nota?.Numero,
                SerieNfe = nota?.Serie,
                ChaveAcesso = nota?.ChaveAcesso,
                StatusNfe = nota?.Status.ToString(),
                DataEmissaoNfe = nota?.DataEmissao
            };
        }).ToList();

        return new PaginatedPedidoNfeDto
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<PaginatedPedidoNfeDto> ListarPedidosSemNfeAsync(int pageNumber, int pageSize, string? busca = null)
    {
        var query = _context.Pedidos
            .Include(p => p.Cliente)
            .Where(p => !_context.Set<NotaFiscal>().Any(n => n.PedidoId == p.Id))
            .Where(p => p.Status != StatusPedido.Cancelado) // Não mostra cancelados
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            query = query.Where(p => 
                p.CodigoPedido.Contains(busca) || 
                p.NomeCliente.Contains(busca) ||
                p.EmailCliente.Contains(busca));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pedidos = await query
            .OrderByDescending(p => p.DataPedido)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = pedidos.Select(p => new PedidoNfeListDto
        {
            Id = p.Id,
            CodigoPedido = p.CodigoPedido,
            NomeCliente = p.NomeCliente,
            EmailCliente = p.EmailCliente,
            TotalPedido = p.TotalPedido,
            DataPedido = p.DataPedido,
            Status = (int)p.Status,
            StatusDescricao = ObterDescricaoStatus(p.Status),
            TemNotaFiscal = false,
            NotaFiscalId = null,
            NumeroNfe = null,
            SerieNfe = null,
            ChaveAcesso = null,
            StatusNfe = null,
            DataEmissaoNfe = null
        }).ToList();

        return new PaginatedPedidoNfeDto
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<PaginatedPedidoNfeDto> ListarTodosPedidosNfeAsync(int pageNumber, int pageSize, string? busca = null)
    {
        var query = _context.Pedidos
            .Include(p => p.Cliente)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            query = query.Where(p => 
                p.CodigoPedido.Contains(busca) || 
                p.NomeCliente.Contains(busca) ||
                p.EmailCliente.Contains(busca));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pedidos = await query
            .OrderByDescending(p => p.DataPedido)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pedidoIds = pedidos.Select(p => p.Id).ToList();
        var notasFiscais = await _context.Set<NotaFiscal>()
            .Where(n => pedidoIds.Contains(n.PedidoId))
            .ToListAsync();

        var items = pedidos.Select(p =>
        {
            var nota = notasFiscais.FirstOrDefault(n => n.PedidoId == p.Id);
            return new PedidoNfeListDto
            {
                Id = p.Id,
                CodigoPedido = p.CodigoPedido,
                NomeCliente = p.NomeCliente,
                EmailCliente = p.EmailCliente,
                TotalPedido = p.TotalPedido,
                DataPedido = p.DataPedido,
                Status = (int)p.Status,
                StatusDescricao = ObterDescricaoStatus(p.Status),
                TemNotaFiscal = nota != null,
                NotaFiscalId = nota?.Id,
                NumeroNfe = nota?.Numero,
                SerieNfe = nota?.Serie,
                ChaveAcesso = nota?.ChaveAcesso,
                StatusNfe = nota?.Status.ToString(),
                DataEmissaoNfe = nota?.DataEmissao
            };
        }).ToList();

        return new PaginatedPedidoNfeDto
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<NfeEstatisticasDto> ObterEstatisticasAsync()
    {
        var totalPedidos = await _context.Pedidos.CountAsync();
        var pedidosComNota = await _context.Set<NotaFiscal>()
            .Select(n => n.PedidoId)
            .Distinct()
            .CountAsync();
        
        var notasAutorizadas = await _context.Set<NotaFiscal>()
            .CountAsync(n => n.Status == StatusNotaFiscal.Autorizada);
        var notasCanceladas = await _context.Set<NotaFiscal>()
            .CountAsync(n => n.Status == StatusNotaFiscal.Cancelada);
        var notasPendentes = await _context.Set<NotaFiscal>()
            .CountAsync(n => n.Status == StatusNotaFiscal.Pendente);
        var notasRejeitadas = await _context.Set<NotaFiscal>()
            .CountAsync(n => n.Status == StatusNotaFiscal.Rejeitada);
        
        var valorTotalNotas = await _context.Set<NotaFiscal>()
            .Where(n => n.Status == StatusNotaFiscal.Autorizada)
            .SumAsync(n => n.ValorTotal);

        return new NfeEstatisticasDto
        {
            TotalPedidos = totalPedidos,
            PedidosComNota = pedidosComNota,
            PedidosSemNota = totalPedidos - pedidosComNota,
            NotasAutorizadas = notasAutorizadas,
            NotasCanceladas = notasCanceladas,
            NotasPendentes = notasPendentes,
            NotasRejeitadas = notasRejeitadas,
            ValorTotalNotas = valorTotalNotas
        };
    }

    private static string ObterDescricaoStatus(StatusPedido status)
    {
        return status switch
        {
            StatusPedido.AguardandoConfirmacao => "Aguardando Confirmação",
            StatusPedido.EmSeparacao => "Em Separação",
            StatusPedido.ACaminho => "A Caminho",
            StatusPedido.Finalizado => "Finalizado",
            StatusPedido.Cancelado => "Cancelado",
            _ => "Desconhecido"
        };
    }

    public async Task<NfeResponseDto> EnviarNfePorEmailAsync(int notaFiscalId, string? emailDestino = null)
    {
        _logger.LogInformation("[NFe] Enviando NF-e {Id} por email", notaFiscalId);

        try
        {
            // Buscar NF-e com o pedido
            var nfe = await _context.Set<NotaFiscal>()
                .Include(n => n.Pedido)
                    .ThenInclude(p => p.Cliente)
                .FirstOrDefaultAsync(n => n.Id == notaFiscalId);

            if (nfe == null)
            {
                return new NfeResponseDto
                {
                    Sucesso = false,
                    Mensagem = "NF-e não encontrada"
                };
            }

            if (nfe.Status != StatusNotaFiscal.Autorizada)
            {
                return new NfeResponseDto
                {
                    Sucesso = false,
                    Mensagem = "Apenas NF-e autorizadas podem ser enviadas por email"
                };
            }

            // Determinar email de destino
            var email = emailDestino ?? nfe.Pedido?.EmailCliente ?? nfe.Pedido?.Cliente?.Email;
            if (string.IsNullOrEmpty(email))
            {
                return new NfeResponseDto
                {
                    Sucesso = false,
                    Mensagem = "Email do destinatário não encontrado"
                };
            }

            // Gerar DANFE em PDF
            var danfePdf = _danfeService.GerarDanfe(nfe);

            // Montar email HTML
            var assunto = $"NF-e {nfe.Numero:D9} - Chase a Flare";
            var corpoHtml = GerarEmailNfeHtml(nfe);

            // Enviar email com anexos (DANFE PDF + XML)
            await EnviarEmailComAnexosAsync(email, assunto, corpoHtml, nfe, danfePdf);

            _logger.LogInformation("[NFe] Email enviado com sucesso para {Email}", email);

            return new NfeResponseDto
            {
                Sucesso = true,
                Mensagem = $"NF-e enviada por email para {email}",
                NotaFiscalId = nfe.Id,
                ChaveAcesso = nfe.ChaveAcesso
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NFe] Erro ao enviar NF-e por email");
            return new NfeResponseDto
            {
                Sucesso = false,
                Mensagem = $"Erro ao enviar email: {ex.Message}"
            };
        }
    }

    private string GerarEmailNfeHtml(NotaFiscal nfe)
    {
        var pedido = nfe.Pedido;
        var nomeCliente = pedido?.NomeCliente?.Split(' ').FirstOrDefault() ?? "Cliente";
        
        // Cores padrão do sistema
        var corAmarelo = "#FFD700";
        var corGraphite = "#2c2c2c";
        var corGraphiteLight = "#f8f8f8";
        var corCinzaTexto = "#6b7280";
        var corBorda = "#e5e7eb";
        var corBranco = "#ffffff";
        
        var backendUrl = _configuration["Backend:currSettingsUrl"];
        var frontendUrl = _configuration["Frontend:currSettingsUrl"];
        var contactEmail = _configuration["Email:ContactEmail"] ?? "contato@chaseaflare.com.br";
        var instagramUrl = _configuration["Email:Instagram"] ?? "https://instagram.com/chaseaflare";
        var tiktokUrl = _configuration["Email:TikTok"] ?? "https://tiktok.com/@chaseaflare";
        var logoUrl = $"{backendUrl}/chaseaflare/CAFLONG.png";
        
        var pedidoSection = pedido != null ? $@"
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Pedido</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">#{pedido.CodigoPedido}</td>
                                    </tr>" : "";
        
        return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Chase a Flare - Nota Fiscal Eletrônica</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif; background-color: #f3f4f6;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f3f4f6; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: {corBranco}; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);"">
                    
                    <!-- Header com Logo -->
                    <tr>
                        <td style=""background: {corBranco}; padding: 40px 30px 30px 30px; text-align: center; border-bottom: 1px solid {corBorda};"">
                            <img src=""{logoUrl}"" alt=""Chase a Flare"" style=""height: 50px; margin-bottom: 0;"" />
                        </td>
                    </tr>
                    
                    <!-- Status Badge -->
                    <tr>
                        <td style=""padding: 40px 30px 20px 30px; text-align: center;"">
                            <div style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 10px 28px; border-radius: 50px; font-weight: 700; font-size: 14px; text-transform: uppercase; letter-spacing: 1px;"">
                                Nota Fiscal Emitida
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Saudação + Mensagem -->
                    <tr>
                        <td style=""padding: 20px 30px 30px 30px; text-align: center;"">
                            <h2 style=""margin: 0 0 12px 0; color: {corGraphite}; font-size: 22px; font-weight: 600;"">
                                Olá, {nomeCliente}!
                            </h2>
                            <p style=""color: {corCinzaTexto}; font-size: 15px; line-height: 1.7; margin: 0; max-width: 480px; margin: 0 auto;"">
                                Segue em anexo a Nota Fiscal Eletrônica referente ao seu pedido. Guarde este documento para eventuais consultas.
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Card de Dados da NF-e -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px;"">
                            <div style=""background: {corGraphiteLight}; border-radius: 12px; overflow: hidden; border: 1px solid {corBorda};"">
                                <div style=""background: #f0f0f0; padding: 16px 20px; border-bottom: 1px solid {corBorda};"">
                                    <h3 style=""margin: 0; color: {corGraphite}; font-size: 16px; font-weight: 600;"">Dados da Nota Fiscal</h3>
                                </div>
                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding: 20px;"">
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Número</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">{nfe.Numero:D9}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Série</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">{nfe.Serie:D3}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 8px 20px; color: {corCinzaTexto}; font-size: 14px;"">Data de Emissão</td>
                                        <td style=""padding: 8px 20px; color: {corGraphite}; font-weight: 600; text-align: right; font-size: 14px;"">{nfe.DataEmissao:dd/MM/yyyy HH:mm}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 16px 20px 8px 20px; color: {corCinzaTexto}; font-size: 14px; border-top: 1px solid {corBorda};"">Valor Total</td>
                                        <td style=""padding: 16px 20px 8px 20px; color: {corGraphite}; font-weight: 700; text-align: right; font-size: 20px; border-top: 1px solid {corBorda};"">R$ {nfe.ValorTotal:N2}</td>
                                    </tr>
                                    {pedidoSection}
                                </table>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Chave de Acesso -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px;"">
                            <div style=""background: {corGraphiteLight}; padding: 20px; border-radius: 12px; border: 1px solid {corBorda};"">
                                <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 12px; text-transform: uppercase; letter-spacing: 1px;"">Chave de Acesso</p>
                                <p style=""margin: 0; font-size: 11px; color: {corGraphite}; font-family: monospace; word-break: break-all; background: #f0f0f0; padding: 12px; border-radius: 6px;"">{nfe.ChaveAcesso}</p>
                                <p style=""margin: 12px 0 0 0; font-size: 12px; color: {corCinzaTexto};"">
                                    Consulte a autenticidade em: <a href=""https://www.nfe.fazenda.gov.br/portal/consultaRecaptcha.aspx"" style=""color: {corGraphite}; font-weight: 600;"" target=""_blank"">Portal SEFAZ</a>
                                </p>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Anexos -->
                    <tr>
                        <td style=""padding: 0 30px 25px 30px;"">
                            <div style=""background: #e8f5e9; border-left: 4px solid #4caf50; padding: 16px 20px; border-radius: 0 8px 8px 0;"">
                                <p style=""margin: 0 0 8px 0; color: #2e7d32; font-size: 14px; font-weight: 600;"">📎 Arquivos em Anexo</p>
                                <p style=""margin: 0; color: #2e7d32; font-size: 13px;"">
                                    • DANFE (PDF) - Documento Auxiliar da NF-e<br>
                                    • XML da NF-e - Arquivo fiscal eletrônico
                                </p>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- CTA Button -->
                    <tr>
                        <td style=""padding: 0 30px 30px 30px; text-align: center;"">
                            <a href=""{frontendUrl}/danfe/{nfe.ChaveAcesso}"" 
                               style=""display: inline-block; background: {corAmarelo}; color: {corGraphite}; padding: 16px 40px; border-radius: 10px; text-decoration: none; font-weight: 700; font-size: 15px;"">
                                Visualizar DANFE Online
                            </a>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style=""background: {corGraphiteLight}; padding: 30px; text-align: center; border-top: 1px solid {corBorda};"">
                            <p style=""margin: 0 0 8px 0; color: {corCinzaTexto}; font-size: 13px;"">
                                Dúvidas? Fale com a gente
                            </p>
                            <a href=""mailto:{contactEmail}"" style=""color: {corGraphite}; font-weight: 600; font-size: 14px; text-decoration: none;"">
                                {contactEmail}
                            </a>
                            
                            <!-- Redes Sociais -->
                            <div style=""margin: 24px 0;"">
                                <a href=""{instagramUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    Instagram
                                </a>
                                <span style=""color: {corBorda};"">|</span>
                                <a href=""{tiktokUrl}"" style=""display: inline-block; margin: 0 8px; color: {corGraphite}; text-decoration: none; font-size: 14px;"">
                                    TikTok
                                </a>
                            </div>
                            
                            <p style=""margin: 0; color: {corCinzaTexto}; font-size: 11px;"">
                                © {DateTime.Now.Year} Chase a Flare. Todos os direitos reservados.
                            </p>
                            <p style=""margin: 8px 0 0 0; color: {corCinzaTexto}; font-size: 10px;"">
                                CNPJ: 63.835.182/0001-30
                            </p>
                        </td>
                    </tr>
                    
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private async Task EnviarEmailComAnexosAsync(string destinatario, string assunto, string corpoHtml, NotaFiscal nfe, byte[] danfePdf)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPassword = _configuration["Email:SmtpPassword"];
        var fromEmail = _configuration["Email:FromEmail"];
        var fromName = _configuration["Email:FromName"] ?? "Chase a Flare";

        using var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(fromName, fromEmail ?? smtpUser));
        message.To.Add(new MimeKit.MailboxAddress("", destinatario));
        message.Subject = assunto;

        var builder = new MimeKit.BodyBuilder();
        builder.HtmlBody = corpoHtml;

        // Anexar DANFE (PDF)
        builder.Attachments.Add($"DANFE_{nfe.Numero:D9}.pdf", danfePdf, new MimeKit.ContentType("application", "pdf"));

        // Anexar XML
        var xmlContent = nfe.XmlProtocolo ?? nfe.XmlNfe;
        if (!string.IsNullOrEmpty(xmlContent))
        {
            var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);
            builder.Attachments.Add($"NFe_{nfe.ChaveAcesso}.xml", xmlBytes, new MimeKit.ContentType("application", "xml"));
        }

        message.Body = builder.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(smtpUser, smtpPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
