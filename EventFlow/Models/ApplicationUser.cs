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

        [StringLength(20)]
        public string? StudentId { get; set; }
    }
}