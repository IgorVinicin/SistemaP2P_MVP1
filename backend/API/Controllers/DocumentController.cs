using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/documents")]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DocumentController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocument([FromForm] IFormFile file, [FromForm] string tipoDocumento)
        {
            try
            {
                // Obter ID do usuário a partir dos claims do JWT
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized(new { message = "Usuário não autenticado ou inválido." });
                }

                // Verificar se o usuário existe
                var user = await _context.Users
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound(new { message = "Usuário não encontrado." });
                }

                // Validar o tipo de documento
                var tiposValidos = new[] { "Frente", "Verso", "Selfie", "FachadaEmpresa" };
                if (!tiposValidos.Contains(tipoDocumento))
                {
                    return BadRequest(new { message = "Tipo de documento inválido. Deve ser Frente, Verso, Selfie ou FachadaEmpresa." });
                }

                if (tipoDocumento == "FachadaEmpresa" && (user.Profile == null || !user.Profile.PossuiEmpresa))
                {
                    return BadRequest(new { message = "Este usuário não declarou possuir empresa e não precisa enviar este documento." });
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "Nenhum arquivo enviado." });
                }
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
                if (file.Length > 10 * 1024 * 1024 || !allowedExtensions.Contains(extension))
                    return BadRequest(new { message = "Envie imagem JPG, PNG, WebP ou PDF de até 10 MB." });
                var signature = new byte[12];
                await using (var input = file.OpenReadStream())
                {
                    var read = await input.ReadAsync(signature);
                    bool valid = extension switch
                    {
                        ".jpg" or ".jpeg" => read >= 3 && signature[0] == 0xff && signature[1] == 0xd8 && signature[2] == 0xff,
                        ".png" => read >= 8 && signature.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                        ".pdf" => read >= 5 && System.Text.Encoding.ASCII.GetString(signature, 0, 5) == "%PDF-",
                        ".webp" => read >= 12 && System.Text.Encoding.ASCII.GetString(signature, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(signature, 8, 4) == "WEBP",
                        _ => false
                    };
                    if (!valid) return BadRequest(new { message = "O conteúdo do arquivo não corresponde ao formato informado." });
                }

                // Definir diretório de uploads local (para desenvolvimento)
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Gerar nome único para o arquivo
                var fileExtension = extension;
                var fileName = $"{userId}_{tipoDocumento}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Salvar o arquivo no disco
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Verificar se já existe um documento do mesmo tipo para atualizar ou criar novo
                var existingDocument = await _context.Documents
                    .FirstOrDefaultAsync(d => d.UserId == userId && d.TipoDocumento == tipoDocumento);
                var previousPath = existingDocument?.CaminhoArquivo;

                if (existingDocument != null)
                {
                    existingDocument.CaminhoArquivo = filePath;
                    existingDocument.Status = "Pendente";
                    existingDocument.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    var doc = new Document
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        TipoDocumento = tipoDocumento,
                        CaminhoArquivo = filePath,
                        Status = "Pendente"
                    };
                    _context.Documents.Add(doc);
                }

                // Atualizar o status do usuário para "EmAnalise" se os 3 (ou 4 se empresa) documentos obrigatórios forem enviados
                await _context.SaveChangesAsync();
                if (!string.IsNullOrEmpty(previousPath) && IsStoredUpload(previousPath))
                {
                    try { System.IO.File.Delete(previousPath); }
                    catch (IOException) { /* O arquivo novo já foi persistido; limpeza pode ser retomada. */ }
                    catch (UnauthorizedAccessException) { }
                }

                var docsEnviados = await _context.Documents
                    .Where(d => d.UserId == userId)
                    .Select(d => d.TipoDocumento)
                    .ToListAsync();

                bool temFrente = docsEnviados.Contains("Frente");
                bool temVerso = docsEnviados.Contains("Verso");
                bool temSelfie = docsEnviados.Contains("Selfie");
                bool temFachada = docsEnviados.Contains("FachadaEmpresa");

                bool requisitosPreenchidos = false;
                if (user.Profile != null && user.Profile.PossuiEmpresa)
                {
                    requisitosPreenchidos = temFrente && temVerso && temSelfie && temFachada;
                }
                else
                {
                    requisitosPreenchidos = temFrente && temVerso && temSelfie;
                }

                if (requisitosPreenchidos && (user.Status == "Pendente" || user.Status == "Reprovado"))
                {
                    user.Status = "EmAnalise";
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    message = "Documento enviado com sucesso.",
                    tipoDocumento,
                    caminhoLocal = fileName,
                    userStatus = user.Status
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetDocumentsStatus()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var userDocs = await _context.Documents
                .Where(d => d.UserId == userId)
                .Select(d => new
                {
                    d.Id,
                    d.TipoDocumento,
                    d.Status,
                    d.CreatedAt
                })
                .ToListAsync();

            return Ok(userDocs);
        }

        [HttpGet("{id}/file")]
        public async Task<IActionResult> GetOwnDocument(Guid id)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
            var document = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
            if (document == null || !IsStoredUpload(document.CaminhoArquivo) || !System.IO.File.Exists(document.CaminhoArquivo))
                return NotFound(new { message = "Documento não encontrado." });
            var type = Path.GetExtension(document.CaminhoArquivo).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp",
                ".pdf" => "application/pdf", _ => "application/octet-stream"
            };
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return PhysicalFile(Path.GetFullPath(document.CaminhoArquivo), type);
        }

        private static bool IsStoredUpload(string path)
        {
            var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "uploads")) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }
}
