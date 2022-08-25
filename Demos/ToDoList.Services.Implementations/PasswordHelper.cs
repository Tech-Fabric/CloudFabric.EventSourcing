using Microsoft.AspNetCore.Identity;

namespace ToDoList.Services.Implementations;

public class PasswordHelper
{
    public static string HashPassword(string password)
    {
        var hasher = new PasswordHasher<object>();
        return hasher.HashPassword(new object(), password);
    }

    public static bool VerifyHashedPassword(string hashedPassword, string providedPassword)
    {
        try
        {
            var hasher = new PasswordHasher<object>();
            var result = hasher.VerifyHashedPassword(new object(), hashedPassword, providedPassword);
            return result == PasswordVerificationResult.Success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Generates a Random Password
    ///     respecting the given strength requirements.
    /// </summary>
    /// <param name="opts">
    ///     A valid PasswordOptions object
    ///     containing the password strength requirements.
    /// </param>
    /// <returns>A random password</returns>
    public static string GenerateRandomPassword(PasswordOptions? opts = null)
    {
        opts ??= new PasswordOptions
            {
                RequiredLength = 8,
                RequiredUniqueChars = 4,
                RequireDigit = true,
                RequireLowercase = true,
                RequireNonAlphanumeric = true,
                RequireUppercase = true
            };

        string[] randomChars =
        {
                "ABCDEFGHJKLMNOPQRSTUVWXYZ", // uppercase 
                "abcdefghijkmnopqrstuvwxyz", // lowercase
                "0123456789", // digits
                "!@$?_-" // non-alphanumeric
            };
        var rand = new Random(Environment.TickCount);
        var chars = new List<char>();

        if (opts.RequireUppercase)
            chars.Insert(rand.Next(0, chars.Count),
                randomChars[0][rand.Next(0, randomChars[0].Length)]);

        if (opts.RequireLowercase)
            chars.Insert(rand.Next(0, chars.Count),
                randomChars[1][rand.Next(0, randomChars[1].Length)]);

        if (opts.RequireDigit)
            chars.Insert(rand.Next(0, chars.Count),
                randomChars[2][rand.Next(0, randomChars[2].Length)]);

        if (opts.RequireNonAlphanumeric)
            chars.Insert(rand.Next(0, chars.Count),
                randomChars[3][rand.Next(0, randomChars[3].Length)]);

        for (var i = chars.Count;
            i < opts.RequiredLength
            || chars.Distinct().Count() < opts.RequiredUniqueChars;
            i++)
        {
            var rcs = randomChars[rand.Next(0, randomChars.Length)];
            chars.Insert(rand.Next(0, chars.Count),
                rcs[rand.Next(0, rcs.Length)]);
        }

        return new string(chars.ToArray());
    }
}