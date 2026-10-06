using System.Security.Claims;
using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services
{
  
    public class VolunteerManagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public VolunteerManagementService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IQueryable<Event> ManageableEvents(ClaimsPrincipal user)
        {
            var query = _context.Events.AsQueryable();

            if (user.IsInRole(AppRoles.Admin))
            {
                return query;
            }

            var userId = _userManager.GetUserId(user);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return query.Where(e => false);
            }

            var isFaculty = user.IsInRole(AppRoles.Faculty);
            var isPresident = user.IsInRole(AppRoles.ClubPresident);

            return query.Where(e =>
                e.OrganizerId == userId

                ||

                (isFaculty &&
                    (
                        e.EventType == EventTypes.College
                        ||
                        (e.EventType == EventTypes.Student &&
                         e.FacultySupervisorId == userId)
                        ||
                        (e.ClubId != null &&
                         e.Club != null &&
                         e.Club.FacultySupervisorId == userId)
                    ))

                ||

                (isPresident &&
                    e.ClubId != null &&
                    e.Club != null &&
                    e.Club.ClubPresidentId == userId));
        }

        public Task<bool> CanManageVolunteersAsync(
            int eventId,
            ClaimsPrincipal user)
        {
            return ManageableEvents(user)
                .AnyAsync(e => e.Id == eventId);
        }

        public IQueryable<Volunteer> ManageableVolunteers(ClaimsPrincipal user)
        {
            var eventIds = ManageableEvents(user).Select(e => e.Id);

            return _context.Volunteers
                .Where(v => eventIds.Contains(v.EventId));
        }

        public Task<int> CountPendingAsync(ClaimsPrincipal user)
        {
            return ManageableVolunteers(user)
                .CountAsync(v => v.Status == VolunteerStatus.Pending);
        }
    }
}
