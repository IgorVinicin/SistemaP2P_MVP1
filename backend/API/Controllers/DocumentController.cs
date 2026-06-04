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

                // Definir diretório de uploads local (para desenvolvimento)
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Gerar nome único para o arquivo
                var fileExtension = Path.GetExtension(file.FileName);
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

                if (existingDocument != null)
                {
                    // Remover o arquivo antigo física/localmente se desejável, ou apenas atualizar
                    try
                    {
                        if (System.IO.File.Exists(existingDocument.CaminhoArquivo))
                        {
                            System.IO.File.Delete(existingDocument.CaminhoArquivo);
                        }
                    }
                    catch { /* ignorar erros ao deletar */ }

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

                if (requisitosPreenchidos && user.Status == "Pendente")
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
    }
}
