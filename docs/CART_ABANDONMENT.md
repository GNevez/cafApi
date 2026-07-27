# Sistema de Recuperação de Carrinho Abandonado

## Visão Geral

O sistema de recuperação de carrinho abandonado envia automaticamente emails para clientes que iniciaram uma compra mas não finalizaram. O objetivo é recuperar vendas perdidas através de lembretes personalizados.

## Funcionamento

### 1. Associação de Cliente ao Carrinho

Quando o usuário preenche o email no checkout, o sistema automaticamente:
- Cria um registro de cliente (se não existir)
- Associa o cliente ao carrinho atual

Isso é feito através do endpoint `POST /api/cart/associate-client` que é chamado no `onBlur` do campo de email.

### 2. Detecção de Carrinho Abandonado

Um carrinho é considerado abandonado quando:
- Tem um cliente associado (`ClienteId` não é nulo)
- Status é `Ativo` ou `Abandonado`
- Possui itens
- Não teve atividade por mais de 1 hora (configurável)
- Não excedeu o limite de emails de recuperação

### 3. Envio de Emails de Recuperação

O `CartAbandonmentBackgroundService` roda a cada 6 horas (configurável) e:
1. Busca carrinhos abandonados elegíveis para email
2. Envia email de recuperação personalizado
3. Incrementa o contador de emails enviados
4. Atualiza o status para `Abandonado` após o primeiro email

### Fluxo de Emails

| Email # | Assunto | Mensagem |
|---------|---------|----------|
| 1º | 🛒 Ei, você esqueceu algo especial no carrinho! | Mensagem amigável lembrando dos produtos |
| 2º | ⏰ Seus itens ainda estão esperando por você! | Reforça que os produtos estão guardados |
| 3º | 🔥 Última chance! Seu carrinho está quase expirando | Urgência para finalizar a compra |

## Configuração

### appsettings.json

```json
{
  "CartAbandonment": {
    "Habilitado": true,
    "IntervaloHoras": 6,
    "HorasParaAbandono": 1,
    "MaxEmailsPorCarrinho": 3,
    "IntervaloEntreEmailsHoras": 6
  }
}
```

### Parâmetros

| Parâmetro | Descrição | Padrão |
|-----------|-----------|--------|
| `Habilitado` | Se o serviço está ativo | `true` |
| `IntervaloHoras` | Intervalo entre verificações (horas) | `6` |
| `HorasParaAbandono` | Tempo sem atividade para considerar abandonado | `1` |
| `MaxEmailsPorCarrinho` | Máximo de emails por carrinho | `3` |
| `IntervaloEntreEmailsHoras` | Mínimo entre emails do mesmo carrinho | `6` |

## Campos do Modelo Carrinho

```csharp
// Controle de recuperação de carrinho abandonado
public DateTime? EmailRecuperacaoEnviadoEm { get; set; }
public int EmailRecuperacaoCount { get; set; } = 0;
```

## Endpoints

### POST /api/cart/associate-client

Associa um cliente ao carrinho. Cria o cliente se não existir.

**Request:**
```json
{
  "email": "cliente@exemplo.com",
  "nome": "Nome do Cliente" // opcional
}
```

**Response:**
```json
{
  "token": "...",
  "itens": [...],
  "subtotal": 199.90
}
```

## Serviços Envolvidos

### ICartService

- `AssociateClientAsync(string? cartToken, string email, string? nome)` - Associa cliente
- `MarkCartAsAbandonedAsync(int carrinhoId)` - Marca como abandonado
- `GetAbandonedCartsForEmailAsync()` - Lista carrinhos para email
- `MarkAbandonmentEmailSentAsync(int carrinhoId)` - Marca email enviado

### IEmailService

- `EnviarEmailCarrinhoAbandonadoAsync(Carrinho carrinho)` - Envia email de recuperação

### CartAbandonmentBackgroundService

Background service que executa periodicamente a verificação e envio de emails.

## Migration SQL

Execute o script em `docs/migrations/add_cart_abandonment_fields.sql` para adicionar os campos necessários:

```sql
ALTER TABLE Carrinhos ADD COLUMN EmailRecuperacaoEnviadoEm DATETIME NULL;
ALTER TABLE Carrinhos ADD COLUMN EmailRecuperacaoCount INT NOT NULL DEFAULT 0;
```

## Monitoramento

Os logs do serviço usam o prefixo `[CartAbandonment]`:

```
[CartAbandonment] Serviço iniciado. Intervalo: 6h
[CartAbandonment] Encontrados 5 carrinhos abandonados para processar
[CartAbandonment] Email #1 enviado para carrinho #123 (Cliente: email@teste.com)
[CartAbandonment] Processamento concluído. Enviados: 5, Erros: 0
```

## Desabilitando o Serviço

Para desabilitar temporariamente, defina no `appsettings.json`:

```json
{
  "CartAbandonment": {
    "Habilitado": false
  }
}
```
