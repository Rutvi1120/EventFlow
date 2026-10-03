using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services
{
    public static class EventRules
    {
        /// <summary>
        /// Server-side check shared by every place that creates or edits an event:
        /// the venue must exist and MaxParticipants must not exceed its capacity.
        /// Adds errors to ModelState; the caller decides what to do.
        /// </summary>
        public static async Task ValidateVenueAsync(
            ApplicationDbContext context,
            ModelStateDictionary modelState,
            Event eventItem)
        {
            var venue = await context.Venues
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == eventItem.VenueId);

            if (venue == null)
            {
                modelState.AddModelError(
                    nameof(Event.VenueId),
                    "Please select a valid venue.");

                return;
            }

            if (eventItem.MaxParticipants > venue.Capacity)
            {
                modelState.AddModelError(
                    nameof(Event.MaxParticipants),
                    $"This venue can accommodate only {venue.Capacity} participants.");
            }
        }
    }
}
