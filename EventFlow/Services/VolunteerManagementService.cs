using System.Security.Claims;
using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services
{
    public enum DirectAddAccess
    {
        Allowed,
        EventNotFound,
        NotAuthorized,
        EventUnavailable
    }

    public enum DirectAddResult
    {
        Success,
        InvalidStudent,
        Duplicate
    }

    /// <summary>
    /// Single source of truth for "who may manage volunteers of which event".
    ///
    ///  Admin          -> every event
    ///  Faculty        -> College events, and Club events of clubs they supervise
    ///  ClubPresident  -> events of the club(s) they preside over
    ///
    /// Every controller action that touches volunteers uses this service,
    /// so hiding a button in a view is never the only protection.
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

        // -------------------------------------------------------------
        // AUTHORIZATION
        // -------------------------------------------------------------

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
                (isFaculty &&
                    (e.EventType == EventTypes.College ||
                     (e.ClubId != null &&
                      e.Club != null &&
                      e.Club.FacultySupervisorId == userId)))
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

        public static bool CanAddDirectly(ClaimsPrincipal user)
        {
            return user.IsInRole(AppRoles.Faculty) ||
                   user.IsInRole(AppRoles.ClubPresident);
        }

        // -------------------------------------------------------------
        // DIRECT ADD (Faculty / ClubPresident)
        // -------------------------------------------------------------

        public async Task<(DirectAddAccess Access, Event? Event)>
            CheckDirectAddAccessAsync(int eventId, ClaimsPrincipal user)
        {
            var eventItem = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventItem == null)
            {
                return (DirectAddAccess.EventNotFound, null);
            }

            if (!CanAddDirectly(user) ||
                !await CanManageVolunteersAsync(eventId, user))
            {
                return (DirectAddAccess.NotAuthorized, null);
            }

            if (eventItem.ApprovalStatus != EventApprovalStatus.Approved ||
                eventItem.HasEnded)
            {
                return (DirectAddAccess.EventUnavailable, eventItem);
            }

            return (DirectAddAccess.Allowed, eventItem);
        }

        /// <summary>Students that do not yet have a volunteer record for the event.</summary>
        public async Task<List<SelectListItem>> GetStudentOptionsAsync(int eventId)
        {
            var students = await _userManager.GetUsersInRoleAsync(AppRoles.Student);

            var alreadyAssigned = await _context.Volunteers
                .Where(v => v.EventId == eventId)
                .Select(v => v.VolunteerId)
                .ToListAsync();

            return students
                .Where(s => !alreadyAssigned.Contains(s.Id))
                .OrderBy(s => s.FullName)
                .Select(s => new SelectListItem
                {
                    Value = s.Id,
                    Text = string.IsNullOrWhiteSpace(s.StudentId)
                        ? $"{s.FullName} ({s.Email})"
                        : $"{s.FullName} - {s.StudentId} ({s.Email})"
                })
                .ToList();
        }

        /// <summary>
        /// Adds the student directly with status Accepted.
        /// The caller must already have passed CheckDirectAddAccessAsync.
        /// </summary>
        public async Task<DirectAddResult> AddVolunteerDirectlyAsync(
            Event eventItem,
            AddVolunteerViewModel model)
        {
            var student = string.IsNullOrWhiteSpace(model.VolunteerId)
                ? null
                : await _userManager.FindByIdAsync(model.VolunteerId);

            if (student == null ||
                !await _userManager.IsInRoleAsync(student, AppRoles.Student))
            {
                return DirectAddResult.InvalidStudent;
            }

            var exists = await _context.Volunteers.AnyAsync(v =>
                v.EventId == eventItem.Id &&
                v.VolunteerId == student.Id);

            if (exists)
            {
                return DirectAddResult.Duplicate;
            }

            var notes = string.IsNullOrWhiteSpace(model.Notes)
                ? null
                : model.Notes.Trim();

            _context.Volunteers.Add(new Volunteer
            {
                EventId = eventItem.Id,
                VolunteerId = student.Id,
                Role = model.Role.Trim(),
                Notes = notes,
                Status = VolunteerStatus.Accepted,
                AssignedAt = DateTime.UtcNow,
                CompletedAt = null
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique index (EventId, VolunteerId) caught a concurrent insert.
                return DirectAddResult.Duplicate;
            }

            return DirectAddResult.Success;
        }
    }
}
