using EventFlow.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Event> Events { get; set; }
        public DbSet<Venue> Venues { get; set; }

        public DbSet<Registration> Registrations { get; set; }

        public DbSet<WaitlistEntry> WaitlistEntries { get; set; }
    }
}