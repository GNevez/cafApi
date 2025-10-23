using cafApi.Attributes;
using cafApi.Models;
using cafApi.Models.DTOs;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;

        public UsuarioController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
        }

        [HttpGet]
        [RequireAdmin]
        public async Task<ActionResult<IEnumerable<UsuarioResponseDto>>> GetAllActiveUsers()
        {
            var usuarios = await _usuarioService.GetAllActiveUsersAsync();
            return Ok(usuarios);
        }

        [HttpGet("inactive")]
        [RequireAdmin]
        public async Task<ActionResult<IEnumerable<UsuarioResponseDto>>> GetAllInactiveUsers()
        {
            var usuarios = await _usuarioService.GetAllInactiveUsersAsync();
            return Ok(usuarios);
        }

        [HttpGet("{id}")]
        [RequireAdmin]
        public async Task<ActionResult<UsuarioResponseDto>> GetUserById(int id)
        {
            var usuario = await _usuarioService.GetUserByIdAsync(id);
            if (usuario == null) return NotFound("Usuário não encontrado.");
            return Ok(usuario);
        }

        [HttpPost("ban/{id}")]
        [RequireAdmin]
        public async Task<IActionResult> BanUser(int id)
        {
            var result = await _usuarioService.BanUserAsync(id);
            if (!result)
            {
                return NotFound("Usuário não encontrado ou já banido.");
            }
            return NoContent();
        }

        [HttpPost("unban/{id}")]
        [RequireAdmin]
        public async Task<IActionResult> UnbanUser(int id)
        {
            var result = await _usuarioService.UnbanUserAsync(id);
            if (!result)
            {
                return NotFound("Usuário não encontrado ou já ativo.");
            }
            return NoContent();
        }

        [HttpPut("{id}/role")]
        [RequireAdmin]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateUserRoleDto roleDto)
        {
            if (roleDto == null || roleDto.NewRoleId <= 0)
            {
                return BadRequest("Dados de role inválidos.");
            }

            var result = await _usuarioService.UpdateUserRoleAsync(id, roleDto.NewRoleId);
            if (!result)
            {
                return NotFound("Usuário ou Role não encontrados.");
            }
            return NoContent();
        }
    }
}
