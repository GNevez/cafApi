using cafApi.Models;
using cafApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cafApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class CorController : ControllerBase
    {
        private readonly ICorService _service;

        public CorController(ICorService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cor>>> Get()
        {
            var cores = await _service.GetAllAsync();
            return Ok(cores);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Cor>> Get(int id)
        {
            var cor = await _service.GetByIdAsync(id);
            if (cor == null) return NotFound();
            return Ok(cor);
        }
    }
}
