namespace EventFlow.Models
{
    /// <summary>
    /// The only valid values for Volunteer.Status.
    /// Pending   -> student applied, waiting for review
    /// Accepted  -> approved by a manager, or added directly by a manager
    /// Rejected  -> application declined
    /// Completed -> student finished the volunteer work
    /// </summary>
    public static class VolunteerStatus
    {
        public const string Pending = "Pending";
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
        public const string Completed = "Completed";
    }

    public static class EventApprovalStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public static class EventTypes
    {
        public const string Student = "Student";
        public const string Club = "Club";
        public const string College = "College";
    }
}
