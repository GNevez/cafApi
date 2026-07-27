# 📦 Webhook dos Correios - Documentação

## Visão Geral

O sistema Chase a Flare possui integração com o webhook dos Correios para atualização automática de status de **Pedidos** e **Devoluções** baseado nos eventos de rastreamento.

---

## Endpoints

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/api/CorreiosWebhook` | Receber evento único |
| `POST` | `/api/CorreiosWebhook/batch` | Receber múltiplos eventos |
| `POST` | `/api/CorreiosWebhook/test` | Testar evento manualmente |
| `GET` | `/api/CorreiosWebhook` | Verificar status do webhook |
| `GET` | `/api/CorreiosWebhook/eventos` | Listar eventos suportados |

---

## Payload do Webhook

```json
{
  "codigoObjeto": "SS123456789BR",
  "tipoEvento": "BDE-1",
  "descricaoEvento": "Objeto entregue ao destinatário",
  "dataEvento": "2024-12-19T14:30:00",
  "unidade": "CDD BELO HORIZONTE",
  "cidade": "Belo Horizonte",
  "uf": "MG"
}
```

---

## 📦 Mapeamento de Eventos - PEDIDOS

### ✅ Objeto Entregue → Status: **FINALIZADO**

| Evento | Descrição | Ação |
|--------|-----------|------|
| `BDE-1` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `BDE-67` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `BDE-68` | Objeto entregue na Caixa de Correios Inteligente | Pedido → Finalizado + Email |
| `BDE-70` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `BDE-77` | Objeto disponível em locker | Pedido → Finalizado + Email |
| `BDI-1` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `BDI-67` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `BDI-68` | Objeto entregue na Caixa de Correios Inteligente | Pedido → Finalizado + Email |
| `BDI-70` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `BDR-1` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `BDR-67` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `BDR-68` | Objeto entregue na Caixa de Correios Inteligente | Pedido → Finalizado + Email |
| `BDR-70` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |
| `CO-8` | Objeto entregue ao destinatário | Pedido → Finalizado + Email |

### 🚚 Em Trânsito → Status: **A CAMINHO**

| Evento | Descrição | Ação |
|--------|-----------|------|
| `PO-1` | Objeto postado | Pedido → A Caminho + Email |
| `PO-9` | Objeto postado após o horário limite | Pedido → A Caminho + Email |
| `OEC-1` | Objeto saiu para entrega ao destinatário | Pedido → A Caminho |
| `OEC-3` | Objeto está em rota de entrega | Pedido → A Caminho |
| `RO-0` | Objeto em trânsito | Pedido → A Caminho |
| `RO-1` | Objeto em trânsito | Pedido → A Caminho |
| `DO-1` | Objeto em trânsito | Pedido → A Caminho |
| `DO-2` | Objeto em trânsito | Pedido → A Caminho |
| `TRI-0` | Objeto encaminhado | Pedido → A Caminho |
| `PMT-1` | Objeto encaminhado | Pedido → A Caminho |
| `CAR-5` | Objeto em trânsito | Pedido → A Caminho |
| `BDE-15` | Recebido na unidade de distribuição | Pedido → A Caminho |
| `BDE-45` | Objeto recebido na unidade de distribuição | Pedido → A Caminho |

### ⚠️ Problemas de Entrega (Apenas notificação, sem mudança de status)

| Evento | Descrição | Ação |
|--------|-----------|------|
| `BDE-2` | Objeto não entregue - carteiro não atendido | Log de problema |
| `BDE-4` | Cliente recusou-se a receber o objeto | Log de problema |
| `BDE-6` | Cliente desconhecido no local | Log de problema |
| `BDE-7` | Endereço incorreto | Log de problema |
| `BDE-8` | Endereço incorreto | Log de problema |
| `BDE-10` | Cliente mudou-se | Log de problema |
| `BDE-18` | Carteiro não atendido | Log de problema |
| `BDE-19` | Endereço incorreto | Log de problema |
| `BDE-20` | Carteiro não atendido | Log de problema |
| `BDE-21` | Carteiro não atendido | Log de problema |
| `BDE-25` | Empresa sem expediente | Log de problema |
| `BDE-34` | Endereço não encontrado | Log de problema |
| `BDE-46` | Tentativa de entrega não efetuada | Log de problema |
| `BDE-47` | Saída para entrega cancelada | Log de problema |

### 🚨 Eventos Críticos (Alerta registrado no pedido)

| Evento | Descrição | Ação |
|--------|-----------|------|
| `BDE-28` | Objeto e/ou conteúdo avariado | Alerta crítico |
| `BDE-37` | Objeto avariado por acidente com veículo | Alerta crítico |
| `BDE-50` | Objeto roubado dos Correios | Alerta crítico |
| `BDE-51` | Objeto roubado dos Correios | Alerta crítico |
| `BDE-52` | Objeto roubado dos Correios | Alerta crítico |
| `BDE-80` | Objeto não localizado no fluxo postal | Alerta crítico |
| `BDE-86` | Objeto com conteúdo avariado | Alerta crítico |
| `BDE-98` | Objeto não localizado no fluxo postal | Alerta crítico |

### 🔄 Objeto em Devolução (Problema no envio)

| Evento | Descrição | Ação |
|--------|-----------|------|
| `BDE-5` | Objeto em devolução | Alerta registrado |
| `BDE-22` | Objeto devolvido aos Correios | Alerta registrado |
| `BDE-33` | Objeto em devolução | Alerta registrado |
| `BDE-49` | Objeto em devolução | Alerta registrado |

---

## 📦↩️ Mapeamento de Eventos - DEVOLUÇÕES (Logística Reversa)

### 📮 Cliente Postou o Produto → Status: **ENVIADO**

| Evento | Descrição | Ação |
|--------|-----------|------|
| `PO-1` | Objeto postado | Devolução → Enviado + Email |
| `CO-1` | Objeto coletado | Devolução → Enviado + Email |
| `CO-8` | Objeto entregue (coleta) | Devolução → Enviado + Email |
| `CO-15` | Objeto coletado após horário limite | Devolução → Enviado + Email |
| `CO-16` | Objeto coletado aguardando conferência | Devolução → Enviado + Email |

### 🚚 Em Trânsito para a Loja (Mantém status Enviado)

| Evento | Descrição | Ação |
|--------|-----------|------|
| `RO-1` | Objeto em trânsito | Log de trânsito |
| `DO-1` | Objeto em trânsito | Log de trânsito |
| `DO-2` | Objeto em trânsito | Log de trânsito |
| `TRI-0` | Objeto encaminhado | Log de trânsito |
| `OEC-9` | Objeto saiu para entrega ao remetente (loja) | Log de trânsito |
| `LDE-0` | Objeto saiu para entrega ao remetente | Log de trânsito |

### ✅ Produto Chegou na Loja → Status: **EM ANÁLISE**

| Evento | Descrição | Ação |
|--------|-----------|------|
| `BDE-14` | Objeto entregue ao remetente | Devolução → Em Análise + Email |
| `BDE-23` | Objeto entregue ao remetente | Devolução → Em Análise + Email |
| `BDI-14` | Objeto entregue ao remetente | Devolução → Em Análise + Email |
| `BDI-23` | Objeto entregue ao remetente | Devolução → Em Análise + Email |
| `BDR-14` | Objeto entregue ao remetente | Devolução → Em Análise + Email |
| `BDR-23` | Objeto entregue ao remetente | Devolução → Em Análise + Email |
| `BDR-79` | Objeto entregue ao contratante | Devolução → Em Análise + Email |

---

## Fluxo de Status

### Pedidos
```
AguardandoConfirmacao → EmSeparacao → ACaminho → Finalizado
                                         ↓
                                      Cancelado
