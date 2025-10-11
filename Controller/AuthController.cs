using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }
        
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            if (loginDto == null)
                return BadRequest("Dados de login são obrigatórios.");

            var result = await _authService.LoginAsync(loginDto, HttpContext);
            if (result == null)
                return Unauthorized("Email ou senha inválidos.");

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto registerDto)
        {
            if (registerDto == null)
                return BadRequest("Dados de registro são obrigatórios.");

            var result = await _authService.RegisterAsync(registerDto);
            if (result == null)
                return BadRequest("Email já está em uso.");

            return Ok(result);
        }

        [HttpPost("validate")]
        public async Task<ActionResult> ValidateToken([FromBody] string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Token é obrigatório.");

            var isValid = await _authService.ValidateTokenAsync(token);
            if (!isValid)
                return Unauthorized("Token inválido.");

            return Ok(new { valid = true });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // 🍪 Remover cookie de autenticação
            Response.Cookies.Delete("auth_token");
            return Ok(new { message = "Logout realizado com sucesso." });
        }
    }
}
