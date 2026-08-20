using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace backend.API.Middleware
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro Interno no Servidor",
                Detail = exception.Message,
                Instance = httpContext.Request.Path,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            };

            if (exception is InvalidOperationException || exception is ArgumentException)
            {
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Solicitação Inválida";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
            }
            else if (exception is UnauthorizedAccessException)
            {
                problemDetails.Status = StatusCodes.Status401Unauthorized;
                problemDetails.Title = "Não Autorizado";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.2";
            }

            httpContext.Response.StatusCode = problemDetails.Status.Value;
            httpContext.Response.ContentType = "application/problem+json";

            var json = JsonSerializer.Serialize(problemDetails);
            await httpContext.Response.WriteAsync(json, cancellationToken);

            return true;
        }
    }
}
