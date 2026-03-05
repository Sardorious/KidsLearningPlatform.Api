using Microsoft.EntityFrameworkCore;
using KidsLearningPlatform.Api.Models;

namespace KidsLearningPlatform.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<Progress> Progresses { get; set; }
    public DbSet<Class> Classes { get; set; }
    public DbSet<Material> Materials { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Setup relations
        modelBuilder.Entity<Course>()
            .HasOne(c => c.Teacher)
            .WithMany(u => u.AuthoredCourses)
            .HasForeignKey(c => c.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Progress>()
            .HasOne(p => p.Student)
            .WithMany(u => u.UserProgress)
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
