using System.Xml;
using cafApi.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace cafApi.Services;

/// <summary>
/// Serviço de geração de DANFE (Documento Auxiliar da Nota Fiscal Eletrônica)
/// Personalizado para Chase a Flare
/// </summary>
public class DanfeService
{
    private readonly ILogger<DanfeService> _logger;
    private readonly IWebHostEnvironment _env;
    
    // Cores formais para DANFE
    private static readonly string CorPrimaria = "#2c2c2c"; // Cinza escuro
    private static readonly string CorSecundaria = "#e0e0e0"; // Cinza claro
    private static readonly string CorFundo = "#f5f5f5";
    private readonly IConfiguration _configuration;

    public DanfeService(ILogger<DanfeService> logger, IWebHostEnvironment env, IConfiguration configuration)
    {
        _logger = logger;
        _env = env;
        _configuration = configuration;
        
        // Configurar licença QuestPDF (Community License para projetos pequenos)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Gera o PDF do DANFE a partir do XML da NF-e
    /// </summary>
    public byte[] GerarDanfe(NotaFiscal notaFiscal)
    {
        try
        {
            _logger.LogInformation("[DANFE] Gerando DANFE para NF-e {Numero}", notaFiscal.Numero);

            var xml = notaFiscal.XmlProtocolo ?? notaFiscal.XmlNfe;
            if (string.IsNullOrEmpty(xml))
                throw new InvalidOperationException("XML da NF-e não encontrado");

            var dadosNfe = ExtrairDadosXml(xml);
            dadosNfe.Protocolo = notaFiscal.ProtocoloAutorizacao;
            dadosNfe.DataAutorizacao = notaFiscal.DataAutorizacao;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(10);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));

                    page.Header().Element(c => ComporCabecalho(c, dadosNfe));
                    page.Content().Element(c => ComporConteudo(c, dadosNfe));
                    page.Footer().Element(c => ComporRodape(c, dadosNfe));
                });
            });

            var pdfBytes = document.GeneratePdf();
            
            _logger.LogInformation("[DANFE] DANFE gerado com sucesso. Tamanho: {Size} bytes", pdfBytes.Length);
            
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DANFE] Erro ao gerar DANFE");
            throw;
        }
    }

    private void ComporCabecalho(IContainer container, DadosNfe dados)
    {
        container.Column(column =>
        {
            // ===== CANHOTO DA NOTA (DESTACÁVEL) =====
            column.Item().Border(1).Row(row =>
            {
                row.RelativeItem(8).Column(leftBlock =>
                {
                    leftBlock.Item().Border(1).Padding(3).Text($"RECEBEMOS DE {dados.Emitente.RazaoSocial.ToUpper()} OS PRODUTOS CONSTANTES DA NOTA FISCAL INDICADA AO LADO").FontSize(6);
                    
                    leftBlock.Item().Height(28).Row(bottomRow =>
                    {
                        bottomRow.RelativeItem().Border(1).Padding(3).Column(c =>
                        {
                            c.Item().Text("DATA DE RECEBIMENTO").FontSize(6);
                            c.Item().PaddingTop(5).Text("____/____/________").FontSize(7);
                        });
                        
                        bottomRow.RelativeItem().Border(1).Padding(3).Column(c =>
                        {
                            c.Item().Text("IDENTIFICAÇÃO E ASSINATURA DO RECEBEDOR").FontSize(6);
                        });
                    });
                });
                
                row.ConstantItem(60).Border(1).Padding(3).AlignCenter().AlignMiddle().Column(c =>
                {
                    c.Item().AlignCenter().Text("NF-e").Bold().FontSize(7);
                    c.Item().AlignCenter().Text($"Nº {dados.Numero.ToString("D9")}").FontSize(7);
                    c.Item().AlignCenter().Text($"Série {dados.Serie}").FontSize(6);
                });
            });
            
            column.Item().AlignCenter().Text("- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -").FontSize(7).FontColor(Colors.Grey.Medium);
            
            column.Item().Height(5);
            
            // ===== INÍCIO DA DANFE =====
            
            column.Item().Border(1).Row(row =>
            {
                row.RelativeItem(4).Border(1).Padding(5).Column(col =>
                {
                    var logoPath = Path.Combine(_env.WebRootPath, "chaseaflare", "CAFLONG.png");
                    if (File.Exists(logoPath))
                    {
                        col.Item().Height(50).Image(logoPath).FitHeight();
                        col.Item().Height(3);
                    }
                    
                    col.Item().Text(dados.Emitente.RazaoSocial).Bold().FontSize(9);
                    if (!string.IsNullOrEmpty(dados.Emitente.NomeFantasia) && dados.Emitente.NomeFantasia != dados.Emitente.RazaoSocial)
                        col.Item().Text(dados.Emitente.NomeFantasia).FontSize(8).FontColor(CorPrimaria);
                    col.Item().Height(2);
                    col.Item().Text($"{dados.Emitente.Logradouro}, {dados.Emitente.Numero}").FontSize(7);
                    col.Item().Text($"{dados.Emitente.Bairro} - {dados.Emitente.Cidade}/{dados.Emitente.UF}").FontSize(7);
                    col.Item().Text($"CEP: {FormatarCep(dados.Emitente.Cep)} - Fone: {FormatarTelefone(dados.Emitente.Telefone)}").FontSize(7);
                });

                row.RelativeItem(2).Border(1).Padding(5).Column(col =>
                {
                    col.Item().AlignCenter().Text("DANFE").Bold().FontSize(14).FontColor(CorPrimaria);
                    col.Item().AlignCenter().Text("Documento Auxiliar da").FontSize(7);
                    col.Item().AlignCenter().Text("Nota Fiscal Eletrônica").FontSize(7);
                    col.Item().Height(8);
                    col.Item().AlignCenter().Text($"0 - ENTRADA").FontSize(6);
                    col.Item().AlignCenter().Text($"1 - SAÍDA").FontSize(6);
                    col.Item().Height(3);
                    col.Item().AlignCenter().Border(1).Padding(3).Background(Colors.White)
                        .Text(dados.TipoOperacao == "1" ? "1" : "0").Bold().FontSize(14);
                    col.Item().Height(5);
                    col.Item().AlignCenter().Text($"Nº {dados.Numero.ToString("D9")}").Bold().FontSize(10).FontColor(CorPrimaria);
                    col.Item().AlignCenter().Text($"Série {dados.Serie.ToString("D3")}").FontSize(8);
                    col.Item().AlignCenter().Text($"Folha 1/1").FontSize(7);
                });

                row.RelativeItem(4).Border(1).Padding(5).Column(col =>
                {
                    col.Item().AlignCenter().Height(40).Element(c => GerarCodigoBarras(c, dados.ChaveAcesso));
                    col.Item().Height(5);
                    col.Item().AlignCenter().Text("CHAVE DE ACESSO").FontSize(6);
                    col.Item().AlignCenter().Text(FormatarChaveAcesso(dados.ChaveAcesso)).Bold().FontSize(7);
                    col.Item().Height(5);
                    col.Item().AlignCenter().Text("Consulta de autenticidade no portal nacional").FontSize(6);
                    col.Item().AlignCenter().Text("da NF-e www.nfe.fazenda.gov.br/portal").FontSize(6);
                });
            });

            column.Item().Border(1).Background(CorSecundaria).Padding(3).Column(col =>
            {
                if (!string.IsNullOrEmpty(dados.Protocolo))
                {
                    col.Item().AlignCenter().Text($"PROTOCOLO DE AUTORIZAÇÃO DE USO: {dados.Protocolo} - {dados.DataAutorizacao:dd/MM/yyyy HH:mm:ss}").Bold().FontSize(8).FontColor(CorPrimaria);
                }
                else
                {
                    col.Item().AlignCenter().Text("NF-E SEM PROTOCOLO DE AUTORIZAÇÃO").FontSize(7);
                }
            });

            column.Item().Height(3);

            column.Item().Border(1).Row(row =>
            {
                row.RelativeItem(7).Border(1).Padding(3).Column(col =>
                {
                    col.Item().Text("NATUREZA DA OPERAÇÃO").FontSize(6);
                    col.Item().Text(dados.NaturezaOperacao).Bold().FontSize(8);
                });
                row.RelativeItem(3).Border(1).Padding(3).Column(col =>
                {
                    col.Item().Text("INSCRIÇÃO ESTADUAL").FontSize(6);
                    col.Item().Text(dados.Emitente.InscricaoEstadual).Bold().FontSize(8);
                });
            });

            column.Item().Border(1).Row(row =>
            {
                row.RelativeItem().Border(1).Padding(3).Column(col =>
                {
                    col.Item().Text("CNPJ").FontSize(6);
                    col.Item().Text(FormatarCnpj(dados.Emitente.Cnpj)).Bold().FontSize(8);
                });
            });
        });
    }

    private void ComporConteudo(IContainer container, DadosNfe dados)
    {
        container.Column(column =>
        {
            column.Item().Height(5);

            column.Item().Border(1).Column(dest =>
            {
                dest.Item().Background(CorPrimaria).Padding(3).Text("DESTINATÁRIO/REMETENTE").Bold().FontSize(8).FontColor(Colors.White);
                
                dest.Item().Row(row =>
                {
                    row.RelativeItem(6).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("NOME/RAZÃO SOCIAL").FontSize(6);
                        col.Item().Text(dados.Destinatario.Nome).Bold().FontSize(8);
                    });
                    row.RelativeItem(2).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("CNPJ/CPF").FontSize(6);
                        col.Item().Text(FormatarCpfCnpj(dados.Destinatario.CpfCnpj)).Bold().FontSize(8);
                    });
                    row.RelativeItem(2).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("DATA EMISSÃO").FontSize(6);
                        col.Item().Text(dados.DataEmissao.ToString("dd/MM/yyyy")).Bold().FontSize(8);
                    });
                });

                dest.Item().Row(row =>
                {
                    row.RelativeItem(5).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("ENDEREÇO").FontSize(6);
                        col.Item().Text($"{dados.Destinatario.Logradouro}, {dados.Destinatario.Numero}").Bold().FontSize(8);
                    });
                    row.RelativeItem(2).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("BAIRRO").FontSize(6);
                        col.Item().Text(dados.Destinatario.Bairro).Bold().FontSize(8);
                    });
                    row.RelativeItem(2).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("CEP").FontSize(6);
                        col.Item().Text(FormatarCep(dados.Destinatario.Cep)).Bold().FontSize(8);
                    });
                    row.RelativeItem(1).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("DATA SAÍDA").FontSize(6);
                        col.Item().Text(dados.DataEmissao.ToString("dd/MM/yyyy")).Bold().FontSize(8);
                    });
                });

                dest.Item().Row(row =>
                {
                    row.RelativeItem(4).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("MUNICÍPIO").FontSize(6);
                        col.Item().Text(dados.Destinatario.Cidade).Bold().FontSize(8);
                    });
                    row.RelativeItem(1).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("UF").FontSize(6);
                        col.Item().Text(dados.Destinatario.UF).Bold().FontSize(8);
                    });
                    row.RelativeItem(2).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("TELEFONE").FontSize(6);
                        col.Item().Text(FormatarTelefone(dados.Destinatario.Telefone)).Bold().FontSize(8);
                    });
                    row.RelativeItem(2).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("IE").FontSize(6);
                        col.Item().Text(dados.Destinatario.InscricaoEstadual ?? "ISENTO").Bold().FontSize(8);
                    });
                    row.RelativeItem(1).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("HORA SAÍDA").FontSize(6);
                        col.Item().Text(dados.DataEmissao.ToString("HH:mm")).Bold().FontSize(8);
                    });
                });
            });

            column.Item().Height(5);

            column.Item().Border(1).Column(prod =>
            {
                prod.Item().Background(CorPrimaria).Padding(3).Text("DADOS DOS PRODUTOS/SERVIÇOS").Bold().FontSize(8).FontColor(Colors.White);
                
                prod.Item().Border(1).Background(CorSecundaria).Row(row =>
                {
                    row.ConstantItem(30).Border(1).Padding(2).AlignCenter().Text("CÓD").FontSize(6).Bold();
                    row.RelativeItem(4).Border(1).Padding(2).Text("DESCRIÇÃO DO PRODUTO/SERVIÇO").FontSize(6).Bold();
                    row.ConstantItem(40).Border(1).Padding(2).AlignCenter().Text("NCM").FontSize(6).Bold();
                    row.ConstantItem(30).Border(1).Padding(2).AlignCenter().Text("CST").FontSize(6).Bold();
                    row.ConstantItem(30).Border(1).Padding(2).AlignCenter().Text("CFOP").FontSize(6).Bold();
                    row.ConstantItem(25).Border(1).Padding(2).AlignCenter().Text("UN").FontSize(6).Bold();
                    row.ConstantItem(35).Border(1).Padding(2).AlignCenter().Text("QTDE").FontSize(6).Bold();
                    row.ConstantItem(50).Border(1).Padding(2).AlignCenter().Text("V. UNIT").FontSize(6).Bold();
                    row.ConstantItem(50).Border(1).Padding(2).AlignCenter().Text("V. TOTAL").FontSize(6).Bold();
                });

                if (dados.Itens.Count == 0)
                {
                    prod.Item().Border(1).Padding(10).AlignCenter().Text("Nenhum item encontrado na nota fiscal").FontSize(8).Italic();
                }
                else
                {
                    foreach (var item in dados.Itens)
                    {
                        prod.Item().Border(1).Row(row =>
                        {
                            row.ConstantItem(30).Border(1).Padding(2).AlignCenter().Text(item.Codigo.Length > 6 ? item.Codigo.Substring(0, 6) : item.Codigo).FontSize(6);
                            row.RelativeItem(4).Border(1).Padding(2).Text(item.Descricao.Length > 50 ? item.Descricao.Substring(0, 50) : item.Descricao).FontSize(6);
                            row.ConstantItem(40).Border(1).Padding(2).AlignCenter().Text(item.Ncm).FontSize(6);
                            row.ConstantItem(30).Border(1).Padding(2).AlignCenter().Text(item.Cst).FontSize(6);
                            row.ConstantItem(30).Border(1).Padding(2).AlignCenter().Text(item.Cfop).FontSize(6);
                            row.ConstantItem(25).Border(1).Padding(2).AlignCenter().Text(item.Unidade).FontSize(6);
                            row.ConstantItem(35).Border(1).Padding(2).AlignRight().Text(item.Quantidade.ToString("N4")).FontSize(6);
                            row.ConstantItem(50).Border(1).Padding(2).AlignRight().Text(item.ValorUnitario.ToString("N2")).FontSize(6);
                            row.ConstantItem(50).Border(1).Padding(2).AlignRight().Text(item.ValorTotal.ToString("N2")).FontSize(6);
                        });
                    }
                }
            });

            column.Item().Height(5);

            column.Item().Border(1).Column(tot =>
            {
                tot.Item().Background(CorPrimaria).Padding(3).Text("CÁLCULO DO IMPOSTO").Bold().FontSize(8).FontColor(Colors.White);
                
                tot.Item().Row(row =>
                {
                    row.RelativeItem().Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("BASE DE CÁLC. ICMS").FontSize(6);
                        col.Item().AlignRight().Text(dados.Totais.BaseIcms.ToString("N2")).Bold().FontSize(8);
                    });
                    row.RelativeItem().Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("VALOR DO ICMS").FontSize(6);
                        col.Item().AlignRight().Text(dados.Totais.ValorIcms.ToString("N2")).Bold().FontSize(8);
                    });
                    row.RelativeItem().Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("VALOR DO FRETE").FontSize(6);
                        col.Item().AlignRight().Text(dados.Totais.ValorFrete.ToString("N2")).Bold().FontSize(8);
                    });
                    row.RelativeItem().Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("VALOR DO SEGURO").FontSize(6);
                        col.Item().AlignRight().Text(dados.Totais.ValorSeguro.ToString("N2")).Bold().FontSize(8);
                    });
                    row.RelativeItem().Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("DESCONTO").FontSize(6);
                        col.Item().AlignRight().Text(dados.Totais.ValorDesconto.ToString("N2")).Bold().FontSize(8);
                    });
                    row.RelativeItem().Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("OUTRAS DESP.").FontSize(6);
                        col.Item().AlignRight().Text(dados.Totais.OutrasDespesas.ToString("N2")).Bold().FontSize(8);
                    });
                    row.RelativeItem().Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("VALOR TOTAL PRODUTOS").FontSize(6);
                        col.Item().AlignRight().Text(dados.Totais.ValorProdutos.ToString("N2")).Bold().FontSize(8);
                    });
                    row.RelativeItem().Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("VALOR TOTAL DA NOTA").FontSize(6);
                        col.Item().AlignRight().Text(dados.Totais.ValorNota.ToString("N2")).Bold().FontSize(10).FontColor(CorPrimaria);
                    });
                });
            });

            column.Item().Height(5);

            column.Item().Border(1).Column(transp =>
            {
                transp.Item().Background(CorSecundaria).Padding(3).Text("TRANSPORTADOR/VOLUMES TRANSPORTADOS").Bold().FontSize(8);
                
                transp.Item().Row(row =>
                {
                    row.RelativeItem(3).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("MODALIDADE DO FRETE").FontSize(6);
                        col.Item().Text(dados.Transporte.ModalidadeFrete switch
                        {
                            "0" => "0 - Emitente",
                            "1" => "1 - Destinatário",
                            "9" => "9 - Sem frete",
                            _ => dados.Transporte.ModalidadeFrete
                        }).Bold().FontSize(8);
                    });
                    row.RelativeItem(3).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("TRANSPORTADORA").FontSize(6);
                        col.Item().Text(dados.Transporte.NomeTransportadora ?? "Correios").Bold().FontSize(8);
                    });
                    row.RelativeItem(2).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("QTD. VOLUMES").FontSize(6);
                        col.Item().Text(dados.Transporte.QuantidadeVolumes.ToString()).Bold().FontSize(8);
                    });
                    row.RelativeItem(2).Border(1).Padding(3).Column(col =>
                    {
                        col.Item().Text("PESO BRUTO").FontSize(6);
                        col.Item().Text($"{dados.Transporte.PesoBruto:N3} kg").Bold().FontSize(8);
                    });
                });
            });

            column.Item().Height(5);

            if (!string.IsNullOrEmpty(dados.InformacoesComplementares))
            {
                column.Item().Border(1).Column(info =>
                {
                    info.Item().Background(CorPrimaria).Padding(3).Text("DADOS ADICIONAIS").Bold().FontSize(8).FontColor(Colors.White);
                    info.Item().Border(1).Padding(5).MinHeight(50).Text(dados.InformacoesComplementares).FontSize(7);
                });
            }
        });
    }

    private void ComporRodape(IContainer container, DadosNfe dados)
    {
        container.Column(column =>
        {
            column.Item().Height(3);
            column.Item().AlignCenter().Text($"DANFE gerado em {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(6).FontColor(Colors.Grey.Medium);
        });
    }

    private void GerarCodigoBarras(IContainer container, string chave)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Height(35).Row(row =>
            {
                foreach (var c in chave)
                {
                    var largura = (c - '0') % 3 + 1;
                    row.ConstantItem(largura).Background(Colors.Black);
                    row.ConstantItem(1).Background(Colors.White);
                }
            });
        });
    }

    private DadosNfe ExtrairDadosXml(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("nfe", "http://www.portalfiscal.inf.br/nfe");

        var dados = new DadosNfe();

        var ide = doc.SelectSingleNode("//*[local-name()='ide']");
        if (ide != null)
        {
            dados.Numero = int.Parse(ide.SelectSingleNode("*[local-name()='nNF']")?.InnerText ?? "0");
            dados.Serie = int.Parse(ide.SelectSingleNode("*[local-name()='serie']")?.InnerText ?? "1");
            dados.TipoOperacao = ide.SelectSingleNode("*[local-name()='tpNF']")?.InnerText ?? "1";
            dados.NaturezaOperacao = ide.SelectSingleNode("*[local-name()='natOp']")?.InnerText ?? "";
            var dhEmi = ide.SelectSingleNode("*[local-name()='dhEmi']")?.InnerText;
            dados.DataEmissao = DateTime.TryParse(dhEmi, out var dt) ? dt : DateTime.Now;
        }

        var infNFe = doc.SelectSingleNode("//*[local-name()='infNFe']");
        dados.ChaveAcesso = infNFe?.Attributes?["Id"]?.Value?.Replace("NFe", "") ?? "";

        var emit = doc.SelectSingleNode("//*[local-name()='emit']");
        if (emit != null)
        {
            dados.Emitente = new DadosEmitente
            {
                Cnpj = emit.SelectSingleNode("*[local-name()='CNPJ']")?.InnerText ?? "",
                RazaoSocial = emit.SelectSingleNode("*[local-name()='xNome']")?.InnerText ?? "",
                NomeFantasia = emit.SelectSingleNode("*[local-name()='xFant']")?.InnerText ?? "",
                InscricaoEstadual = emit.SelectSingleNode("*[local-name()='IE']")?.InnerText ?? "",
            };

            var enderEmit = emit.SelectSingleNode("*[local-name()='enderEmit']");
            if (enderEmit != null)
            {
                dados.Emitente.Logradouro = enderEmit.SelectSingleNode("*[local-name()='xLgr']")?.InnerText ?? "";
                dados.Emitente.Numero = enderEmit.SelectSingleNode("*[local-name()='nro']")?.InnerText ?? "";
                dados.Emitente.Bairro = enderEmit.SelectSingleNode("*[local-name()='xBairro']")?.InnerText ?? "";
                dados.Emitente.Cidade = enderEmit.SelectSingleNode("*[local-name()='xMun']")?.InnerText ?? "";
                dados.Emitente.UF = enderEmit.SelectSingleNode("*[local-name()='UF']")?.InnerText ?? "";
                dados.Emitente.Cep = enderEmit.SelectSingleNode("*[local-name()='CEP']")?.InnerText ?? "";
                dados.Emitente.Telefone = enderEmit.SelectSingleNode("*[local-name()='fone']")?.InnerText ?? "";
            }
        }

        var dest = doc.SelectSingleNode("//*[local-name()='dest']");
        if (dest != null)
        {
            dados.Destinatario = new DadosDestinatario
            {
                CpfCnpj = dest.SelectSingleNode("*[local-name()='CPF']")?.InnerText 
                    ?? dest.SelectSingleNode("*[local-name()='CNPJ']")?.InnerText ?? "",
                Nome = dest.SelectSingleNode("*[local-name()='xNome']")?.InnerText ?? "",
                InscricaoEstadual = dest.SelectSingleNode("*[local-name()='IE']")?.InnerText,
                Email = dest.SelectSingleNode("*[local-name()='email']")?.InnerText ?? "",
            };

            var enderDest = dest.SelectSingleNode("*[local-name()='enderDest']");
            if (enderDest != null)
            {
                dados.Destinatario.Logradouro = enderDest.SelectSingleNode("*[local-name()='xLgr']")?.InnerText ?? "";
                dados.Destinatario.Numero = enderDest.SelectSingleNode("*[local-name()='nro']")?.InnerText ?? "";
                dados.Destinatario.Bairro = enderDest.SelectSingleNode("*[local-name()='xBairro']")?.InnerText ?? "";
                dados.Destinatario.Cidade = enderDest.SelectSingleNode("*[local-name()='xMun']")?.InnerText ?? "";
                dados.Destinatario.UF = enderDest.SelectSingleNode("*[local-name()='UF']")?.InnerText ?? "";
                dados.Destinatario.Cep = enderDest.SelectSingleNode("*[local-name()='CEP']")?.InnerText ?? "";
                dados.Destinatario.Telefone = enderDest.SelectSingleNode("*[local-name()='fone']")?.InnerText ?? "";
            }
        }

        var detNodes = doc.SelectNodes("//*[local-name()='det']");
        _logger.LogInformation("[DANFE] Encontrados {Count} itens no XML", detNodes?.Count ?? 0);
        
        if (detNodes != null)
        {
            foreach (XmlNode det in detNodes)
            {
                var prod = det.SelectSingleNode("*[local-name()='prod']");
                if (prod == null)
                {
                    _logger.LogWarning("[DANFE] Node 'prod' não encontrado no item");
                    continue;
                }
                
                var imposto = det.SelectSingleNode("*[local-name()='imposto']");
                var icms = imposto?.SelectSingleNode("*[local-name()='ICMS']")?.FirstChild;

                var descricao = prod.SelectSingleNode("*[local-name()='xProd']")?.InnerText ?? "";
                
                // Filtrar texto de homologação da descrição do produto
                if (descricao.Contains("NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO"))
                {
                    descricao = "PRODUTO DE TESTE - HOMOLOGAÇÃO";
                    _logger.LogWarning("[DANFE] Descrição de homologação detectada e substituída");
                }
                
                _logger.LogInformation("[DANFE] Adicionando item: {Descricao}", descricao);

                var item = new ItemNfe
                {
                    Codigo = prod.SelectSingleNode("*[local-name()='cProd']")?.InnerText ?? "",
                    Descricao = descricao,
                    Ncm = prod.SelectSingleNode("*[local-name()='NCM']")?.InnerText ?? "",
                    Cfop = prod.SelectSingleNode("*[local-name()='CFOP']")?.InnerText ?? "",
                    Unidade = prod.SelectSingleNode("*[local-name()='uCom']")?.InnerText ?? "UN",
                    Quantidade = decimal.Parse(prod.SelectSingleNode("*[local-name()='qCom']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    ValorUnitario = decimal.Parse(prod.SelectSingleNode("*[local-name()='vUnCom']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    ValorTotal = decimal.Parse(prod.SelectSingleNode("*[local-name()='vProd']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    Cst = icms?.SelectSingleNode("*[local-name()='CSOSN']")?.InnerText 
                        ?? icms?.SelectSingleNode("*[local-name()='CST']")?.InnerText ?? "102",
                };

                dados.Itens.Add(item);
            }
        }
        
        _logger.LogInformation("[DANFE] Total de itens extraídos: {Count}", dados.Itens.Count);

        var icmsTot = doc.SelectSingleNode("//*[local-name()='ICMSTot']");
        if (icmsTot != null)
        {
            dados.Totais = new TotaisNfe
            {
                BaseIcms = decimal.Parse(icmsTot.SelectSingleNode("*[local-name()='vBC']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                ValorIcms = decimal.Parse(icmsTot.SelectSingleNode("*[local-name()='vICMS']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                ValorFrete = decimal.Parse(icmsTot.SelectSingleNode("*[local-name()='vFrete']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                ValorSeguro = decimal.Parse(icmsTot.SelectSingleNode("*[local-name()='vSeg']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                ValorDesconto = decimal.Parse(icmsTot.SelectSingleNode("*[local-name()='vDesc']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                OutrasDespesas = decimal.Parse(icmsTot.SelectSingleNode("*[local-name()='vOutro']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                ValorProdutos = decimal.Parse(icmsTot.SelectSingleNode("*[local-name()='vProd']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                ValorNota = decimal.Parse(icmsTot.SelectSingleNode("*[local-name()='vNF']")?.InnerText ?? "0", System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        var transp = doc.SelectSingleNode("//*[local-name()='transp']");
        if (transp != null)
        {
            var transportadora = transp.SelectSingleNode("*[local-name()='transporta']");
            var vol = transp.SelectSingleNode("*[local-name()='vol']");
            
            dados.Transporte = new TransporteNfe
            {
                ModalidadeFrete = transp.SelectSingleNode("*[local-name()='modFrete']")?.InnerText ?? "9",
                NomeTransportadora = transportadora?.SelectSingleNode("*[local-name()='xNome']")?.InnerText,
                QuantidadeVolumes = int.TryParse(vol?.SelectSingleNode("*[local-name()='qVol']")?.InnerText, out var qvol) ? qvol : 1,
                PesoBruto = decimal.TryParse(vol?.SelectSingleNode("*[local-name()='pesoB']")?.InnerText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var peso) ? peso : 0,
            };
        }

        var infAdic = doc.SelectSingleNode("//*[local-name()='infAdic']");
        var infCpl = infAdic?.SelectSingleNode("*[local-name()='infCpl']")?.InnerText ?? "";
        
        if (infCpl.Contains("NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO"))
        {
            infCpl = infCpl.Replace("NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL", "").Trim();
        }
        
        dados.InformacoesComplementares = infCpl;

        return dados;
    }

    #region Formatadores

    private string FormatarCnpj(string cnpj)
    {
        if (string.IsNullOrEmpty(cnpj)) return "";
        cnpj = new string(cnpj.Where(char.IsDigit).ToArray());
        if (cnpj.Length != 14) return cnpj;
        return $"{cnpj.Substring(0, 2)}.{cnpj.Substring(2, 3)}.{cnpj.Substring(5, 3)}/{cnpj.Substring(8, 4)}-{cnpj.Substring(12, 2)}";
    }

    private string FormatarCpfCnpj(string doc)
    {
        if (string.IsNullOrEmpty(doc)) return "";
        doc = new string(doc.Where(char.IsDigit).ToArray());
        if (doc.Length == 11)
            return $"{doc.Substring(0, 3)}.{doc.Substring(3, 3)}.{doc.Substring(6, 3)}-{doc.Substring(9, 2)}";
        if (doc.Length == 14)
            return FormatarCnpj(doc);
        return doc;
    }

    private string FormatarCep(string cep)
    {
        if (string.IsNullOrEmpty(cep)) return "";
        cep = new string(cep.Where(char.IsDigit).ToArray());
        if (cep.Length != 8) return cep;
        return $"{cep.Substring(0, 5)}-{cep.Substring(5, 3)}";
    }

    private string FormatarTelefone(string tel)
    {
        if (string.IsNullOrEmpty(tel)) return "";
        tel = new string(tel.Where(char.IsDigit).ToArray());
        if (tel.Length == 11)
            return $"({tel.Substring(0, 2)}) {tel.Substring(2, 5)}-{tel.Substring(7, 4)}";
        if (tel.Length == 10)
            return $"({tel.Substring(0, 2)}) {tel.Substring(2, 4)}-{tel.Substring(6, 4)}";
        return tel;
    }

    private string FormatarChaveAcesso(string chave)
    {
        if (string.IsNullOrEmpty(chave) || chave.Length != 44) return chave;
        return string.Join(" ", Enumerable.Range(0, 11).Select(i => chave.Substring(i * 4, 4)));
    }

    #endregion
}

#region Classes de Dados

public class DadosNfe
{
    public int Numero { get; set; }
    public int Serie { get; set; }
    public string ChaveAcesso { get; set; } = "";
    public string TipoOperacao { get; set; } = "1";
    public string NaturezaOperacao { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public string? Protocolo { get; set; }
    public DateTime? DataAutorizacao { get; set; }
    public string InformacoesComplementares { get; set; } = "";
    
    public DadosEmitente Emitente { get; set; } = new();
    public DadosDestinatario Destinatario { get; set; } = new();
    public List<ItemNfe> Itens { get; set; } = new();
    public TotaisNfe Totais { get; set; } = new();
    public TransporteNfe Transporte { get; set; } = new();
}

public class DadosEmitente
{
    public string Cnpj { get; set; } = "";
    public string RazaoSocial { get; set; } = "";
    public string NomeFantasia { get; set; } = "";
    public string InscricaoEstadual { get; set; } = "";
    public string Logradouro { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Bairro { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string UF { get; set; } = "";
    public string Cep { get; set; } = "";
    public string Telefone { get; set; } = "";
}

public class DadosDestinatario
{
    public string CpfCnpj { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? InscricaoEstadual { get; set; }
    public string Email { get; set; } = "";
    public string Logradouro { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Bairro { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string UF { get; set; } = "";
    public string Cep { get; set; } = "";
    public string Telefone { get; set; } = "";
}

public class ItemNfe
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Ncm { get; set; } = "";
    public string Cfop { get; set; } = "";
    public string Cst { get; set; } = "";
    public string Unidade { get; set; } = "UN";
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}

public class TotaisNfe
{
    public decimal BaseIcms { get; set; }
    public decimal ValorIcms { get; set; }
    public decimal ValorFrete { get; set; }
    public decimal ValorSeguro { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal OutrasDespesas { get; set; }
    public decimal ValorProdutos { get; set; }
    public decimal ValorNota { get; set; }
}

public class TransporteNfe
{
    public string ModalidadeFrete { get; set; } = "9";
    public int QuantidadeVolumes { get; set; } = 1;
    public string? NomeTransportadora { get; set; }
    public decimal PesoBruto { get; set; } = 0;
}

#endregion
