using GenxAi_Solutions_V1.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using GenxAi_Solutions_V1.Dtos;
using GenxAi_Solutions_V1.Services.Interfaces;


namespace GenxAi_Solutions_V1.Api
{
    [Route("api/textQuery")]
    [ApiController]
    public class TextQueryController : ControllerBase
    {

        private readonly IVectorSemanticService _svc;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TextQueryController> _logger;

        public TextQueryController(
            IVectorSemanticService svc,
            IWebHostEnvironment env,
            ILogger<TextQueryController> logger)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Uploads a PDF and ingests it into the vector DB.
        /// </summary>
        /// <param name="file">PDF file (multipart/form-data field name: file)</param>
        [HttpPost("upload")]
        [RequestSizeLimit(524_288_000)] // 500 MB
        [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            try
            {
                var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsDir);

                var fileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                await using (var fs = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(fs);
                }

                _logger.LogInformation("Received file upload: {FileName} ({Size} bytes)", file.FileName, file.Length);

                var result = await _svc.IngestPdfAsync(filePath);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Upload failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs a semantic query against the ingested documents and returns the assistant reply plus context.
        /// </summary>
        /// <param name="req">QueryRequest JSON: { \"query\": \"...\" }</param>
        [HttpPost("query")]
        [ProducesResponseType(typeof(QueryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Query([FromBody] QueryRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Query))
                return BadRequest("Query is required.");

            try
            {
                _logger.LogInformation("Received query: {Query}", req.Query);
                var resp = await _svc.QueryAsync(req.Query);
                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Query failed");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Query failed: {ex.Message}");
            }
        }
    }
}
