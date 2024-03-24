using Cepub;
using System;
using Hangfire;
using Hangfire.Server;
using WSTKNG.Models;
using Microsoft.EntityFrameworkCore;
using WSTKNG.Services;
using Hangfire.Console;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;

public class Crawler
{
  private List<string> SPECIALCHARACTERS = new List<string> { "\\", "/", "*", "?", "\"", "<", ">", "|", ":" };
  private readonly ILogger _logger;
  public IServiceProvider _serviceProvider;
  public IEmailService _emailService;
  public string basePath;

  public Crawler(IServiceProvider serviceProvider, IEmailService emailService, ILogger<Crawler> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _emailService = emailService;

    basePath = Path.Combine(Directory.GetCurrentDirectory(), "epubs");
  }

  private class TOCResult
  {
    public string Title { get; set; }

    public string Url { get; set; }
  }

  private async Task<IHtmlDocument> GetPage(string url)
  {
    HttpClient httpClient = new HttpClient();

    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

    HttpResponseMessage request = await httpClient.GetAsync(url);
    Stream response = await request.Content.ReadAsStreamAsync();

    HtmlParser parser = new HtmlParser();
    IHtmlDocument document = parser.ParseDocument(response);

    return document;
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task CheckTOC(int? id, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        List<Series> Series = new List<Series>();

        if (id == null)
        {
          Series = await context.Series
              .Include(s => s.Template)
              .ToListAsync();
        }
        else
        {
          Series.Add(await context.Series
              .Include(s => s.Template)
              .FirstOrDefaultAsync(s => s.ID == id));
        }

        foreach (Series s in Series)
        {
          _logger.LogInformation("Checking for update for series \"" + s.Name + "\"");

          string selector = s.Template != null ? s.Template.TocSelector : s.TocSelector;

          IHtmlDocument document = await this.GetPage(s.TocUrl);

          var entries = document.QuerySelectorAll(selector);

          List<TOCResult> chapters = new List<TOCResult>();

          foreach (var entry in entries)
          {
            string href = entry.Attributes["href"].Value;
            string text = entry.Text();

            if (href != null && href.Length > 0 && text.Trim().Length > 0)
            {
              chapters.Add(new TOCResult
              {
                Title = text,
                Url = href
              });
            }
          }

          foreach (TOCResult chapter in chapters)
          {
            string Url = chapter.Url;

            if (!Url.StartsWith("http") && s.TocUrl.StartsWith("https://www.royalroad.com"))
            {
              Url = "https://www.royalroad.com" + chapter.Url;
            }

            if (!Url.StartsWith("https"))
            {
              Url = "https://" + chapter.Url;
            }

            // check whether chapter exists in DB
            if (!context.Chapters.Where(c => c.URL.Equals(Url) && c.SeriesID == s.ID).Any())
            {
              string title = chapter.Title.Replace("\n", "").Replace("&", "and").Trim();

              foreach (string specialCharacter in SPECIALCHARACTERS)
              {
                title = title.Replace(specialCharacter, "");
              }

              _logger.LogInformation("Found new chapter \"" + title + "\" for series \"" + s.Name + "\"");

              // create new chapter
              WSTKNG.Models.Chapter newChapter = new WSTKNG.Models.Chapter
              {
                Published = DateTime.UtcNow,
                Title = title,
                URL = Url,
                Crawled = false,
                Sent = false,
                Content = "",
                Series = s
              };

              context.Chapters.Add(newChapter);
              await context.SaveChangesAsync();
            }
          }
        }
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error checking TOC");
        _logger.LogError(e.Message);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task CrawlChapter(int id, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        WSTKNG.Models.Chapter chapter = await context.Chapters
            .Include(c => c.Series)
            .ThenInclude(s => s.Template)
            .FirstOrDefaultAsync(c => c.ID == id);

        if (chapter == null) return;

        _logger.LogInformation("Crawling chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");

        var document = await GetPage(chapter.URL);

        string selector = chapter.Series.Template != null ? chapter.Series.Template.ContentSelector : chapter.Series.ContentSelector;

        var ps = document.QuerySelectorAll(selector);

        if (ps == null || ps.Count() == 0)
        {
          _logger.LogError("Could not find content for chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");
          return;
        }

        string content = "<h1>" + chapter.Title + "</h1>";

        foreach (var p in ps)
        {
          if (!p.InnerHtml.Contains("Next Chapter") && !p.InnerHtml.Contains("Previous Chapter") && !p.InnerHtml.Contains("About") && !p.InnerHtml.Contains("<img"))
          {
            content += p.OuterHtml.Replace("&nbsp;", "");
          }
        }

        if(content.Length < 50) {
          _logger.LogWarning("Not enought content found for chapter");
          return;
        }

        chapter.Content = content;
        chapter.Crawled = true;
        await context.SaveChangesAsync();

        _logger.LogInformation("Crawled chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error getting content of chapter");
        _logger.LogError(e.Message);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task CrawlChapters(List<int> ids, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      foreach(int id in ids) {
        await CrawlChapter(id, pc);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task CreateEpub(List<int> chapterIds, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        List<WSTKNG.Models.Chapter> chapters = await context.Chapters
            .Include(c => c.Series)
            .Where(c => chapterIds.Contains(c.ID))
            .ToListAsync();

        if (chapters.Count() == 0)
        {
          _logger.LogError("No chapters to crawl");
          return;
        }

        var series = chapters.First().Series;

        _logger.LogInformation("Creating epub for series \"" + series.Name + "\" with chapters " + string.Join(", ", chapterIds));

        Epub epub = new Epub();
        epub.Title = series.Name;
        epub.Author = series.AuthorName;
        epub.Date = chapters.OrderByDescending(c => c.Published).FirstOrDefault().Published;

        foreach (var chapter in chapters)
        {
          if (!chapter.Crawled)
          {
            _logger.LogInformation("Chapter" + chapter.Title + " has not been crawled yet");
            continue;
          }

          _logger.LogInformation("Adding chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");

          epub.AddChapter(chapter.Title, chapter.Content);
        }

        if (!Directory.Exists(basePath))
        {
          Directory.CreateDirectory(basePath);
        }

        if (!Directory.Exists(Path.Combine(basePath, series.Name)))
        {
          Directory.CreateDirectory(Path.Combine(basePath, series.Name));
        }

        if (File.Exists(Path.Combine(basePath, series.Name, "job_" + pc.BackgroundJob.Id.ToString() + ".epub")))
        {
          File.Delete(Path.Combine(basePath, series.Name, "job_" + pc.BackgroundJob.Id.ToString() + ".epub"));
        }

        epub.Save(Path.Combine(basePath, series.Name), "job_" + pc.BackgroundJob.Id.ToString());
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error creating epub");
        _logger.LogError(e.Message);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task CreateEpub(int id, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        CreateEpub(new List<int> { id }, pc);
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error creating epub");
        _logger.LogError(e.Message);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task EmailEpub(List<int> chapterIds, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        var chapters = context.Chapters.Include(c => c.Series).Where(c => chapterIds.Contains(c.ID)).ToList();

        if (chapters.Count() == 0)
        {
          _logger.LogInformation("No chapters to email");
          return;
        }

        _logger.LogInformation("Emailing chapter for series \"" + chapters.First().Series.Name + "\"");

        await CrawlChapters(chapters.Where(c => !c.Crawled).Select(c => c.ID).ToList(), pc);
        
        await CreateEpub(chapterIds, pc);

        string path = Path.Combine(basePath, chapters.First().Series.Name, "job_" + pc.BackgroundJob.Id.ToString() + ".epub");

        using (StreamReader sr = new StreamReader(path))
        {
          await _emailService.Send(chapters.First().Series.Name + "_job_" + pc.BackgroundJob.Id.ToString() + ".png", sr.BaseStream);
        }

        foreach (var chapter in context.Chapters.Where(c => chapterIds.Contains(c.ID)))
        {
          chapter.Sent = true;
        }

        await context.SaveChangesAsync();
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error emailing chapter");
        _logger.LogError(e.Message);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task ScheduledCrawl(PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        foreach (Series series in context.Series.Include(s => s.Chapters))
        {
          await CrawlChapters(series.Chapters.Where(c => !c.Crawled).Select(c => c.ID).ToList(), pc);
        }
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error crawling chapters");
        _logger.LogError(e.Message);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task ScheduledEmail(PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        foreach (Series series in context.Series.Include(s => s.Chapters).Where(s => s.Active))
        {
          await EmailEpub(series.Chapters.Where(c => !c.Sent).Select(c => c.ID).ToList(), pc);
        }
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error crawling chapters");
        _logger.LogError(e.Message);
      }
    }
  }
}