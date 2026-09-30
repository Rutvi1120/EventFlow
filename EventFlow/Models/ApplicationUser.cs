using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace EventFlow.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(50)]
        public string? StudentId { get; set; }

        [StringLength(30)]
        public string? RequestedRole { get; set; }

        public bool IsApproved { get; set; } = false;

        public int? RequestedClubId { get; set; }

        public Club? RequestedClub { get; set; }

        public ICollection<Event> OrganizedEvents { get; set; }
            = new List<Event>();

        public ICollection<Registration> Registrations { get; set; }
            = new List<Registration>();

        public ICollection<WaitlistEntry> WaitlistEntries { get; set; }
            = new List<WaitlistEntry>();

        public ICollection<Volunteer> VolunteerAssignments { get; set; }
            = new List<Volunteer>();
    }
}