# Sistema de Email - Chase a Flare

## 📧 Visão Geral

Sistema completo de notificação por email para atualizações de status de pedidos, com templates HTML responsivos e identidade visual personalizada.

## ⚙️ Configuração

### 1. Configurações SMTP

As configurações de SMTP estão definidas em `appsettings.json` e `appsettings.Development.json`:

```json
"Email": {
  "SmtpHost": "smtp.seuservidor.com.br",
  "SmtpPort": 587,
  "SmtpUser": "noreply@chaseaflare.com.br",
  "SmtpPassword": "sua_senha_aqui",
  "EnableSsl": true,
  "FromEmail": "noreply@chaseaflare.com.br",
  "FromName": "Chase a Flare"
}
```

### 2. Atualizando para seu servidor

Para configurar com seu servidor SMTP, edite os seguintes campos:

- **SmtpHost**: Endereço do servidor SMTP (ex: `smtp.gmail.com`, `smtp.office365.com`)
- **SmtpPort**: Porta do servidor (587 para TLS, 465 para SSL, 25 sem criptografia)
- **SmtpUser**: Usuário/email para autenticação
- **SmtpPassword**: Senha do email
- **EnableSsl**: `true` para usar SSL/TLS, `false` caso contrário
- **FromEmail**: Email remetente
- **FromName**: Nome que aparece como remetente

### 3. Exemplos de Configuração

#### Gmail
```json
"Email": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "SmtpUser": "seu-email@gmail.com",
  "SmtpPassword": "sua-senha-de-app",
  "EnableSsl": true,
  "FromEmail": "seu-email@gmail.com",
  "FromName": "Chase a Flare"
}
```

#### Office 365
```json
"Email": {
  "SmtpHost": "smtp.office365.com",
  "SmtpPort": 587,
  "SmtpUser": "seu-email@outlook.com",
  "SmtpPassword": "sua-senha",
  "EnableSsl": true,
  "FromEmail": "seu-email@outlook.com",
  "FromName": "Chase a Flare"
}
```

## 📬 Funcionamento

### Disparo Automático

Os emails são enviados automaticamente quando o status de um pedido é atualizado. Isso acontece em dois lugares:

1. **Webhook do Pagar.me** (`PaymentsController.cs`):
   - Quando o pagamento é confirmado → Status muda para `EmSeparacao`
   - Quando o pagamento falha → Status muda para `Cancelado`

2. **Atualização Manual** (painel administrativo):
   - Qualquer mudança de status via API

### Fluxo de Email

```
Pedido Criado (AguardandoConfirmacao) → Email enviado
         ↓
Pagamento Confirmado (EmSeparacao) → Email enviado
         ↓
Pedido Enviado (ACaminho) → Email enviado com código de rastreamento
         ↓
Pedido Entregue (Finalizado) → Email enviado
```

## 🎨 Templates de Email

Cada status possui um template HTML personalizado com:

- ✅ Design responsivo para mobile e desktop
- ✅ Cores e identidade visual da Chase a Flare
- ✅ Informações completas do pedido
- ✅ Lista de itens comprados
- ✅ Código de rastreamento (quando disponível)
- ✅ Badges coloridos por status

### Status e Cores

| Status | Cor | Ícone |
|--------|-----|-------|
| Aguardando Confirmação | Amarelo (#ffc107) | ⏳ |
| Em Separação | Roxo (#667eea) | 📦 |
| A Caminho | Verde (#28a745) | 🚚 |
| Finalizado | Azul (#17a2b8) | ✅ |
| Cancelado | Vermelho (#dc3545) | ❌ |

## 🔧 Estrutura do Código

### IEmailService.cs
Interface do serviço de email com dois métodos principais:
- `EnviarEmailStatusPedidoAsync`: Envia email de atualização de pedido
- `EnviarEmailAsync`: Envia email genérico

### EmailService.cs
Implementação completa com:
- Conexão SMTP
- Geração de templates HTML
- Lógica de envio
- Tratamento de erros

### PedidoService.cs
Integração com o serviço de email:
- Detecta mudanças de status
- Dispara envio de email automaticamente

## 📝 Logs

O sistema registra logs de todas as operações de email:

```
[Email] Email enviado para cliente@email.com - Pedido: CAF-123456, Status: EmSeparacao
[Email] Erro ao enviar email para pedido #123: Connection timeout
```

## 🛠️ Personalização

### Modificar Templates

Para personalizar os templates HTML, edite o método `GerarTemplateEmailPorStatusAsync` em `EmailService.cs`.

### Adicionar Novos Eventos

Para enviar emails em outros eventos:

```csharp
await _emailService.EnviarEmailStatusPedidoAsync(pedido, novoStatus, "Observação opcional");
```

## ⚠️ Observações Importantes

1. **Não falha operações principais**: Se o envio de email falhar, a operação principal (atualização do pedido) continua normalmente.

2. **Validação de configurações**: Se as configurações SMTP estiverem vazias, o sistema apenas registra um aviso e não tenta enviar.

3. **Código de rastreamento**: O email só inclui a seção de rastreamento quando o pedido está com status `ACaminho` e possui código válido.

4. **Ambiente de desenvolvimento**: Use um servidor SMTP de testes ou configure logs para verificar emails antes de produção.

## 🧪 Testes

Para testar o envio de emails:

1. Configure um servidor SMTP válido
2. Crie um pedido de teste
3. Atualize o status do pedido via painel administrativo
4. Verifique o email na caixa de entrada do cliente

## 🔐 Segurança

- ⚠️ Nunca commite senhas reais no repositório
- ✅ Use variáveis de ambiente em produção
- ✅ Configure autenticação de dois fatores quando possível
- ✅ Use senhas de aplicativo (Gmail) ao invés da senha principal

## 📞 Suporte

Para dúvidas sobre configuração:
- Email: contato@example.com
- Documentação SMTP do seu provedor
