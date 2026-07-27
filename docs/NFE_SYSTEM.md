# Sistema de NF-e (Nota Fiscal Eletrônica)

Este documento descreve a implementação do sistema de emissão de NF-e da Chase a Flare.

## ⚠️ IMPORTANTE: MEI e NF-e

**MEI (Microempreendedor Individual):**
- ✅ **PODE** emitir NF-e se quiser
- ✅ É **OBRIGADO** a emitir quando vende para PJ (CNPJ)
- ❌ **NÃO é obrigado** quando vende para pessoa física (CPF)
- 📦 **RECOMENDADO** emitir para envios pelos Correios (evita fiscalização)

**ME (Microempresa) no Simples Nacional:**
- ✅ Pode e geralmente é obrigada a emitir NF-e

## 📋 Visão Geral

O sistema permite:
- ✅ Emissão de NF-e (Modelo 55) para vendas
- ✅ Cancelamento de NF-e autorizadas
- ✅ Consulta de NF-e na SEFAZ
- ✅ Inutilização de numeração
- ✅ Geração de DANFE (PDF)
- ✅ Armazenamento seguro do certificado digital

## 🔐 Segurança do Certificado Digital

### Opção 1: Variáveis de Ambiente (RECOMENDADO para Produção)

```bash
# No servidor (VPS)
export NFE_CERT_PATH=/caminho/seguro/certificado.pfx
export NFE_CERT_PASSWORD=senha_do_certificado
```

No Windows:
```powershell
[Environment]::SetEnvironmentVariable("NFE_CERT_PATH", "C:\certificados\certificado.pfx", "Machine")
[Environment]::SetEnvironmentVariable("NFE_CERT_PASSWORD", "senha_do_certificado", "Machine")
```

### Opção 2: Senha Criptografada no appsettings

1. Use o endpoint `/api/nfe/util/criptografar-senha` para gerar a senha criptografada:

```bash
POST /api/nfe/util/criptografar-senha
{
    "senha": "sua_senha_do_certificado"
}
```

2. Resposta:
```json
{
    "senhaCriptografada": "abc123...",
    "chave": "xyz789...",
    "configuracao": {
        "appsettings": "Adicione 'SenhaCriptografada' na seção NFe:Certificado",
        "variavelAmbiente": "Defina NFE_ENCRYPTION_KEY com o valor da chave"
    }
}
```

3. Configure o `appsettings.json`:
```json
{
    "NFe": {
        "Certificado": {
            "CaminhoArquivo": "credentials/certificado.pfx",
            "SenhaCriptografada": "abc123..."
        }
    }
}
```

4. Defina a variável de ambiente com a chave:
```bash
export NFE_ENCRYPTION_KEY=xyz789...
```

## ⚙️ Configuração

### appsettings.json

```json
{
    "NFe": {
        "Ambiente": 2,  // 1 = Produção, 2 = Homologação
        "UfEmitente": 53,  // Código IBGE do DF
        "Serie": 1,
        "Modelo": 55,  // NF-e (65 = NFC-e)
        "TipoEmissao": 1,  // Normal
        "Finalidade": 1,  // Normal
        "IndicadorPresenca": 2,  // Não presencial, internet
        "CstIcms": "00",
        "Csosn": "102",  // Simples Nacional sem permissão de crédito
        "CstPis": "99",  // Outras operações
        "CstCofins": "99",  // Outras operações
        "NcmPadrao": "90041000",  // Óculos de sol
        "CfopDentroEstado": "5102",  // Venda dentro do estado
        "CfopForaEstado": "6102",  // Venda fora do estado
        "Emitente": {
            "Cnpj": "REMOVED_CNPJ",
            "InscricaoEstadual": "0123456789012",
            "RazaoSocial": "CHASE A FLARE COMERCIO DE ACESSORIOS LTDA",
            "NomeFantasia": "Chase a Flare",
            "RegimeTributario": 1,  // 1 = Simples Nacional
            "CnaePrincipal": "4789099",
            "Logradouro": "REMOVED_STREET",
            "Numero": "76",
            "Complemento": "",
            "Bairro": "REMOVED_DISTRICT",
            "CodigoMunicipio": "5300108",  // Código IBGE de Brasília
            "NomeMunicipio": "Brasília",
            "UF": "DF",
            "Cep": "REMOVED_POSTAL_CODE",
            "CodigoPais": "1058",
            "NomePais": "Brasil",
            "Telefone": "REMOVED_PHONE"
        },
        "Certificado": {
            "CaminhoArquivo": "credentials/certificado.pfx",
            "Senha": ""
        }
    }
}
```

### Códigos UF (IBGE)

