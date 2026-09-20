using System.ComponentModel.DataAnnotations;

namespace EventFlow.Models
{
    public class Event
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        [Range(1, 100000)]
        public int MaxParticipants { get; set; }

        [Required]
        public string Status { get; set; } = "Upcoming";

        public int VenueId { get; set; }

        public Venue? Venue { get; set; }
    }
}