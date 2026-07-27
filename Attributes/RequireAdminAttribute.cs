using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace cafApi.Attributes
{
    public class RequireAdminAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var allowAnonymous = context.ActionDescriptor.EndpointMetadata
                .Any(m => m is IAllowAnonymous);

            if (allowAnonymous)
            {
                return;
            }

            var user = context.HttpContext.User;
            
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedObjectResult(new { 
                    message = "Acesso negado. Faça login para continuar." 
                });
                return;
            }

            var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
            
            if (roleClaim != "Administrador")
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
