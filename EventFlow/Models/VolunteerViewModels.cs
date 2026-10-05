using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EventFlow.Models
{
    /// <summary>
    /// Posted by a student. Deliberately does NOT contain VolunteerId or Status:
    /// those are always set on the server.
    /// </summary>
    public class VolunteerApplyViewModel
    {
        public int EventId { get; set; }

        [StringLength(1000, ErrorMessage = "Notes cannot be longer than 1000 characters.")]
        public string? Notes { get; set; }

        [BindNever]
        [ValidateNever]
        public Event? Event { get; set; }
    }

    public class AssignVolunteerWorkViewModel
    {
        public int VolunteerId { get; set; }
        public int EventId { get; set; }

        [Required(ErrorMessage = "Please enter the work to assign.")]
        [StringLength(100, ErrorMessage = "Assigned work cannot be longer than 100 characters.")]
        [Display(Name = "Assigned Work")]
        public string AssignedWork { get; set; } = string.Empty;
    }

    public class EventVolunteersViewModel
    {
        public Event Event { get; set; } = default!;
        public List<Volunteer> Pending { get; set; } = new();
        public List<Volunteer> Accepted { get; set; } = new();
        public List<Volunteer> Completed { get; set; } = new();
        public List<Volunteer> Rejected { get; set; } = new();

        public int Total =>
            Pending.Count + Accepted.Count + Completed.Count + Rejected.Count;
    }

    public class VolunteerManageRow
    {
        public int EventId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string? ClubName { get; set; }
        public string? VenueName { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public int Pending { get; set; }
        public int Accepted { get; set; }
        public int Completed { get; set; }
        public int Rejected { get; set; }

        public bool HasEnded => DateTime.Now >= EndDateTime;
    }
}
