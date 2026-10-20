using System.ComponentModel.DataAnnotations;

namespace EstoqueApi.Infra;

/// <summary>
/// Minimal APIs do .NET 8 não aplicam DataAnnotations automaticamente.
/// Este filtro valida o argumento do tipo T e devolve 400 (ValidationProblem) se inválido.
/// </summary>
public class ValidacaoFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var arg = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (arg is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Corpo da requisição ausente ou inválido."] });

        var resultados = new List<ValidationResult>();
        if (!Validator.TryValidateObject(arg, new ValidationContext(arg), resultados, validateAllProperties: true))
        {
            var erros = resultados
                .SelectMany(r => r.MemberNames.DefaultIfEmpty("").Select(m => (m, r.ErrorMessage ?? "Inválido")))
                .GroupBy(x => x.m)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToArray());
            return Results.ValidationProblem(erros);
        }

        return await next(ctx);
    }
}
