using EventFlow.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Event> Events { get; set; }
        public DbSet<Venue> Venues { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<WaitlistEntry> WaitlistEntries { get; set; }
        public DbSet<Volunteer> Volunteers { get; set; }
        public DbSet<Club> Clubs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Event>()
                .HasOne(e => e.Organizer)
                .WithMany(u => u.OrganizedEvents)
                .HasForeignKey(e => e.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Event>()
            .HasOne(e => e.FacultySupervisor)
            .WithMany()
            .HasForeignKey(e => e.FacultySupervisorId)
            .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Event>()
                .HasOne(e => e.Club)
                .WithMany(c => c.Events)
                .HasForeignKey(e => e.ClubId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Club>()
                .HasOne(c => c.ClubPresident)
                .WithMany()
                .HasForeignKey(c => c.ClubPresidentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Club>()
                .HasOne(c => c.FacultySupervisor)
                .WithMany()
                .HasForeignKey(c => c.FacultySupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.RequestedClub)
                .WithMany()
                .HasForeignKey(u => u.RequestedClubId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Registration>()
                .HasOne(r => r.Event)
                .WithMany(e => e.Registrations)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Registration>()
                .HasOne(r => r.User)
                .WithMany(u => u.Registrations)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Registration>()
                .HasIndex(r => new { r.EventId, r.UserId })
                .IsUnique();

            builder.Entity<WaitlistEntry>()
                .HasOne(w => w.Event)
                .WithMany(e => e.WaitlistEntries)
                .HasForeignKey(w => w.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WaitlistEntry>()
                .HasOne(w => w.User)
                .WithMany(u => u.WaitlistEntries)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<WaitlistEntry>()
                .HasIndex(w => new { w.EventId, w.UserId })
                .IsUnique();

            builder.Entity<Volunteer>()
                .HasOne(v => v.Event)
                .WithMany(e => e.Volunteers)
                .HasForeignKey(v => v.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Volunteer>()
                .HasOne(v => v.VolunteerUser)
                .WithMany(u => u.VolunteerAssignments)
                .HasForeignKey(v => v.VolunteerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Volunteer>()
                .HasIndex(v => new { v.EventId, v.VolunteerId })
                .IsUnique();

            builder.Entity<Event>()
                .Property(e => e.EventType)
                .HasDefaultValue("Student");

            builder.Entity<Event>()
                .Property(e => e.ApprovalStatus)
                .HasDefaultValue("Pending");

            builder.Entity<Event>()
                .Property(e => e.Status)
                .HasDefaultValue("Upcoming");

            builder.Entity<ApplicationUser>()
                .Property(u => u.IsApproved)
                .HasDefaultValue(false);

            builder.Entity<Club>()
                .Property(c => c.Status)
                .HasDefaultValue("Pending");
        }
    }
}