using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WSTKNG.Models;

namespace WSTKNG.Pages;

public class TemplateSettingsModel : PageModel
{
    private readonly ILogger<TemplateSettingsModel> _logger;
    private readonly ApplicationContext _context;

    public TemplateSettingsModel(ILogger<TemplateSettingsModel> logger, ApplicationContext context)
    {
        _logger = logger;
        _context = context;
    }

    public Template Template { get; set; }

    public async void OnGet(int id)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(s => s.ID == id);

        if(template == null) {
            template = new Template();
        }

        Template = template;
    }

    public async Task<IActionResult> OnPost(Template template) {
        if(template.ID == 0) {
            _context.Templates.Add(template);
        } else {
            _context.Templates.Update(template);
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("Templates");
    }
}
