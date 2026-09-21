namespace EventFlow.Models
{
    public class UserRoleViewModel
    {
        public ApplicationUser User { get; set; } = default!;

        public IList<string> Roles { get; set; } = new List<string>();
    }
}