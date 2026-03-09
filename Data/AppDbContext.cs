using Microsoft.EntityFrameworkCore;
using KidsLearningPlatform.Api.Models;

namespace KidsLearningPlatform.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<LessonQuestion> LessonQuestions { get; set; }
    public DbSet<Progress> Progresses { get; set; }
    public DbSet<Class> Classes { get; set; }
    public DbSet<Material> Materials { get; set; }
    public DbSet<MaterialQuestion> MaterialQuestions { get; set; }

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

        // Material → Course relationship
        modelBuilder.Entity<Material>()
            .HasOne(m => m.Course)
            .WithMany(c => c.Materials)
            .HasForeignKey(m => m.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // MaterialQuestion → Material relationship (cascade delete)
        modelBuilder.Entity<MaterialQuestion>()
            .HasOne(mq => mq.Material)
            .WithMany()
            .HasForeignKey(mq => mq.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        // LessonQuestion → Lesson relationship (cascade delete)
        modelBuilder.Entity<LessonQuestion>()
            .HasOne(q => q.Lesson)
            .WithMany()
            .HasForeignKey(q => q.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
