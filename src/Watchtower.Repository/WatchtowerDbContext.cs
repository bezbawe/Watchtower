using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Watchtower.Entities.Alerts;
using Watchtower.Entities.Events;

namespace Watchtower.Repository;

public class WatchtowerDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public WatchtowerDbContext(DbContextOptions<WatchtowerDbContext> options) : base(options)
    {
    }

    public DbSet<LogEvent> LogEvents => Set<LogEvent>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LogEvent>(entity =>
        {
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.EventType).HasConversion<string>().HasMaxLength(40);

            var fieldsConverter = new ValueConverter<Dictionary<string, string>, string>(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions) ?? new Dictionary<string, string>());

            var fieldsComparer = new ValueComparer<Dictionary<string, string>>(
                (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                v => v == null ? 0 : JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!);

            entity.Property(e => e.Fields)
                .HasColumnType("jsonb")
                .HasConversion(fieldsConverter, fieldsComparer);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.SourceIp);
            entity.HasIndex(e => e.Actor);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Fields).HasMethod("gin");
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
        });
    }
}
