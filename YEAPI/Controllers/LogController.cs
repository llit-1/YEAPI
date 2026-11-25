using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YEAPI.Models;

namespace YEAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogController : ControllerBase
    {
        private readonly YEContext _db;

        public LogController(YEContext db)
        {
            _db = db;
        }

        // GET api/Log/{order}
        [HttpGet("{order}")]
        public async Task<ActionResult<List<YEHttpsLog>>> GetByOrder(string order)
        {
            var logs = await _db.YEHttpsLog
                .Where(x => x.Order == order)
                .OrderByDescending(x => x.DatetimeBegin)
                .ToListAsync();

            return Ok(logs);
        }
    }
}