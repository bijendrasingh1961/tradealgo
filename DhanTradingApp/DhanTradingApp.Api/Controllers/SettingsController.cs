using DhanTradingApp.Api.Data;
using DhanTradingApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhanTradingApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly TradingDbContext _context;

        public SettingsController(TradingDbContext _context)
        {
            this._context = _context;
        }

        [HttpGet]
        public async Task<ActionResult<DhanSettings>> GetSettings()
        {
            var settings = await _context.DhanSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                return NotFound();
            }
            return settings;
        }

        [HttpPost]
        public async Task<ActionResult<DhanSettings>> SaveSettings(DhanSettings settings)
        {
            var existing = await _context.DhanSettings.FirstOrDefaultAsync();
            if (existing == null)
            {
                _context.DhanSettings.Add(settings);
            }
            else
            {
                existing.ClientId = settings.ClientId;
                existing.AccessToken = settings.AccessToken;
            }

            await _context.SaveChangesAsync();
            return Ok(settings);
        }
    }
}
