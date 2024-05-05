using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WSTKNG.Models;

namespace WSTKNG.Pages;

public class ChapterModel : PageModel
{
    private readonly ILogger<ChapterModel> _logger;
    private readonly ApplicationContext _context;

    public ChapterModel(ILogger<ChapterModel> logger, ApplicationContext context)
    {
        _logger = logger;
        _context = context;
    }

    public Chapter Chapter { get; set; }

    public async Task<IActionResult> OnGet(int id, int chapterId)
    {
        var chapter = await _context.Chapters.FindAsync(chapterId);

        if(chapter == null) {
            return RedirectToPage("Series", new {id = id});
        }

        Chapter = chapter;

        return Page();
    }

    public ActionResult OnPostCrawl(int id, int chapterId) {
        Request.Headers.TryGetValue("ChapterPassword", out var password);

        if(!password.ToString().Equals("")) {
            var chapter = _context.Chapters.Find(chapterId);
            if(chapter != null) {
                chapter.Password = password.ToString();
                _context.SaveChanges();
            }
        }
        string jobId = BackgroundJob.Enqueue<Crawler>(c => c.CrawlChapter(chapterId, null));

        return new JsonResult(new {jobId});
    }
}
