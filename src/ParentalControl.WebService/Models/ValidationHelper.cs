using System.ComponentModel.DataAnnotations;

namespace ParentalControl.WebService.Models;

public class ValidationHelper
{
    public static bool ValidateTimeLimit(int minutes) => minutes >= 0 && minutes <= 1440;
    
    public static bool ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        if (username.Length > 64) return false;
        return username.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.');
    }

    // Usernames are matched case-insensitively across the whole system (clients report
    // OS usernames, which are case-insensitive on Windows and inconsistently-cased by
    // convention on Linux); normalize once, everywhere a username is stored or looked up.
    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
    
    public static bool ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true; // Optional field
        if (email.Length > 255) return false;
        return new EmailAddressAttribute().IsValid(email);
    }
    
    public static bool ValidateProfileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Length <= 100;
    }
}
