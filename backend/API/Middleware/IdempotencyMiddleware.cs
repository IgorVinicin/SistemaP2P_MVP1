using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace backend.API.Middleware
{
    public class IdempotencyMiddleware
    {
        private readonly RequestDelegate _next;

        public IdempotencyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
        {
            // Verificar se a requisição possui o cabeçalho X-Idempotency-Key
            if (context.Request.Headers.TryGetValue("X-Idempotency-Key", out var idempotencyKeyValues) &&
                !string.IsNullOrWhiteSpace(idempotencyKeyValues.ToString()))
            {
                string idempotencyKey = idempotencyKeyValues.ToString().Trim();

                // Verificar se a chave já existe no banco de dados
                var existingRecord = await dbContext.IdempotencyRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey);

                if (existingRecord != null)
                {
                    // Reenviar a resposta armazenada para evitar execução duplicada de lançamentos financeiros
                    context.Response.StatusCode = existingRecord.StatusCode;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(existingRecord.ResponseBody);
                    return;
                }

                // Interceptar o stream da resposta para gravar o resultado
                var originalBodyStream = context.Response.Body;
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                await _next(context);

                memoryStream.Position = 0;
                string responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);

                // Gravar o registro se o código HTTP for sucesso (2xx)
                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
                {
                    try
                    {
                        var record = new IdempotencyRecord
                        {
                            Id = Guid.NewGuid(),
                            IdempotencyKey = idempotencyKey,
                            RequestPath = context.Request.Path,
                            RequestMethod = context.Request.Method,
                            ResponseBody = responseBody,
                            StatusCode = context.Response.StatusCode,
                            CreatedAt = DateTime.UtcNow
                        };

                        dbContext.IdempotencyRecords.Add(record);
                        await dbContext.SaveChangesAsync();
                    }
                    catch
                    {
                        // Ignorar falha de inserção concorrente silenciosamente
                    }
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}
