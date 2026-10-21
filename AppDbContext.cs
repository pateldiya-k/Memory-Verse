using Microsoft.EntityFrameworkCore;
using MemoryVerse.Models;

namespace MemoryVerse.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Memory> Memories => Set<Memory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(u => u.Name).HasMaxLength(50).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
        });

        b.Entity<Memory>(e =>
        {
            e.Property(m => m.Title).HasMaxLength(120).IsRequired();
            e.Property(m => m.Story).HasMaxLength(5000).IsRequired();
            e.Property(m => m.Mood).HasMaxLength(20);
            e.Property(m => m.Category).HasMaxLength(40);
            e.HasIndex(m => new { m.UserId, m.MemoryDate });
        });
    }
}
