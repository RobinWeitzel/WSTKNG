using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WSTKNG.Models;

namespace WSTKNG.Pages;

public class SeriesSettingsModel : PageModel
{
    private readonly ILogger<SeriesSettingsModel> _logger;
    private readonly ApplicationContext _context;

    public SeriesSettingsModel(ILogger<SeriesSettingsModel> logger, ApplicationContext context)
    {
        _logger = logger;
        _context = context;
    }

    public List<Template> Templates { get; set; }
    public Series Series { get; set; }

    public async void OnGet(int id)
    {
        var series = await _context.Series
            .Include(s => s.Template)
            .FirstOrDefaultAsync(s => s.ID == id);

        if(series == null) {
            series = new Series();
        }

        Series = series;

        Templates = await _context.Templates
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IActionResult> OnPost(Series series) {
        if(series.ID == 0) {
            _context.Series.Add(series);
        } else {
            _context.Series.Update(series);
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("Series", new { id = series.ID });
    }

     public async Task<IActionResult> OnPostDelete(int id) {
        var series = await _context.Series.FindAsync(id);

        _context.Series.Remove(series);

        await _context.SaveChangesAsync();

        return RedirectToPage("Index");
    }
}
