# 🔄 Sistema de Polling de Rastreamento dos Correios

## Visão Geral

Como a API dos Correios não oferece webhook push em tempo real, implementamos um **sistema de polling** que consulta automaticamente o status de rastreamento de pedidos e devoluções em aberto.

---

## Como Funciona

### 1. Background Service (Execução Automática)

O serviço `CorreiosPollingBackgroundService` roda em background e:

- ✅ Inicia 1 minuto após o start da aplicação
- ✅ Executa a cada **20 minutos** (configurável)
- ✅ Busca todos os pedidos com status `EmSeparacao` ou `ACaminho` que tenham código de rastreamento
- ✅ Busca todas as devoluções com status `Enviado` que tenham código de rastreamento
- ✅ Consulta a API dos Correios para cada código de rastreamento
- ✅ Processa os eventos mais recentes usando a mesma lógica do webhook
- ✅ Atualiza automaticamente os status e envia emails quando necessário

---

## Arquitetura

```
┌─────────────────────────────────────────────────────┐
│  CorreiosPollingBackgroundService                   │
│  (Executa a cada 20 minutos)                        │
└────────────────┬────────────────────────────────────┘
                 │
                 ├─► 1. Busca Pedidos/Devoluções em aberto
                 │      com código de rastreamento
                 │
                 ├─► 2. CorreiosRastreamentoService
                 │      └─► Consulta API dos Correios
                 │           GET /srorastro/v1/objetos/{codigo}
                 │
                 └─► 3. CorreiosWebhookService
                      └─► Processa eventos (mesma lógica)
                           └─► Atualiza status + Envia email
```

---

## Configuração

### appsettings.json

```json
{
  "CorreiosPolling": {
    "IntervaloMinutos": 20,
    "BaseUrl": "https://api.correios.com.br",
    "Usuario": "SEU_USUARIO",
    "Token": "seu_token_aqui",
    "Cartao": "SEU_CARTAO_POSTAGEM",
    "HabilitarPolling": true
  }
}
```

| Parâmetro | Descrição | Padrão |
|-----------|-----------|--------|
| `IntervaloMinutos` | Intervalo entre consultas | 20 minutos |
| `BaseUrl` | URL base da API dos Correios | https://api.correios.com.br |
| `Usuario` | CNPJ do contrato | - |
| `Token` | Token de autenticação | - |
| `Cartao` | Número do cartão de postagem | - |
| `HabilitarPolling` | Ativar/desativar polling | true |

---

## Serviços Implementados

### 1. CorreiosRastreamentoService

Responsável por consultar a API de rastreamento dos Correios.

**Recursos:**
- ✅ Autenticação OAuth2 com cache de token
- ✅ Consulta individual de objetos
- ✅ Consulta em lote com limite de concorrência
- ✅ Tratamento de rate limit (delay entre lotes)
- ✅ Logs detalhados de todas as operações

**Métodos:**
```csharp
Task<CorreiosRastreamentoResponse?> RastrearObjetoAsync(string codigoRastreamento);
Task<List<CorreiosRastreamentoResponse>> RastrearVariosObjetosAsync(List<string> codigosRastreamento);
```

### 2. CorreiosPollingBackgroundService

Serviço de background que executa o polling automaticamente.

**Fluxo de Execução:**
1. Busca pedidos em `EmSeparacao` ou `ACaminho` com código de rastreamento
2. Busca devoluções em `Enviado` com código de rastreamento
3. Consolida todos os códigos únicos
4. Consulta rastreamentos na API dos Correios (em lotes de 5)
5. Processa cada evento usando `CorreiosWebhookService`
6. Loga estatísticas de atualização

**Características:**
- ✅ Executa em background sem bloquear a aplicação
- ✅ Suporta graceful shutdown
- ✅ Logs detalhados de início, progresso e fim
- ✅ Tratamento robusto de exceções

---

## DTOs e Modelos

### CorreiosRastreamentoResponse

```csharp
public class CorreiosRastreamentoResponse
{
    public string CodObjeto { get; set; }
    public List<CorreiosEvento> Eventos { get; set; }
    public string TipoPostal { get; set; }
    public string Modalidade { get; set; }
}
```

### CorreiosEvento

```csharp
public class CorreiosEvento
{
    public string Codigo { get; set; }           // Ex: BDE-1, PO-1
    public string Descricao { get; set; }        // Ex: "Objeto entregue"
    public DateTime DtHrCriado { get; set; }
    public string Tipo { get; set; }
    public CorreiosUnidade? Unidade { get; set; }
    public CorreiosDestinatario? Destinatario { get; set; }
}
```

---

## Integração com Sistema Existente

O polling **reutiliza completamente** a lógica do `CorreiosWebhookService`:

- ✅ Mesmos mapeamentos de eventos → status
- ✅ Mesmos gatilhos de email
- ✅ Mesma lógica de atualização de pedidos/devoluções
- ✅ Mesmos logs e auditoria

