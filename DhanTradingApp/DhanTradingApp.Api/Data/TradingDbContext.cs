using DhanTradingApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DhanTradingApp.Api.Data
{
    public class TradingDbContext : DbContext
    {
        public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options)
        {
        }

        public DbSet<DhanSettings> DhanSettings { get; set; }
    }
}
