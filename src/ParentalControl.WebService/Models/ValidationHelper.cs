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
