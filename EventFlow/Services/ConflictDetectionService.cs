using EventFlow.Data;
using EventFlow.Models;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services
{
    public class ConflictDetectionService
    {
        private readonly ApplicationDbContext _context;

        public ConflictDetectionService(ApplicationDbContext context)
        {
            _context = context;
        }

       

        public async Task<Event?> FindVenueConflictAsync(
            int venueId,
            DateTime startDateTime,
            DateTime endDateTime,
            int? excludeEventId = null)
        {
            if (venueId <= 0)
                return null;

            return await _context.Events
                .Include(e => e.Venue)
                .Where(e =>
                    e.VenueId == venueId &&
                    e.StartDateTime < endDateTime &&
                    startDateTime < e.EndDateTime &&
                    (!excludeEventId.HasValue ||
                     e.Id != excludeEventId.Value))
                .OrderBy(e => e.StartDateTime)
                .FirstOrDefaultAsync();
        }


      
        public async Task<Event?> FindParticipantConflictAsync(
            string userId,
            int eventId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            var currentEvent = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (currentEvent == null)
                return null;

            return await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Registrations)
                .Where(e =>
                    e.Registrations.Any(r =>
                        r.UserId == userId) &&

                    e.Id != eventId &&

                    e.StartDateTime < currentEvent.EndDateTime &&
                    currentEvent.StartDateTime < e.EndDateTime)
                .OrderBy(e => e.StartDateTime)
                .FirstOrDefaultAsync();
        }


       

        public async Task<Event?> FindParticipantConflictAsync(
            string userId,
            DateTime startDateTime,
            DateTime endDateTime,
            int? excludeEventId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return await _context.Events
                .Include(e => e.Registrations)
                .Where(e =>
                    e.Registrations.Any(r =>
                        r.UserId == userId) &&

                    e.StartDateTime < endDateTime &&
                    startDateTime < e.EndDateTime &&

                    (!excludeEventId.HasValue ||
                     e.Id != excludeEventId.Value))
                .OrderBy(e => e.StartDateTime)
                .FirstOrDefaultAsync();
        }


        


        public async Task<Volunteer?> FindVolunteerConflictAsync(
    string volunteerId,
    DateTime startDateTime,
    DateTime endDateTime,
    int? excludeEventId = null)
        {
            if (string.IsNullOrWhiteSpace(volunteerId))
                return null;

            var volunteerAssignments = await _context.Volunteers
                .Include(v => v.Event)
                .Where(v =>
                    v.VolunteerId == volunteerId &&
                    v.Event != null &&
                    (v.Status == VolunteerStatus.Pending ||
                     v.Status == VolunteerStatus.Accepted) &&
                    (!excludeEventId.HasValue ||
                     v.EventId != excludeEventId.Value))
                .ToListAsync();

            return volunteerAssignments
                .Where(v =>
                    v.Event!.StartDateTime < endDateTime &&
                    startDateTime < v.Event.EndDateTime)
                .OrderBy(v => v.Event!.StartDateTime)
                .FirstOrDefault();
        }

      

        public async Task<List<string>> CheckEventConflictsAsync(
            int eventId)
        {
            var conflicts = new List<string>();

            var eventItem = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventItem == null)
                return conflicts;


         

            if (eventItem.VenueId > 0)
            {
                var venueConflict = await FindVenueConflictAsync(
                    eventItem.VenueId,
                    eventItem.StartDateTime,
                    eventItem.EndDateTime,
                    eventItem.Id);

                if (venueConflict != null)
                {
                    conflicts.Add(
                        $"Venue conflict: {venueConflict.Name} is already scheduled " +
                        $"from {venueConflict.StartDateTime:g} to " +
                        $"{venueConflict.EndDateTime:g}.");
                }
            }


          

            var registrations = await _context.Registrations
                .Where(r => r.EventId == eventId)
                .ToListAsync();

            foreach (var registration in registrations)
            {
                if (string.IsNullOrWhiteSpace(registration.UserId))
                    continue;

                var participantConflict =
                    await FindParticipantConflictAsync(
                        registration.UserId,
                        eventItem.StartDateTime,
                        eventItem.EndDateTime,
                        eventItem.Id);

                if (participantConflict != null)
                {
                    conflicts.Add(
                        $"Participant conflict: a participant registered for " +
                        $"'{participantConflict.Name}' has an overlapping event.");
                }
            }


           

            var volunteers = await _context.Volunteers
                .Where(v =>
                    v.EventId == eventId &&
                    (v.Status == VolunteerStatus.Pending ||
                     v.Status == VolunteerStatus.Accepted))
                .ToListAsync();

            foreach (var volunteer in volunteers)
            {
                if (string.IsNullOrWhiteSpace(volunteer.VolunteerId))
                    continue;

                var volunteerConflict =
                    await FindVolunteerConflictAsync(
                        volunteer.VolunteerId,
                        eventItem.StartDateTime,
                        eventItem.EndDateTime);

                if (volunteerConflict != null &&
                    volunteerConflict.EventId != eventId)
                {
                    conflicts.Add(
                        $"Volunteer conflict: a volunteer is already assigned " +
                        $"to '{volunteerConflict.Event?.Name}'.");
                }
            }

            return conflicts;
        }
    }
}