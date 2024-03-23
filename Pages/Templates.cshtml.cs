using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WSTKNG.Models;

namespace WSTKNG.Pages;

public class TemplatesModel : PageModel
{
    private readonly ILogger<TemplatesModel> _logger;
    private readonly ApplicationContext _context;


    public TemplatesModel(ILogger<TemplatesModel> logger, ApplicationContext context)
    {
        _logger = logger;
        _context = context;
    }

    public List<Template> Templates { get; set; }

    public async void OnGet()
    {
        Templates = await _context.Templates
            .Include(t => t.Series)
            .AsNoTracking()
            .ToListAsync();
    }
}
