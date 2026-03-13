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
    public DbSet<ClassStudent> ClassStudents { get; set; }
    public DbSet<Material> Materials { get; set; }
    public DbSet<MaterialQuestion> MaterialQuestions { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Announcement> Announcements { get; set; }
    public DbSet<Achievement> Achievements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Existing Relations ──────────────────────────────────────────────

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

        modelBuilder.Entity<Material>()
            .HasOne(m => m.Course)
            .WithMany(c => c.Materials)
            .HasForeignKey(m => m.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MaterialQuestion>()
            .HasOne(mq => mq.Material)
            .WithMany()
            .HasForeignKey(mq => mq.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LessonQuestion>()
            .HasOne(q => q.Lesson)
            .WithMany()
            .HasForeignKey(q => q.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Enrollment ──────────────────────────────────────────────────────

        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.Course)
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent duplicate enrollments
        modelBuilder.Entity<Enrollment>()
            .HasIndex(e => new { e.StudentId, e.CourseId })
            .IsUnique();

        // ── ClassStudent (composite key) ─────────────────────────────────────

        modelBuilder.Entity<ClassStudent>()
            .HasKey(cs => new { cs.ClassId, cs.StudentId });

        modelBuilder.Entity<ClassStudent>()
            .HasOne(cs => cs.Class)
            .WithMany()
            .HasForeignKey(cs => cs.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClassStudent>()
            .HasOne(cs => cs.Student)
            .WithMany()
            .HasForeignKey(cs => cs.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Announcement ─────────────────────────────────────────────────────

        modelBuilder.Entity<Announcement>()
            .HasOne(a => a.Author)
            .WithMany()
            .HasForeignKey(a => a.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Achievement ──────────────────────────────────────────────────────

        modelBuilder.Entity<Achievement>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
