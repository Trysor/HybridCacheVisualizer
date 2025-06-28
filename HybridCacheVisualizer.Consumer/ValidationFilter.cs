using System.ComponentModel.DataAnnotations;

namespace HybridCacheVisualizer.Consumer;

/// <summary>
/// Represents an endpoint filter that validates the request body of type <typeparamref name="T"/>.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        T? bodyObject = context.GetArgument<T>(0);
        if (bodyObject is null)
            return new(TypedResults.BadRequest());

        var validationContext = new ValidationContext(bodyObject);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(bodyObject, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = validationResults.ToDictionary(
                vr => vr.MemberNames.FirstOrDefault() ?? "Unknown",
                vr => new[] { vr.ErrorMessage ?? "Validation error" }
            );

            return new(TypedResults.ValidationProblem(errors));
        }

        return next(context);
    }
}
