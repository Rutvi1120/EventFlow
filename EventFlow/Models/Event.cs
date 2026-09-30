using System.ComponentModel.DataAnnotations;

namespace EventFlow.Models
{
    public class Event
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        [Required]
        [Range(1, 100000)]
        public int MaxParticipants { get; set; }

        [Required]
        [StringLength(30)]
        public string EventType { get; set; } = "Student";

        [Required]
        [StringLength(30)]
        public string ApprovalStatus { get; set; } = "Pending";

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Upcoming";

        [StringLength(150)]
        public string? ClubName { get; set; }

        [Required]
        public int VenueId { get; set; }

        public Venue? Venue { get; set; }

        public string? OrganizerId { get; set; }

        public ApplicationUser? Organizer { get; set; }

        public int? ClubId { get; set; }

        public Club? Club { get; set; }

        public ICollection<Registration> Registrations { get; set; }
            = new List<Registration>();

        public ICollection<WaitlistEntry> WaitlistEntries { get; set; }
            = new List<WaitlistEntry>();

        public ICollection<Volunteer> Volunteers { get; set; }
            = new List<Volunteer>();
    }
}