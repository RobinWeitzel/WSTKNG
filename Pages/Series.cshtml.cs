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
        var series = _context.Series.Include(s => s.Chapters).Where(s => s.ID == id).FirstOrDefault();

        if(series == null) {
            return new JsonResult(new {});
        }

        // Get chapters ordered by their position in the series (oldest to newest)
        var orderedChapters = series.Chapters
            .OrderBy(c => c.Published)
            .ThenBy(c => c.Title)
            .ToList();

        // Validate chapter range
        if (startChapter < 1 || endChapter < 1 || startChapter > orderedChapters.Count || endChapter > orderedChapters.Count || startChapter > endChapter) {
            return new JsonResult(new { error = "Invalid chapter range" });
        }

        // Get the chapters in the specified range (convert to 0-based indexing)
        var selectedChapters = orderedChapters
            .Skip(startChapter - 1)
            .Take(endChapter - startChapter + 1)
            .Select(c => c.ID)
            .ToList();

        string jobId = BackgroundJob.Enqueue<Crawler>(c => c.EmailEpub(selectedChapters, null));

        return new JsonResult(new {jobId});
    }
}
