using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Shared.Attributes
{
    /// <summary>
    /// Validates Egyptian mobile phone numbers (010, 011, 012, 015 + 8 digits)
    /// </summary>
    public class EgyptianPhoneAttribute : ValidationAttribute
    {
        private static readonly Regex PhoneRegex = new(@"^(010|011|012|015)\d{8}$", RegexOptions.Compiled);

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null) return ValidationResult.Success; // use [Required] separately

            if (value is string phone && PhoneRegex.IsMatch(phone))
                return ValidationResult.Success;

            return new ValidationResult(
                ErrorMessage ?? "Please enter a valid Egyptian phone number (e.g., 01012345678)");
        }
    }

    /// <summary>
    /// Validates maximum file size in bytes
    /// </summary>
    public class MaxFileSizeAttribute : ValidationAttribute
    {
        private readonly long _maxBytes;

        public MaxFileSizeAttribute(long maxBytes)
        {
            _maxBytes = maxBytes;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is IFormFile file && file.Length > _maxBytes)
            {
                var mb = _maxBytes / (1024.0 * 1024.0);
                return new ValidationResult(
                    ErrorMessage ?? $"File size cannot exceed {mb:0.##} MB");
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that uploaded file has an allowed extension
    /// </summary>
    public class AllowedExtensionsAttribute : ValidationAttribute
    {
        private readonly string[] _extensions;

        public AllowedExtensionsAttribute(string[] extensions)
        {
            _extensions = extensions.Select(e => e.ToLowerInvariant()).ToArray();
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is IFormFile file)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!_extensions.Contains(ext))
                {
                    return new ValidationResult(
                        ErrorMessage ?? $"Allowed file types: {string.Join(", ", _extensions)}");
                }
            }
            return ValidationResult.Success;
        }
    }
}
