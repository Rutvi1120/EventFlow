using System.ComponentModel.DataAnnotations;

namespace EventFlow.Models
{
    public class Volunteer
    {
        public int Id { get; set; }

        [Required]
        public string VolunteerId { get; set; } = string.Empty;

        public ApplicationUser? VolunteerUser { get; set; }

        [Required]
        public int EventId { get; set; }

        public Event? Event { get; set; }

        [Required]
        [StringLength(100)]
        public string Role { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Notes { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        public DateTime? AssignedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}