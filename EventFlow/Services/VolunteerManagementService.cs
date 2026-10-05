using System.Security.Claims;
using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services
{
    /// <summary>
    /// Single source of truth for who may manage volunteers for an event.
    ///
    /// Admin          -> every event
    /// Event creator  -> their own events, regardless of their application role
    /// Faculty        -> College events, assigned Student events, and supervised Club events
    /// ClubPresident  -> events belonging to their club
    ///
    /// Students can apply to volunteer, but they cannot manage volunteers unless
    /// they are also the creator of that event.
    /// </summary>
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
                // Event creator can manage volunteers for their own event.
                e.OrganizerId == userId

                ||

                // Existing Faculty permissions.
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

                // Existing Club President permissions.
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
