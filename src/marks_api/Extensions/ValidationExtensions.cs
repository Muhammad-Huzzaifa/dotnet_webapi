using System.ComponentModel.DataAnnotations;

namespace marks_api.Extensions;

public static class ValidationExtensions
{
    public static bool TryValidate(
        this object model,
        out Dictionary<string, string[]> errors)
    {
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        errors = results
            .SelectMany(result => result.MemberNames
                .Where(member => !string.IsNullOrEmpty(member))
                .Select(member => new
                {
                    Member = char.ToLowerInvariant(member[0]) + member[1..],
                    result.ErrorMessage
                }))
            .GroupBy(x => x.Member)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => x.ErrorMessage ?? "Invalid value.")
                    .Distinct()
                    .ToArray());

        return isValid;
    }
}
