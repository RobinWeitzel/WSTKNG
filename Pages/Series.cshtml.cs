using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Utilities;
using WSTKNG.Models;

namespace WSTKNG.Pages;

public class SeriesModel : PageModel
{
    private readonly ILogger<SeriesModel> _logger;

    private readonly ApplicationContext _context;

    public SeriesModel(ILogger<SeriesModel> logger, ApplicationContext context)
    {
        _logger = logger;
        _context = context;
    }

    public Series Series { get; set; }
    public async Task<IActionResult> OnGet(int id)
    {
        Series = await _context.Series
        .Include(s => s.Chapters)
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.ID == id);

        if(Series == null) {
            return RedirectToPage("Index");
        }

        return Page();
    }

    public ActionResult OnPostScan(int id) {
        string jobId = BackgroundJob.Enqueue<Crawler>(c => c.CheckTOC(id, null));

        return new JsonResult(new {jobId});
    }

    public ActionResult OnPostCrawl(int id) {
        string jobId = BackgroundJob.Enqueue<Crawler>(c => c.CrawlChapter(id, null));

        return new JsonResult(new {jobId});
    }

    public ActionResult OnPostChapter(int id) {
        string jobId = BackgroundJob.Enqueue<Crawler>(c => c.EmailEpub(new List<int> {id}, null));

        return new JsonResult(new {jobId});
    }

    public ActionResult OnPostSeries(int id) {
        var series = _context.Series.Include(s => s.Chapters).Where(s => s.ID == id).FirstOrDefault();

        if(series == null) {
            return new JsonResult(new {});
        }

        string jobId = BackgroundJob.Enqueue<Crawler>(c => c.EmailEpub(series.Chapters.Select(c => c.ID).ToList(), null));

        return new JsonResult(new {jobId});
        
    }

    public ActionResult OnPostRange(int id, int startChapter, int endChapter) {
        try 
        {
            // Input validation
            if (id <= 0)
            {
                _logger.LogWarning("Invalid series ID provided: {SeriesId}", id);
                return new JsonResult(new { error = "Invalid series ID" });
            }

            if (startChapter <= 0 || endChapter <= 0)
            {
                _logger.LogWarning("Invalid chapter range provided: start={StartChapter}, end={EndChapter}", startChapter, endChapter);
                return new JsonResult(new { error = "Chapter numbers must be greater than 0" });
            }

            if (startChapter > endChapter)
            {
                _logger.LogWarning("Invalid chapter range: start chapter {StartChapter} is greater than end chapter {EndChapter}", startChapter, endChapter);
                return new JsonResult(new { error = "Start chapter must be less than or equal to end chapter" });
            }

            var series = _context.Series.Include(s => s.Chapters).Where(s => s.ID == id).FirstOrDefault();

            if(series == null) {
                _logger.LogWarning("Series not found with ID: {SeriesId}", id);
                return new JsonResult(new { error = "Series not found" });
            }

        // Get chapters ordered by their position in the series (oldest to newest)
        var orderedChapters = series.Chapters
            .OrderBy(c => c.Published)
            .ThenBy(c => c.Title)
            .ToList();

            // Additional validation for chapter range
            if (startChapter > orderedChapters.Count || endChapter > orderedChapters.Count) {
                _logger.LogWarning("Chapter range exceeds available chapters. Requested: {StartChapter}-{EndChapter}, Available: {TotalChapters}", 
                    startChapter, endChapter, orderedChapters.Count);
                return new JsonResult(new { error = $"Invalid chapter range. Series has {orderedChapters.Count} chapters." });
            }

            // Get the chapters in the specified range (convert to 0-based indexing)
            var selectedChapters = orderedChapters
                .Skip(startChapter - 1)
                .Take(endChapter - startChapter + 1)
                .Select(c => c.ID)
                .ToList();

            _logger.LogInformation("Enqueuing chapter range {StartChapter}-{EndChapter} for series {SeriesId} - {SeriesName}", 
                startChapter, endChapter, series.ID, series.Name);

            string jobId = BackgroundJob.Enqueue<Crawler>(c => c.EmailEpub(selectedChapters, null));

            return new JsonResult(new {jobId});
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing chapter range request for series {SeriesId}, chapters {StartChapter}-{EndChapter}", 
                id, startChapter, endChapter);
            return new JsonResult(new { error = "An error occurred while processing your request" });
        }
    }
}
