using GenxAi_Solutions_V1.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GenxAi_Solutions_V1.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class SemanticController : ControllerBase
    {
        private readonly IBackgroundJobQueue _queue;
        private readonly IJobStore _store;
        private readonly ISemanticSeeder _seeder;
        private readonly ILogger<SemanticController> _log;

        public SemanticController(IBackgroundJobQueue queue, IJobStore store, ISemanticSeeder seeder, ILogger<SemanticController> log)
        {
            _queue = queue; _store = store; _seeder = seeder;
            _log = log;
        }

        //// POST /api/semantic/start-seed?companyId=123
        //[HttpPost("start-seed")]
        //public IActionResult StartSeed([FromQuery] int companyId)
        //{
        //    var jobId = _store.Create("SeedSemantic", companyId);
        //    //_ = _queue.EnqueueAsync(jobId, ct => _seeder.RunSeedAsync(companyId, ct));

        //    _ = _queue.EnqueueAsync(jobId, async ct =>
        //    {
        //        // Run schema/columns/prompts seeding
        //        await _seeder.RunSeedAsync(companyId, ct);
        //        // Run PDF prompts/doc seeding
        //        await _seeder.RunSeedPDFAsync(companyId, ct);
        //    });

        //    return Ok(new { jobId });
        //}

        // POST /api/semantic/start-db-seed?companyId=123
        [HttpPost("start-db-seed")]
        public IActionResult StartDbSeed([FromQuery] int companyId)
        {
            var jobId = _store.Create("DbSeed", companyId);
            //_ = _queue.EnqueueAsync(jobId, ct => _seeder.RunSeedAsync(companyId, ct));
            _ = _queue.EnqueueAsync(jobId, ct => _seeder.RunSeedAsyncNew(companyId, ct));
            return Ok(new { jobId, message = "DB seeding queued." });
        }

        // POST /api/semantic/start-pdf-seed?companyId=123
        [HttpPost("start-pdf-seed")]
        public IActionResult StartPdfSeed([FromQuery] int companyId)
        {
            try
            {
                var jobId = _store.Create("PdfSeed", companyId);
                //_ = _queue.EnqueueAsync(jobId, ct => _seeder.RunSeedPDFAsync(companyId, ct));
                _ = _queue.EnqueueAsync(jobId, ct => _seeder.RunSeedPDFAsync_New(companyId, ct));
                return Ok(new { jobId, message = "PDF seeding queued." });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "StartPdfSeed failed for company {CompanyId}", companyId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "PDF seeding failed." });
            }
        }

        // POST /api/semantic/start-all-seed?companyId=123
        [HttpPost("start-all-seed")]
        public IActionResult StartAllSeed([FromQuery] int companyId)
        {
            try
            {
                var jobId = _store.Create("AllSeed", companyId);
                _ = _queue.EnqueueAsync(jobId, async ct =>
                {
                    try
                    {
                       // await _seeder.RunSeedAsync(companyId, ct);      // DB/tables/prompts
                        await _seeder.RunSeedAsyncNew(companyId, ct);      // DB/tables/prompts
                        await _seeder.RunSeedPDFAsync_New(companyId, ct);    // PDF/doc prompts
                    }
                    catch (Exception exInner)
                    {
                        // log the failure inside the queued work too
                        _log.LogError(exInner, "StartAllSeed queued work failed for company {CompanyId}", companyId);
                        throw;
                    }
                });
                return Ok(new { jobId, message = "All seeding queued." });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "StartAllSeed failed for company {CompanyId}", companyId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "All seeding failed." });
            }
        }

        // GET /api/semantic/status/{jobId}
        [HttpGet("status/{jobId:guid}")]
        public IActionResult Status([FromRoute] Guid jobId)
        {
            try
            {
                var info = _store.Get(jobId);
            if (info is null) return NotFound(new { message = "Job not found" });
            return Ok(new
            {
                info.JobId,
                info.Type,
                info.CompanyId,
                status = info.Status.ToString(),
                info.Error,
                info.CreatedAt,
                info.StartedAt,
                info.CompletedAt
            });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Status read failed for job {JobId}", jobId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to get job status." });
            }
        }

        //// POST /api/semantic/seed/123
        //[HttpPost("seed/{companyId:int}")]
        //public async Task<IActionResult> SeedForCompany([FromRoute] int companyId)
        //{
        //    try
        //    {
        //        if (companyId <= 0) return BadRequest(new { message = "Invalid company id." });

        //        // record a job
        //        var jobId = _store.Create(type: "SemanticSeed", companyId: companyId);

        //        // enqueue work
        //        await _queue.EnqueueAsync(jobId, async (ct) =>
        //        {
        //            _store.MarkRunning(jobId);
        //            try
        //            {
        //                //await _seeder.RunSeedAsync(companyId, ct);
        //                await _seeder.RunSeedAsyncNew(companyId, ct);
        //                _store.MarkSucceeded(jobId);
        //            }
        //            catch (Exception ex)
        //            {
        //                _log.LogError(ex, "SeedForCompany queued work failed for company {CompanyId}", companyId);
        //                _store.MarkFailed(jobId, ex.Message);
        //            }
        //        });

        //        return Ok(new { jobId, message = "Seeding queued." });
        //    }
        //    catch (Exception ex)
        //    {
        //        _log.LogError(ex, "SeedForCompany failed for company {CompanyId}", companyId);
        //        return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Seeding failed." });
        //    }
        //}

        // GET /api/semantic/job/{jobId}
        [HttpGet("job/{jobId:guid}")]
        public IActionResult GetJob([FromRoute] Guid jobId)
        {
            try
            {
                var info = _store.Get(jobId);
            if (info == null) return NotFound();
            return Ok(new
            {
                info.JobId,
                info.Type,
                info.CompanyId,
                status = info.Status.ToString(),
                info.Error,
                info.CreatedAt,
                info.StartedAt,
                info.CompletedAt
            });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetJob failed for job {JobId}", jobId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to get job." });
            }
        }
    }
}
