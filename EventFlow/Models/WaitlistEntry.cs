using System.ComponentModel.DataAnnotations;

namespace EventFlow.Models
{
    public class WaitlistEntry
    {
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }

        public Event? Event { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.Now;
    }
}