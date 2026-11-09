using RaceDayApi.Models;

namespace RaceDayApi.Data;

/// <summary>
/// Adds a small set of demo data the first time the database is empty, so the website has events to show.
/// All dates are relative to today, so there are always upcoming events and one finished event with results.
/// Every demo account uses the password Password123!
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static void Seed(RaceDayContext db)
    {
        if (db.Organisers.Any() || db.Participants.Any() || db.Events.Any()) return;

        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        var today = DateTime.UtcNow.Date;

        var thabo = new Organiser
        {
            OrganiserFirstName = "Thabo",
            OrganiserLastName = "Nkosi",
            OrganiserEmailAddress = "thabo.nkosi@raceday.co.za",
            OrganiserContactNumber = "0821234567",
            PasswordHash = hash
        };
        var sarah = new Organiser
        {
            OrganiserFirstName = "Sarah",
            OrganiserLastName = "van der Merwe",
            OrganiserEmailAddress = "sarah.vdm@raceday.co.za",
            OrganiserContactNumber = "0837654321",
            PasswordHash = hash
        };

        var lindiwe = new Participant { FirstName = "Lindiwe", LastName = "Dube", EmailAddress = "lindiwe.dube@example.com", PasswordHash = hash };
        var james = new Participant { FirstName = "James", LastName = "Botha", EmailAddress = "james.botha@example.com", PasswordHash = hash };
        var aisha = new Participant { FirstName = "Aisha", LastName = "Patel", EmailAddress = "aisha.patel@example.com", PasswordHash = hash };

        // A finished event (results captured) and two upcoming events.
        var cycleTour = new Event
        {
            EventName = "Pretoria Cycle Tour",
            EventDescription = "A fast, flat road tour through the Pretoria suburbs with a social and a race category.",
            EventDate = today.AddDays(-60),
            Location = "Pretoria",
            DistanceKm = 80,
            EventType = EventType.Cycle
        };
        var durbanRun = new Event
        {
            EventName = "Durban Beachfront 10km Run",
            EventDescription = "Run the famous Golden Mile promenade with the ocean on your left the whole way.",
            EventDate = today.AddDays(14),
            Location = "Durban",
            DistanceKm = 10,
            EventType = EventType.Run
        };
        var sowetoWalk = new Event
        {
            EventName = "Soweto Heritage Walk",
            EventDescription = "A relaxed 15km walk past the landmarks of Soweto, open to all ages.",
            EventDate = today.AddDays(45),
            Location = "Soweto",
            DistanceKm = 15,
            EventType = EventType.Walk
        };

        thabo.Events.Add(cycleTour);
        thabo.Events.Add(sowetoWalk);
        sarah.Events.Add(durbanRun);

        var social = new Category { CategoryName = "40km Social", CategoryDescription = "A relaxed ride for beginners", Event = cycleTour };
        var race = new Category { CategoryName = "80km Race", CategoryDescription = "Timed race distance", Event = cycleTour };
        var under20 = new Category { CategoryName = "Under 20", CategoryDescription = "Runners younger than 20", Event = durbanRun };
        var senior = new Category { CategoryName = "Senior", CategoryDescription = "Runners 20 and older", Event = durbanRun };
        var walk15 = new Category { CategoryName = "15km Walk", CategoryDescription = "All ages", Event = sowetoWalk };

        db.Organisers.AddRange(thabo, sarah);
        db.Participants.AddRange(lindiwe, james, aisha);
        db.Categories.AddRange(social, race, under20, senior, walk15);

        // Finished event: three entries with results.
        db.EventEntrances.AddRange(
            new EventEntrance
            {
                Participant = lindiwe,
                Category = race,
                EventEntranceDescription = "Lindiwe entered the 80km Race at Pretoria Cycle Tour",
                Result = new Result { FinishTime = new TimeSpan(2, 41, 10), FinishPosition = 1, ResultDescription = "Lindiwe finished the 80km Race" }
            },
            new EventEntrance
            {
                Participant = james,
                Category = race,
                EventEntranceDescription = "James entered the 80km Race at Pretoria Cycle Tour",
                Result = new Result { FinishTime = new TimeSpan(2, 55, 32), FinishPosition = 2, ResultDescription = "James finished the 80km Race" }
            },
            new EventEntrance
            {
                Participant = aisha,
                Category = social,
                EventEntranceDescription = "Aisha entered the 40km Social at Pretoria Cycle Tour",
                Result = new Result { FinishTime = new TimeSpan(1, 35, 0), FinishPosition = 1, ResultDescription = "Aisha finished the 40km Social" }
            },
            // Upcoming events: enrolled, no result yet.
            new EventEntrance
            {
                Participant = lindiwe,
                Category = senior,
                EventEntranceDescription = "Lindiwe entered the Senior at Durban Beachfront 10km Run"
            },
            new EventEntrance
            {
                Participant = james,
                Category = walk15,
                EventEntranceDescription = "James entered the 15km Walk at Soweto Heritage Walk"
            });

        db.SaveChanges();
    }
}