| UF | Código |
|----|--------|
| AC | 12 |
| AL | 27 |
| AP | 16 |
| AM | 13 |
| BA | 29 |
| CE | 23 |
| DF | 53 |
| ES | 32 |
| GO | 52 |
| MA | 21 |
| MT | 51 |
| MS | 50 |
| MG | 31 |
| PA | 15 |
| PB | 25 |
| PR | 41 |
| PE | 26 |
| PI | 22 |
| RJ | 33 |
| RN | 24 |
| RS | 43 |
| RO | 11 |
| RR | 14 |
| SC | 42 |
| SP | 35 |
| SE | 28 |
| TO | 17 |

## 🚀 Endpoints da API

### Verificar Status
```http
GET /api/nfe/status
```

### Emitir NF-e
```http
POST /api/nfe/emitir
Content-Type: application/json

{
    "pedidoId": 123,
    "naturezaOperacao": "Venda de mercadoria adquirida ou recebida de terceiros"
}
```

### Consultar NF-e
```http
GET /api/nfe/consultar/{chaveAcesso}
```

### Cancelar NF-e
```http
POST /api/nfe/cancelar
Content-Type: application/json

{
    "notaFiscalId": 1,
    "motivo": "Erro na emissão - dados do cliente incorretos"
}
```

### Inutilizar Numeração
```http
POST /api/nfe/inutilizar
Content-Type: application/json

{
    "serie": 1,
    "numeroInicial": 1,
    "numeroFinal": 10,
    "justificativa": "Numeração não utilizada devido a erro de sistema"
}
```

### Listar NF-e por Pedido
```http
GET /api/nfe/pedido/{pedidoId}
```

### Download do XML
```http
GET /api/nfe/{notaFiscalId}/xml
```

### Gerar DANFE
```http
GET /api/nfe/{notaFiscalId}/danfe
```

## 🔧 Ambiente de Homologação

Para testes, use `"Ambiente": 2` no appsettings. Neste modo:

1. O XML é gerado com dados de teste
2. O nome do emitente e destinatário são substituídos por texto padrão
3. O envio à SEFAZ é **simulado** (não real)
4. A NF-e é marcada como autorizada para testes

⚠️ **IMPORTANTE**: O ambiente de homologação é apenas para testes! As NF-e geradas não têm valor fiscal.

## � Como Testar em Produção

### Passo 1: Testar em Homologação
```json
{
    "NFe": {
        "Ambiente": 2,  // Homologação (simulado)
        ...
    }
}
```
- Faça um pedido de teste
- Verifique se a NF-e foi gerada
- Confirme se o email foi enviado com o link

### Passo 2: Testar com SEFAZ Real (Homologação)
Para testar com a SEFAZ de verdade (sem valor fiscal):
1. Instale Zeus.Net.NFe.NFCe
2. Configure `"Ambiente": 2` (homologação da SEFAZ)
3. A NF-e será enviada para o ambiente de teste da SEFAZ
4. Você pode consultar em: https://www.nfe.fazenda.gov.br/portal/consulta.aspx

### Passo 3: Produção Real
```json
{
    "NFe": {
        "Ambiente": 1,  // PRODUÇÃO - Cuidado!
        ...
    }
}
```
⚠️ **NF-e em produção tem valor fiscal e legal!**

## �📦 Dependências

Para comunicação real com a SEFAZ em produção, instale o Zeus.Net.NFe:

```bash
dotnet add package Zeus.Net.NFe.NFCe
```

## 🔄 Migração do Banco de Dados

Após adicionar o modelo NotaFiscal, execute a migration:

```bash
cd cafApi
dotnet ef migrations add AddNotaFiscal
dotnet ef database update
```

## 📝 Fluxo de Emissão

1. **Pedido é pago** → Webhook Pagar.me recebe `order.paid`
2. **Sistema atualiza status** → Pedido muda para "EmSeparacao"
3. **NF-e é emitida automaticamente** → XML gerado e assinado
4. **Email enviado** → Cliente recebe confirmação com link da NF-e
5. **DANFE disponível** → Cliente pode baixar o XML

## 🔄 Emissão Automática

A NF-e é emitida **automaticamente** quando o pagamento é aprovado:

```
Webhook Pagar.me (order.paid)
    ↓
Atualiza status para EmSeparacao
    ↓
Gera pré-postagem Correios
    ↓
Emite NF-e automaticamente ← NOVO!
    ↓
Envia email com link da NF-e ← NOVO!
```

O cliente recebe no email:
- Número da NF-e
- Série
- Chave de acesso (44 dígitos)
- Botão para baixar o XML

## 🆘 Troubleshooting

### Erro: "Certificado não encontrado"
- Verifique o caminho do arquivo .pfx
- Confirme permissões de leitura
- Verifique a variável NFE_CERT_PATH

### Erro: "Senha do certificado inválida"
- Verifique a senha configurada
- Use o utilitário de criptografia se necessário
- Confirme a variável NFE_CERT_PASSWORD

### Erro: "Schema inválido"
- Verifique os dados do emitente
- Confirme o código do município (IBGE)
- Valide o NCM dos produtos

### Erro: "Rejeição 539 - Duplicidade de NF-e"
- A chave de acesso já foi utilizada
- Verifique a numeração no banco de dados