```

### Devoluções
```
Solicitado → SolicitacaoEnviada → Enviado → EmAnalise → ReembolsoEmitido → Reembolsado
                                                 ↓
                                              Rejeitado
```

---

## Exemplo de Integração

### Testar manualmente um evento de entrega:

```bash
curl -X POST http://localhost:5006/api/CorreiosWebhook/test \
  -H "Content-Type: application/json" \
  -d '{
    "codigoObjeto": "SS123456789BR",
    "tipoEvento": "BDE-1",
    "descricaoEvento": "Objeto entregue ao destinatário",
    "dataEvento": "2024-12-19T14:30:00"
  }'
```

### Resposta esperada:

```json
{
  "sucesso": true,
  "codigoObjeto": "SS123456789BR",
  "tipoEvento": "BDE-1",
  "acaoTomada": "Pedido #CAF-123456 atualizado para FINALIZADO (Entregue)",
  "mensagem": "Evento processado com sucesso para Pedido"
}
```

---

## Configuração no Portal dos Correios

1. Acesse o portal Meu Correios
2. Vá em **Webhooks > Configurar**
3. Adicione a URL: `https://seudominio.com/api/CorreiosWebhook`
4. Selecione os eventos desejados
5. Salve e ative o webhook

---

## Observações

- Todos os eventos são logados para auditoria
- Eventos críticos (avariado, roubado) são registrados nas observações do pedido
- Emails são enviados automaticamente nas mudanças de status relevantes
- O webhook retorna 200 OK mesmo em caso de erro para evitar retentativas infinitas
