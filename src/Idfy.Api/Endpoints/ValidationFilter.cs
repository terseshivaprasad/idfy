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
        var parameters = factoryContext.MethodInfo.GetParameters()
            .Where(p => IsValidatable(p.ParameterType))
            .ToArray();

        if (parameters.Length == 0)
            return next;

        return async context =>
        {
            foreach (var parameter in parameters)
            {
                if (context.Arguments[parameter.Position] is not { } argument)
                {
                    // A multipart body with no recognised field binds the whole form as null.
                    if (typeof(FileUploadForm).IsAssignableFrom(parameter.ParameterType))
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                        {
                            [nameof(FileUploadForm.File)] = [$"The {nameof(FileUploadForm.File)} field is required."],
                        });
                    continue;
                }

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
