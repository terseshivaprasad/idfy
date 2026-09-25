using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Idfy.Api.Endpoints;

/// <summary>
/// DataAnnotations validation for request models and upload forms (.NET 8 minimal APIs have none built in).
/// Invalid input short-circuits with a 400 ValidationProblem keyed by property name.
/// </summary>
internal static class ValidationFilter
{
    public static EndpointFilterDelegate Factory(EndpointFilterFactoryContext factoryContext, EndpointFilterDelegate next)
    {
        // Resolved once per endpoint: which handler arguments are our own request models.
        var indexes = factoryContext.MethodInfo.GetParameters()
            .Where(p => IsValidatable(p.ParameterType))
            .Select(p => p.Position)
            .ToArray();

        if (indexes.Length == 0)
            return next;

        return async context =>
        {
            foreach (var index in indexes)
            {
                if (context.Arguments[index] is not { } argument)
                    continue;

                var results = new List<ValidationResult>();
                if (Validator.TryValidateObject(argument, new ValidationContext(argument), results, validateAllProperties: true))
                    continue;

                var errors = results
                    .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty), (r, member) => (member, message: r.ErrorMessage ?? "Invalid value."))
                    .GroupBy(e => e.member)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.message).ToArray());

                return TypedResults.ValidationProblem(errors);
            }

            return await next(context);
        };
    }

    private static bool IsValidatable(Type type) =>
        type.Namespace == typeof(Models.ValidateDocumentRequest).Namespace
        || typeof(FileUploadForm).IsAssignableFrom(type);
}
