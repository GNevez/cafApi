using System.Net;

namespace cafApi.Middleware
{
    public class AccessDeniedMiddleware
    {
        private readonly RequestDelegate _next;

        public AccessDeniedMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            // Se a resposta é 403 (Forbidden), customizar a mensagem
            if (context.Response.StatusCode == (int)HttpStatusCode.Forbidden)
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    message = "Acesso negado. Você não tem permissão para realizar esta operação.",
                    statusCode = 403
                }));
            }
        }
    }
}
