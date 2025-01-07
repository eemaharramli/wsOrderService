using Microsoft.EntityFrameworkCore;
using wsOrderService.Models;

namespace wsOrderService.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {}

        public DbSet<Order> Orders { get; set; }
    }
}
