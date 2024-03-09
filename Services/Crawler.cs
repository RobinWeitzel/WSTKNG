using HtmlAgilityPack;
using Cepub;
using System;

public class Crawler
{
  private const List<string> SPECIALCHARACTERS = new List<string> { "\\", "/", "*", "?", "\"", "<", ">", "|", ":" }
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

  [AutomaticRetry(Attempts = 0)]
  public async Task CheckTOC(int? id, PerformContext pc)
  {
    using (HangfireConsoleLogger.InContext(pc))
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
          var htmlWeb = new HtmlWeb();

          var toc = htmlWeb.Load(s.TocUrl);
          var entries = toc.DocumentNode.QuerySelectorAll(selector);

          List<TOCResult> chapters = new List<TOCResult>();

          foreach (var entry in entries)
          {
            string href = HtmlEntity.DeEntitize(entry.Attributes["href"].Value);
            string text = HtmlEntity.DeEntitize(entry.InnerText);

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
            if (!context.Chapters.Where(c => c.URL.Equals(Url)).Any())
            {
              _logger.LogInformation("Found new chapter \"" + chapter.Title + "\" for series \"" + s.Name + "\"");

              // create new chapter
              Chapter newChapter = new Chapter
              {
                Published = DateTime.UtcNow,
                Title = r.Title.Replace("\n", "").Trim(),
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
    using (HangfireConsoleLogger.InContext(pc))
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        Chapter chapter = await context.Chapters
            .Include(c => c.Series)
            .FirstOrDefaultAsync(c => c.ID == id);

        if (chapter == null) return;

        _logger.LogInformation("Crawling chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");

        var htmlWeb = new HtmlWeb();
        var doc = htmlWeb.Load(chapter.URL);

        string selector = chapter.Series.Template != null ? chapter.Series.Template.ContentSelector : chapter.Series.ContentSelector;

        string content = HtmlEntity.DeEntitize(doc.DocumentNode.QuerySelector(selector).InnerHtml);

        if (content == null && content.Length == 0)
        {
          _logger.LogError("Could not find content for chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");
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
  public async Task CreateEpub(int seriesId, List<int> chapterIds, PerformContext pc)
  {
    using (HangfireConsoleLogger.InContext(pc))
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        Series series = await context.Series
            .Include(s => s.Chapters)
            .FirstOrDefaultAsync(s => s.ID == seriesId);

        if (series == null)
        {
          _logger.LogError("Series with id " + seriesId.ToString() + " does not exist");
          return;
        }


        bool seriesEpub = chapterIds == null || chapterIds.Count == 0;

        if(seriesEpub) {
          _logger.LogInformation("Creating epub for series \"" + series.Name + "\"");
        } else {
          _logger.LogInformation("Creating epub for series \"" + series.Name + "\" with chapters " + string.Join(", ", chapterIds));
        }
        
        if (chapterIds == null || chapterIds.Count == 0)
        {
          chapterIds = series.Chapters.Select(c => c.ID).ToList();
        }

        Epub epub = new Epub();
        epub.Title = series.Title;
        epub.Author = series.AuthorName;
        epub.Date = series.Chapters.Where(c => c.Crawled).OrderByDescending(c => c.Published).FirstOrDefault().Published;

        foreach (int id in chapterIds)
        {
          Chapter chapter = await context.Chapters
            .Include(c => c.Series)
            .FirstOrDefaultAsync(c => c.ID == id);

          if (chapter == null)
          {
            _logger.LogError("Chapter with id " + id.ToString() + " does not exist");
            return;
          }

          if(!chapter.Crawled) {
            _logger.LogInformation("Chapter" + chapter.Title + " has not been crawled yet");
            continue;
          }

          _logger.LogInformation("Adding chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");

          epub.AddChapter(chapter.Title, chapter.Content);
        }

        if(seriesEpub) {
          epub.Save(Path.Combine(basePath, series.Name), "series_" + series.ID.ToString() + ".epub");
        } else {
          epub.Save(Path.Combine(basePath, series.Name), "chapters_" + string.Join("_", chapterIds) + ".epub");
        }
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
    using (HangfireConsoleLogger.InContext(pc))
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        Chapter chapter = await context.Chapters
            .Include(c => c.Series)
            .FirstOrDefaultAsync(c => c.ID == id);

        if (chapter == null)
        {
          _logger.LogError("Chapter with id " + id.ToString() + " does not exist");
          return;
        }

        CreateEpub(chapter.Series.ID, new List<int> { id }, pc);
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error creating epub");
        _logger.LogError(e.Message);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task CreateEpub(List<int> ids, PerformContext pc)
  {
    using (HangfireConsoleLogger.InContext(pc))
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        if (ids == null || ids.Count == 0)
        {
          _logger.LogError("No chapters to create epub for");
          return;
        }

        Chapter firstChapter = await context.Chapters
            .Include(c => c.Series)
            .FirstOrDefaultAsync(c => c.ID == ids[0]);

        if (firstChapter == null)
        {
          _logger.LogError("First chapter does not exist");
          return;
        }

        CreateEpub(firstChapter.Series.ID, ids, pc);
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error creating epub");
        _logger.LogError(e.Message);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task EmailEpub(int id, bool series, PerformContext pc)
  {
    using (HangfireConsoleLogger.InContext(pc))
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        Chapter chapter = await context.Chapters
            .Include(c => c.Series)
            .FirstOrDefaultAsync(c => c.ID == id);

        if (chapter == null) return;

        _logger.LogInformation("Emailing chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");



      }
      catch (System.Exception e)
      {
        _logger.LogError("Error emailing chapter");
        _logger.LogError(e.Message);
      }
    }
  }
}