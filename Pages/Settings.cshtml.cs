using Hangfire.Storage.SQLite.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WSTKNG.Models;

namespace WSTKNG.Pages;

public class SettingsModel : PageModel
{
    private readonly ILogger<SettingsModel> _logger;    
    private readonly ApplicationContext _context;


    public SettingsModel(ILogger<SettingsModel> logger, ApplicationContext context)
    {
        _logger = logger;
        _context = context;
    }

    public Setting Setting { get; set; }

    public async void OnGet()
    {
        var setting = await _context.Settings
            .FirstOrDefaultAsync();

        if(setting == null) {
            setting = new Setting();
        }

        Setting = setting;
    }

    public async Task<IActionResult> OnPost(Setting setting) {
        if(setting.ID == 0) {
            _context.Settings.Add(setting);
        } else {
            _context.Settings.Update(setting);
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("Settings");
    }
}
