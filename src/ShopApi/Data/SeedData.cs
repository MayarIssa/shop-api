using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopApi.Models;

namespace ShopApi.Data;

public static class SeedData
{
    /// <summary>
    /// Creates the database if needed and adds an admin account plus sample catalog data
    /// the first time the app runs. Safe to call on every start.
    /// </summary>
    public static async Task InitializeAsync(ShopDbContext db, IConfiguration config)
    {
        await db.Database.EnsureCreatedAsync();

        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword)
            && !await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var admin = new User { Email = adminEmail, FullName = "Store Admin", Role = Roles.Admin };
            admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, adminPassword);
            db.Users.Add(admin);
        }

        if (!await db.Categories.AnyAsync())
        {
            var electronics = new Category { Name = "Electronics", Description = "Phones, laptops and accessories" };
            var books = new Category { Name = "Books", Description = "Printed and digital books" };
            var home = new Category { Name = "Home", Description = "Kitchen and home essentials" };
            db.Categories.AddRange(electronics, books, home);

            db.Products.AddRange(
                new Product { Name = "Wireless Earbuds", Price = 49.99m, Stock = 120, Category = electronics, Description = "Bluetooth 5.3 earbuds with charging case" },
                new Product { Name = "USB-C Charger 65W", Price = 29.50m, Stock = 80, Category = electronics, Description = "Fast charger for laptops and phones" },
                new Product { Name = "Mechanical Keyboard", Price = 89.00m, Stock = 35, Category = electronics, Description = "Hot-swappable switches, RGB backlight" },
                new Product { Name = "Clean Code", Price = 34.99m, Stock = 50, Category = books, Description = "Robert C. Martin" },
                new Product { Name = "Designing Data-Intensive Applications", Price = 44.99m, Stock = 40, Category = books, Description = "Martin Kleppmann" },
                new Product { Name = "Ceramic Mug Set", Price = 19.99m, Stock = 200, Category = home, Description = "Set of 4, dishwasher safe" });
        }

        await db.SaveChangesAsync();
    }
}
