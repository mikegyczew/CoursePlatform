using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class CourseDbContext : DbContext
{
    public CourseDbContext(DbContextOptions<CourseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<User> Users => Set<User>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<DropboxLessonProgress> DropboxLessonProgress =>
        Set<DropboxLessonProgress>();
    public DbSet<DropboxCoupon> DropboxCoupons => Set<DropboxCoupon>();
    public DbSet<DropboxCouponRedemption> DropboxCouponRedemptions =>
        Set<DropboxCouponRedemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LessonProgress>()
            .HasIndex(progress => new
            {
                progress.UserId,
                progress.LessonId
            })
            .IsUnique();

        modelBuilder.Entity<LessonProgress>()
            .HasOne(progress => progress.User)
            .WithMany()
            .HasForeignKey(progress => progress.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LessonProgress>()
            .HasOne(progress => progress.Lesson)
            .WithMany()
            .HasForeignKey(progress => progress.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DropboxLessonProgress>()
            .HasIndex(progress => new
            {
                progress.UserId,
                progress.LessonPath
            })
            .IsUnique();

        modelBuilder.Entity<DropboxLessonProgress>()
            .HasOne(progress => progress.User)
            .WithMany()
            .HasForeignKey(progress => progress.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DropboxCoupon>()
            .Property(coupon => coupon.Type)
            .HasConversion<string>();

        modelBuilder.Entity<DropboxCoupon>()
            .HasIndex(coupon => coupon.CodeHash)
            .IsUnique();

        modelBuilder.Entity<DropboxCouponRedemption>()
            .HasIndex(redemption => new
            {
                redemption.CouponId,
                redemption.UserId
            })
            .IsUnique();

        modelBuilder.Entity<DropboxCouponRedemption>()
            .HasOne(redemption => redemption.Coupon)
            .WithMany()
            .HasForeignKey(redemption => redemption.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DropboxCouponRedemption>()
            .HasOne(redemption => redemption.User)
            .WithMany()
            .HasForeignKey(redemption => redemption.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
