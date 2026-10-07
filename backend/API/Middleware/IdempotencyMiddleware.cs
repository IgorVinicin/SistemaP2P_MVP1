using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace backend.API.Middleware;

public class IdempotencyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        var rawKey = context.Request.Headers["X-Idempotency-Key"].ToString().Trim();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(rawKey) || userId == null || !context.Request.HasJsonContentType() ||
            !new[] { "POST", "PUT", "PATCH", "DELETE" }.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }
        if (rawKey.Length > 200)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { message = "Chave de idempotência muito longa." });
            return;
        }
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(context.RequestAborted);
        context.Request.Body.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var scopedKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{userId}|{context.Request.Method}|{context.Request.Path}{context.Request.QueryString}|{rawKey}")));
        // Trava por sessão de banco cobre commits dos serviços chamados.
        var connection = dbContext.Database.IsNpgsql() ? dbContext.Database.GetDbConnection() : null;
        bool openedHere = connection != null && connection.State != ConnectionState.Open;
        long lockKey = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(scopedKey)), 0);
        var originalStream = context.Response.Body;
        bool locked = false;
        try
        {
            if (connection != null)
            {
                if (openedHere) await connection.OpenAsync(context.RequestAborted);
                await ExecuteLockAsync(connection, "pg_advisory_lock", lockKey, context.RequestAborted);
                locked = true;
            }
            var record = await dbContext.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == scopedKey, context.RequestAborted);
            if (record != null)
            {
                if (record.RequestHash != hash)
                {
                    context.Response.StatusCode = 409;
                    await context.Response.WriteAsJsonAsync(new { message = "Esta chave já foi usada com outros dados." });
                    return;
                }
                context.Response.StatusCode = record.StatusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(record.ResponseBody);
                return;
            }
            await using var responseStream = new MemoryStream();
            context.Response.Body = responseStream;
            await next(context);
            responseStream.Position = 0;
            var responseBody = await new StreamReader(responseStream, leaveOpen: true).ReadToEndAsync();
            if (context.Response.StatusCode is >= 200 and < 300 &&
                context.Response.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                dbContext.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    IdempotencyKey = scopedKey,
                    RequestHash = hash,
                    RequestPath = context.Request.Path,
                    RequestMethod = context.Request.Method,
                    ResponseBody = responseBody,
                    StatusCode = context.Response.StatusCode
                });
                await dbContext.SaveChangesAsync(context.RequestAborted);
            }
            context.Response.Body = originalStream;
            responseStream.Position = 0;
            await responseStream.CopyToAsync(originalStream, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalStream;
            if (connection != null)
            {
                try
                {
                    if (locked && connection.State == ConnectionState.Open)
                        await ExecuteLockAsync(connection, "pg_advisory_unlock", lockKey, CancellationToken.None);
                }
                finally
                {
                    if (openedHere) await connection.CloseAsync();
                }
            }
        }
    }
    private static async Task ExecuteLockAsync(System.Data.Common.DbConnection connection,
        string function, long key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {function}(@key)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = key;
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
