using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services
{
    public static class EventRules
    {
       
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
