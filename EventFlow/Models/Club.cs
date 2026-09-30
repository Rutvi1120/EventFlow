using System.ComponentModel.DataAnnotations;

namespace EventFlow.Models
{
    public class Club
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        public string? ClubPresidentId { get; set; }

        public ApplicationUser? ClubPresident { get; set; }

        [Required]
        public string FacultySupervisorId { get; set; } = string.Empty;

        public ApplicationUser? FacultySupervisor { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        public ICollection<Event> Events { get; set; }
            = new List<Event>();
    }
}