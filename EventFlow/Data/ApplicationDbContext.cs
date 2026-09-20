using EventFlow.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EventFlow.Models;
namespace EventFlow.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext(options)
    {
        public DbSet<Event> Events { get; set; }
        public DbSet<Venue> Venues { get; set; }
    }
}