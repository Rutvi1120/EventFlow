using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventFlow.Models
{
    public class Volunteer
    {
        public int Id { get; set; }

        // Volunteer User
        [Required]
        public string VolunteerId { get; set; } = string.Empty;

        [ForeignKey("VolunteerId")]
        public ApplicationUser VolunteerUser { get; set; } = null!;

        // Event
        [Required]
        public int EventId { get; set; }

        [ForeignKey("EventId")]
        public Event Event { get; set; } = null!;

        // Assignment Information
        [Required]
        [StringLength(100)]
        public string Role { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        // Status
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Assigned";

        // Dates
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
    }
}