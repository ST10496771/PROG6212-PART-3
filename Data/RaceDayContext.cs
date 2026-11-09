using Microsoft.EntityFrameworkCore;
using RaceDayApi.Models;

namespace RaceDayApi.Data;

public class RaceDayContext : DbContext
{
    public RaceDayContext(DbContextOptions<RaceDayContext> options) : base(options)
    {
    }

    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventEntrance> EventEntrances => Set<EventEntrance>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organiser>(e =>
        {
            e.ToTable("Organiser");
            e.HasKey(x => x.OrganiserID);
            e.HasIndex(x => x.OrganiserEmailAddress).IsUnique();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        modelBuilder.Entity<Participant>(e =>
        {
            e.ToTable("Participant");
            e.HasKey(x => x.ParticipantID);
            e.HasIndex(x => x.EmailAddress).IsUnique();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Event");
            e.HasKey(x => x.EventID);
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Organiser)
                .WithMany(o => o.Events)
                .HasForeignKey(x => x.OrganiserID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Category");
            e.HasKey(x => x.CategoryID);
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventEntrance>(e =>
        {
            e.ToTable("EventEntrance");
            e.HasKey(x => x.EventEntranceID);
            e.Property(x => x.EntryDate).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => new { x.ParticipantID, x.CategoryID }).IsUnique();
            e.HasOne(x => x.Participant)
                .WithMany(p => p.EventEntrances)
                .HasForeignKey(x => x.ParticipantID)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category)
                .WithMany(c => c.EventEntrances)
                .HasForeignKey(x => x.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Result");
            e.HasKey(x => x.ResultID);
            e.Property(x => x.RecordedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.EventEntranceID).IsUnique();
            e.HasOne(x => x.EventEntrance)
                .WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EventEntranceID)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
