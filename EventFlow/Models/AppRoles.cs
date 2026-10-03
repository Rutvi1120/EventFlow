namespace EventFlow.Models
{
    /// <summary>
    /// Central role names. Values match the roles seeded by RoleSeeder.
    /// </summary>
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Student = "Student";
        public const string Faculty = "Faculty";
        public const string ClubPresident = "ClubPresident";

        /// <summary>Roles that may review volunteers (comma separated for [Authorize]).</summary>
        public const string VolunteerManagers = "Admin,Faculty,ClubPresident";

        /// <summary>Roles that may add a volunteer directly.</summary>
        public const string VolunteerAdders = "Faculty,ClubPresident";
    }
}
