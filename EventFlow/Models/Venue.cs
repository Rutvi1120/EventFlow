using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations;

namespace EventFlow.Models
{
    public class Venue
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Location { get; set; } = string.Empty;

        [Range(1, 100000)]
        public int Capacity { get; set; }
    }
}