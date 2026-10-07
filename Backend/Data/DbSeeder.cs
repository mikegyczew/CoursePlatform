using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(CourseDbContext db)
    {
        if (!await db.Courses.AnyAsync())
        {
            var course = new Course
            {
                Title = "Podstawy programowania w C#",
                Description = "Kurs demonstracyjny platformy CoursePlatform.",
                Category = "Programowanie",
                ImageUrl = null
            };

            course.Lessons.Add(new Lesson
            {
                Title = "Pierwszy program w C#",
                Description = "Wprowadzenie do tworzenia aplikacji w C#.",
                Content = """
                Witaj w CoursePlatform!

                W tej lekcji poznasz podstawy języka C#,
                strukturę programu oraz metodę Main.
                """,
                Order = 1
            });

            db.Courses.Add(course);
        }

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Email = "demo@courseplatform.local",
                Name = "Demo User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                    "CoursePlatformDemo123!"
                ),
                EmailConfirmed = true
            });
        }

        await db.SaveChangesAsync();
    }
}
