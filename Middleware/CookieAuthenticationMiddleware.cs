using Microsoft.AspNetCore.Authorization; // Necessário para IAllowAnonymous

namespace cafApi.Middleware
{
    public class CookieAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;

        public CookieAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();

            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await _next(context);
                return;
            }

            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader))
            {
                var cookieToken = context.Request.Cookies["auth_token"]; 
                
                if (!string.IsNullOrEmpty(cookieToken))
                {
                    context.Request.Headers["Authorization"] = $"Bearer {cookieToken}";
                    Console.WriteLine("🍪 Cookie token encontrado e convertido para header.");
                }
                else
                {
                    Console.WriteLine("❌ Nenhum token encontrado para rota protegida.");
                }
            }
            else
            {
                Console.WriteLine("✅ Token já presente no header da requisição.");
            }

            await _next(context);
        }
    }
}