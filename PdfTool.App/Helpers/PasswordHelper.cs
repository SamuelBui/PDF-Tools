using System.Security.Cryptography;

namespace PdfTool.App.Helpers;

public static class PasswordHelper
{
    public const int MinimumPasswordLength = 12;

    private static readonly string[] PassphraseWords =
    {
        "amber", "anchor", "apricot", "atlas", "bamboo", "banner", "beacon", "birch",
        "bloom", "canyon", "cedar", "cinder", "citrus", "clover", "cobalt", "comet",
        "coral", "dawn", "delta", "echo", "ember", "falcon", "forest", "frost",
        "garden", "glacier", "harbor", "hazel", "horizon", "ivory", "jasmine", "lagoon",
        "lantern", "maple", "meadow", "meridian", "meteor", "mist", "noble", "oasis",
        "olive", "opal", "orchid", "pine", "prairie", "quartz", "raven", "river",
        "saffron", "shadow", "silver", "solstice", "spruce", "summit", "sunrise", "thunder",
        "timber", "topaz", "valley", "velvet", "violet", "willow", "winter", "zephyr"
    };

    public static PasswordStrengthInfo EvaluateStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return new PasswordStrengthInfo(0, "Empty", "Enter a password to protect the PDF.");
        }

        var trimmedPassword = password.Trim();
        var score = 0;
        var hasUpper = trimmedPassword.Any(char.IsUpper);
        var hasLower = trimmedPassword.Any(char.IsLower);
        var hasDigit = trimmedPassword.Any(char.IsDigit);
        var hasSpecial = trimmedPassword.Any(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
        var meetsPolicy = MeetsStrongPolicy(trimmedPassword);

        if (trimmedPassword.Length >= MinimumPasswordLength)
        {
            score++;
        }

        if (trimmedPassword.Length >= 16)
        {
            score++;
        }

        if (hasUpper && hasLower)
        {
            score++;
        }

        if (hasDigit && hasSpecial)
        {
            score++;
        }

        if (CountWords(trimmedPassword) >= 4 && meetsPolicy)
        {
            score = Math.Max(score, 4);
        }

        if (!meetsPolicy)
        {
            return new PasswordStrengthInfo(
                Math.Min(score, 2),
                trimmedPassword.Length < MinimumPasswordLength ? "Weak" : "Fair",
                "Use at least 12 characters with uppercase, lowercase, number, and special character.");
        }

        return score switch
        {
            <= 1 => new PasswordStrengthInfo(score, "Weak", "Too easy to guess. Increase length or use a passphrase."),
            2 => new PasswordStrengthInfo(score, "Fair", "Usable, but a longer password would be safer."),
            3 => new PasswordStrengthInfo(score, "Strong", "Good balance of length and complexity."),
            _ => new PasswordStrengthInfo(4, "Very strong", "Excellent. This password is suitable for protected PDFs.")
        };
    }

    public static bool IsTooShort(string password)
        => !string.IsNullOrWhiteSpace(password) && password.Trim().Length < MinimumPasswordLength;

    public static bool MeetsStrongPolicy(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var trimmedPassword = password.Trim();
        return trimmedPassword.Length >= MinimumPasswordLength
               && trimmedPassword.Any(char.IsUpper)
               && trimmedPassword.Any(char.IsLower)
               && trimmedPassword.Any(char.IsDigit)
               && trimmedPassword.Any(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
    }

    public static int CountWords(string password)
        => password
            .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;

    public static string GeneratePassphrase(int wordCount = 4)
    {
        if (wordCount < 4)
        {
            wordCount = 4;
        }

        var words = Enumerable.Range(0, wordCount)
            .Select(_ => PassphraseWords[RandomNumberGenerator.GetInt32(PassphraseWords.Length)])
            .ToArray();

        var separators = new[] { " ", "-", " " };
        var result = Capitalize(words[0]);

        for (var i = 1; i < words.Length; i++)
        {
            var separator = i - 1 < separators.Length
                ? separators[i - 1]
                : (RandomNumberGenerator.GetInt32(2) == 0 ? " " : "-");

            var nextWord = i == wordCount - 1
                ? Capitalize(words[i])
                : words[i];

            result += separator + nextWord;
        }

        var suffix = $"{RandomNumberGenerator.GetInt32(10, 100)}!";
        return result + "-" + suffix;
    }

    private static string Capitalize(string word)
        => string.IsNullOrWhiteSpace(word)
            ? string.Empty
            : char.ToUpperInvariant(word[0]) + word[1..];
}

public readonly record struct PasswordStrengthInfo(int Score, string Label, string Guidance);