**Benefício:** Manutenção em um único lugar!

---

## Logs e Monitoramento

### Logs Produzidos

```
[INFO] Serviço de polling dos Correios iniciado. Intervalo: 20 minutos
[INFO] Iniciando verificação de rastreamentos...
[INFO] Encontrados 5 pedidos em aberto para rastrear
[INFO] Encontradas 2 devoluções em aberto para rastrear
[INFO] Iniciando rastreamento de 7 objetos
[INFO] Rastreamento concluído: 7/7 objetos
[INFO] Rastreamento processado: SS123456789BR - Pedido #CAF-123 atualizado para FINALIZADO
[INFO] Verificação concluída: 3 pedidos e 1 devoluções atualizados
```

### Logs de Erro

```
[ERROR] Erro ao obter token dos Correios: 401
[ERROR] Erro ao rastrear objeto SS123456789BR: 500
[ERROR] Exceção ao obter token dos Correios
```

---

## Desempenho e Otimizações

### Consultas em Lote
- Processa objetos em **lotes de 5** simultaneamente
- Delay de **1 segundo** entre lotes para respeitar rate limit
- Evita sobrecarga na API dos Correios

### Cache de Token
- Token OAuth2 é cacheado em memória
- Reutilizado até expiração
- Reduz chamadas desnecessárias à API de autenticação

### Seletividade
- Apenas pedidos/devoluções **em aberto** são consultados
- Pedidos finalizados/cancelados são ignorados
- Devoluções reembolsadas/rejeitadas são ignoradas

---

## Teste Manual

### 1. Verificar se o serviço está rodando

Olhe nos logs ao iniciar a aplicação:

```
[INFO] Serviço de polling dos Correios iniciado. Intervalo: 20 minutos
```

### 2. Forçar execução (para desenvolvimento)

Altere temporariamente o intervalo para 1 minuto:

```json
{
  "CorreiosPolling": {
    "IntervaloMinutos": 1
  }
}
```

### 3. Desabilitar polling

```json
{
  "CorreiosPolling": {
    "HabilitarPolling": false
  }
}
```

---

## Mapeamento de Eventos (Mesmo do Webhook)

O polling usa exatamente os mesmos mapeamentos documentados em [WEBHOOK_CORREIOS.md](WEBHOOK_CORREIOS.md):

### Pedidos
- `BDE-1, BDE-67, BDE-68, BDE-70` → **Finalizado** + Email
- `PO-1, OEC-1, RO-1` → **A Caminho**
- `BDE-2 a BDE-10` → Problemas (apenas log)
- `BDE-28, BDE-50, BDE-80` → Eventos críticos

### Devoluções
- `PO-1, CO-1, CO-8` → **Enviado** + Email
- `BDE-14, BDE-23, BDR-79` → **Em Análise** + Email

---

## Comparação: Webhook vs Polling

| Aspecto | Webhook (Push) | Polling (Pull) |
|---------|----------------|----------------|
| **Tempo Real** | ✅ Instantâneo | ⚠️ Atraso de até 20 min |
| **Carga no Servidor** | ✅ Baixa | ⚠️ Média (consultas periódicas) |
| **Confiabilidade** | ⚠️ Depende dos Correios | ✅ Controlado por nós |
| **Complexidade** | ✅ Simples (apenas recebe) | ⚠️ Média (consulta + processa) |
| **Disponibilidade** | ❌ Não disponível nos Correios | ✅ Disponível |

---

## Troubleshooting

### Polling não está executando

1. Verifique se `HabilitarPolling: true` no appsettings.json
2. Verifique os logs de inicialização
3. Confirme que há pedidos/devoluções em aberto com código de rastreamento

### Erro 401 ao consultar API

- Verifique `Usuario`, `Token` e `Cartao` no appsettings.json
- Confirme que as credenciais estão válidas no portal dos Correios

### Nenhum objeto sendo atualizado

- Verifique se os códigos de rastreamento estão corretos no banco
- Confirme que os pedidos estão com status `EmSeparacao` ou `ACaminho`
- Confirme que as devoluções estão com status `Enviado`
- Verifique os logs para identificar erros específicos

---

## Próximos Passos

1. ✅ Sistema implementado e configurado
2. ⏳ Execute a aplicação e verifique os logs
3. ⏳ Aguarde 1 minuto para a primeira execução do polling
4. ⏳ Monitore os logs para confirmar atualizações
5. ⏳ Ajuste o intervalo conforme necessário (recomendado: 15-30 minutos)

---

## Observações Importantes

- ⚠️ O polling consulta a API dos Correios, que pode ter rate limits
- ⚠️ Não configure intervalos muito curtos (< 10 minutos) para evitar bloqueio
- ⚠️ Certifique-se de que as credenciais dos Correios estão corretas
- ✅ O serviço inicia automaticamente com a aplicação
- ✅ Não requer configuração adicional no portal dos Correios
