# Cliente de Impressão - Chase a Flare

Este é um cliente de impressão que roda em um computador local com impressora conectada. Ele monitora a fila de impressão no servidor e automaticamente imprime os rótulos dos Correios.

## Arquitetura

```
┌─────────────────────────┐     ┌──────────────────────────────────┐
│   VPS/Servidor (API)    │     │   Computador Local (Impressora)  │
│                         │     │                                  │
│  ┌─────────────────┐   │     │  ┌────────────────────────────┐  │
│  │ PaymentsWebhook │   │     │  │   ClienteImpressaoCAF.exe  │  │
│  │       ↓         │   │     │  │                            │  │
│  │ GeraPrePostagem │   │     │  │  1. Consulta fila pendente │  │
│  │       ↓         │   │     │  │  2. Reserva o job          │  │
│  │ GeraRotulo      │   │     │  │  3. Baixa o PDF            │  │
│  │       ↓         │   │     │  │  4. Envia para impressora  │  │
│  │ FilaImpressao   │◄──┼─────┼──┤  5. Confirma sucesso       │  │
│  └─────────────────┘   │     │  └────────────────────────────┘  │
│                         │     │              ↓                   │
│                         │     │  ┌────────────────────────────┐  │
│                         │     │  │     Impressora USB/Rede    │  │
│                         │     │  └────────────────────────────┘  │
└─────────────────────────┘     └──────────────────────────────────┘
```

## Requisitos

1. **.NET 8.0 Runtime** (se usar versão não self-contained)
2. **SumatraPDF** (recomendado) ou Adobe Reader para impressão PDF
3. Conexão com internet para acessar o servidor

### Instalar SumatraPDF

O SumatraPDF é recomendado por ser leve e ter bom suporte a impressão por linha de comando:

```
winget install SumatraPDF.SumatraPDF
```

Ou baixe em: https://www.sumatrapdfreader.org/download-free-pdf-viewer

## Configuração

### Opção 1: Variáveis de Ambiente

Configure as seguintes variáveis de ambiente no Windows:

| Variável | Descrição | Exemplo |
|----------|-----------|---------|
| `CAF_SERVER_URL` | URL base do servidor | `https://api.chaseaflare.com` |
| `CAF_CLIENTE_ID` | ID único deste cliente | `impressora-loja-01` |
| `CAF_INTERVALO` | Intervalo entre consultas (segundos) | `10` |
| `CAF_IMPRESSORA` | Nome da impressora (vazio = padrão) | `HP LaserJet Pro` |

### Opção 2: Editar o arquivo IniciarCliente.bat

Abra o arquivo `IniciarCliente.bat` e edite as variáveis no início do arquivo.

## Compilação

Para compilar o cliente em um executável único:

```bash
cd PrintClient
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

O executável será gerado em `bin/Release/net8.0/win-x64/publish/ClienteImpressaoCAF.exe`

## Execução

### Opção 1: Via arquivo .bat

1. Edite o arquivo `IniciarCliente.bat` com suas configurações
2. Execute o arquivo `IniciarCliente.bat`

### Opção 2: Via linha de comando

```cmd
set CAF_SERVER_URL=https://api.chaseaflare.com
set CAF_CLIENTE_ID=impressora-01
ClienteImpressaoCAF.exe
```

### Opção 3: Via PowerShell

```powershell
$env:CAF_SERVER_URL = "https://api.chaseaflare.com"
$env:CAF_CLIENTE_ID = "impressora-01"
.\ClienteImpressaoCAF.exe
```

## Executar como Serviço Windows

Para rodar automaticamente quando o Windows iniciar:

### Opção 1: Agendador de Tarefas

1. Abra o Agendador de Tarefas do Windows
2. Crie uma nova Tarefa Básica
3. Configure para iniciar na inicialização do Windows
4. Aponte para o `IniciarCliente.bat` ou diretamente para o `.exe`

### Opção 2: Criar Serviço Windows (avançado)

Use o NSSM (Non-Sucking Service Manager):

```cmd
nssm install CAFImpressao "C:\CAF\ClienteImpressaoCAF.exe"
nssm set CAFImpressao AppEnvironmentExtra "CAF_SERVER_URL=https://api.chaseaflare.com" "CAF_CLIENTE_ID=impressora-01"
nssm start CAFImpressao
```

## Endpoints da API

O cliente utiliza os seguintes endpoints:

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| GET | `/api/FilaImpressao/pendentes` | Lista jobs pendentes |
| POST | `/api/FilaImpressao/{id}/reservar` | Reserva um job |
| GET | `/api/FilaImpressao/{id}/download` | Baixa o PDF |
| POST | `/api/FilaImpressao/{id}/confirmar` | Confirma impressão |
| POST | `/api/FilaImpressao/{id}/erro` | Reporta erro |

## Fluxo de Impressão

1. **Polling**: O cliente consulta `/pendentes` a cada X segundos
2. **Reserva**: Ao encontrar job, chama `/reservar` para "travar" o job
3. **Download**: Baixa o PDF via `/download`
4. **Impressão**: Envia para impressora (SumatraPDF → Adobe → Windows Shell)
5. **Confirmação**: Se sucesso, chama `/confirmar`. Se erro, chama `/erro`

## Troubleshooting

### Erro: "Impressora não encontrada"

1. Verifique o nome exato da impressora no Painel de Controle
2. O nome é case-sensitive
3. Deixe `CAF_IMPRESSORA` vazio para usar a impressora padrão

### Erro: "Falha ao baixar PDF"

1. Verifique a URL do servidor
2. Verifique se o servidor está online
3. Verifique se o rótulo existe no servidor

### PDF não imprime

1. Instale o SumatraPDF em `C:\Program Files\SumatraPDF\`
2. Teste manualmente: `SumatraPDF.exe -print-to-default arquivo.pdf`
3. Verifique as permissões de impressão

### Cliente fecha sozinho

1. Execute via `IniciarCliente.bat` para ver mensagens de erro
2. Verifique se o .NET 8 Runtime está instalado
3. Verifique logs de erro no Windows Event Viewer

## Logs

O cliente exibe logs no console com timestamp:

```
[14:30:25] Encontrados 2 job(s) pendente(s)
Processando Job #123 - rotulo_ABC123.pdf
  Reservando...
  Baixando PDF...
  Enviando para impressora...
  [OK] Impressão enviada com sucesso!
```

## Suporte

Para problemas ou dúvidas, contate o suporte técnico.
