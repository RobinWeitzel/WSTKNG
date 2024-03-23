using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WSTKNG.Models;

namespace WSTKNG.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ApplicationContext _context;

    public IndexModel(ILogger<IndexModel> logger, ApplicationContext context)
    {
        _logger = logger;
        _context = context;
    }
    
    public IList<Series> Series { get; set; }
    public int ChapterCount { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }

    public async void OnGetAsync()
    {
        Series = await _context.Series
                .Include(s => s.Template)
                .AsNoTracking()
                .ToListAsync();

        ChapterCount = _context.Chapters.Count();

        var monitor = JobStorage.Current.GetMonitoringApi();

        IDictionary<DateTime, long> succeeded = monitor.SucceededByDatesCount();
        IDictionary<DateTime, long> failed = monitor.FailedByDatesCount();

        Succeeded = succeeded.OrderByDescending(s => s.Key).First().Value;
        Failed = failed.OrderByDescending(s => s.Key).First().Value;
    }
}
