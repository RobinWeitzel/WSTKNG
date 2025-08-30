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
using System.Net;

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

  private async Task<IHtmlDocument> GetPage(string url, string CookieName, string CookieValue)
  {
    HttpClientHandler handler = new HttpClientHandler();
    if (CookieName != null && CookieValue != null && !CookieName.Equals(""))
    {
      handler.CookieContainer = new CookieContainer();
      Cookie cookie = new Cookie(CookieName, CookieValue) { Domain = new Uri(url).Host };
      handler.CookieContainer.Add(cookie);
    }
    HttpClient httpClient = new HttpClient(handler);
    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

    HttpResponseMessage request = await httpClient.GetAsync(url);
    Stream response = await request.Content.ReadAsStreamAsync();

    HtmlParser parser = new HtmlParser();
    IHtmlDocument document = parser.ParseDocument(response);

    return document;
  }

  /**
    * Posts a form to a url and returns the value of a header
    * @param url The url to post to
    * @param formName The name of the form field
    * @param formValue The value of the form field
    * @param CookieName The name of the header to return 
    * @return The header
  */
  private async Task<IEnumerable<string>> GetCookies(string url, string formName, string formValue, string CookieName)
  {
    HttpClient httpClient = new HttpClient();

    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

    HttpResponseMessage request = await httpClient.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string> {
      { formName, formValue }
    }));

    return request.Headers.GetValues("Set-Cookie");
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

          IHtmlDocument document = await this.GetPage(s.TocUrl, null, null);

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
            string title = chapter.Title.Replace("\n", "").Replace("&", "and").Trim();

            if (!Url.StartsWith("http") && s.TocUrl.StartsWith("https://www.royalroad.com"))
            {
              Url = "https://www.royalroad.com" + chapter.Url;
            }

            if (!Url.StartsWith("https"))
            {
              Url = "https://" + chapter.Url;
            }

            // check whether chapter exists in DB
            if (!context.Chapters.Where(c => (c.URL.Equals(Url) || c.Title.Equals(title)) && c.SeriesID == s.ID).Any())
            {
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

        if (!string.IsNullOrEmpty(chapter.Password) && chapter.Series.Template.Name.Equals("Wordpress"))
        {
          string baseUrl = "https://" + new Uri(chapter.Series.TocUrl).Host + "/wp-login.php?action=postpass&wpe-login=true";
          var cookies = await GetCookies(baseUrl, "post_password", chapter.Password, "wp-postpass_");
          foreach (var cookie in cookies)
          {
            string name = cookie.Split("=")[0];
            string value = cookie.Split("=")[1].Split(";")[0];

            if (name.StartsWith("wp-postpass_"))
            {
              chapter.CookieName = name;
              chapter.CookieValue = value;
              break;
            }
          }
          await context.SaveChangesAsync();
        }
        IHtmlDocument document = null;

        if (chapter.CookieName != null && chapter.CookieValue != null)
        {
          document = await GetPage(chapter.URL, chapter.CookieName, chapter.CookieValue);
        }
        else
        {
          document = await GetPage(chapter.URL, null, null);
        }

        // https://wanderinginn.com/wp-login.php?action=postpass&wpe-login=true post
        // name='post_password'
        // wp-postpass_1066c31e854ee525207c99d9fecb9fc0 (last part is dynamic)

        string selector = chapter.Series.Template != null ? chapter.Series.Template.ContentSelector : chapter.Series.ContentSelector;

        var ps = document.QuerySelectorAll(selector);

        if (ps == null || !ps.Any())
        {
          _logger.LogError("Could not find content for chapter \"" + chapter.Title + "\" for series \"" + chapter.Series.Name + "\"");
          return;
        }

        bool isRoyalRoad = false;
        string className = "";

        if (chapter.Series.Template != null && chapter.Series.Template.Name == "RoyalRoad")
        {
          isRoyalRoad = true;
          var styles = document.QuerySelectorAll("style");

          foreach (var style in styles)
          {
            if (style.InnerHtml.Contains("display: none;"))
            {
              className = style.InnerHtml.Split("{")[0].Replace(".", "").Trim();
            }
          }
        }

        string content = "<h1>" + chapter.Title + "</h1>";

        foreach (var p in ps)
        {
          if (!p.InnerHtml.Contains("Next Chapter") && !p.InnerHtml.Contains("Previous Chapter") && !p.InnerHtml.Contains("<img") && !p.InnerHtml.Contains("<img") && (!isRoyalRoad || !p.ClassList.Contains(className)))
          {
            content += p.OuterHtml.Replace("&nbsp;", "");
          }
        }

        if (content.Length < 100)
        {
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
      foreach (int id in ids)
      {
        await CrawlChapter(id, pc);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task<string> CreateEpub(List<int> chapterIds, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        List<WSTKNG.Models.Chapter> chapters = await context.Chapters
            .Include(c => c.Series)
            .Where(c => chapterIds.Contains(c.ID))
            .OrderBy(c => c.Published)
            .ToListAsync();

        if (!chapters.Any())
        {
          _logger.LogError("No chapters to crawl");
          return null;
        }

        var series = chapters.First().Series;

        _logger.LogInformation("Creating epub for series \"" + series.Name + "\" with chapters " + string.Join(", ", chapterIds));

        Epub epub = new Epub();
        epub.Title = series.Name;
        epub.Author = series.AuthorName;
        epub.Date = chapters.FirstOrDefault().Published;

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

        string fileName = "";

        if (chapters.Count() == 1)
        {
          fileName = chapters.First().Title;
        }
        else
        {
          var allChapters = await context.Chapters.AsNoTracking().Where(c => c.SeriesID == series.ID).OrderBy(c => c.Published).Select(c => c.ID).ToListAsync();

          int start = allChapters.IndexOf(chapters.First().ID) + 1;
          int end = allChapters.IndexOf(chapters.Last().ID) + 1;

          fileName = series.Name + " - Chapters " + start + " to " + end;
        }

        if (!Directory.Exists(basePath))
        {
          Directory.CreateDirectory(basePath);
        }

        if (!Directory.Exists(Path.Combine(basePath, series.Name)))
        {
          Directory.CreateDirectory(Path.Combine(basePath, series.Name));
        }

        if (!Directory.Exists(Path.Combine(basePath, series.Name, "job_" + pc.BackgroundJob.Id.ToString())))
        {
          Directory.CreateDirectory(Path.Combine(basePath, series.Name, "job_" + pc.BackgroundJob.Id.ToString()));
        }

        if (File.Exists(Path.Combine(basePath, series.Name, "job_" + pc.BackgroundJob.Id.ToString(), fileName + ".epub")))
        {
          File.Delete(Path.Combine(basePath, series.Name, "job_" + pc.BackgroundJob.Id.ToString(), fileName + ".epub"));
        }

        epub.Save(Path.Combine(basePath, series.Name, "job_" + pc.BackgroundJob.Id.ToString()), fileName);

        return Path.Combine(basePath, series.Name, "job_" + pc.BackgroundJob.Id.ToString(), fileName + ".epub");
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error creating epub");
        _logger.LogError(e.Message);
        return null;
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task<string> CreateEpub(int id, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      try
      {
        return await CreateEpub(new List<int> { id }, pc);
      }
      catch (System.Exception e)
      {
        _logger.LogError("Error creating epub");
        _logger.LogError(e.Message);
        return null;
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

        string path = await CreateEpub(chapterIds, pc);

        if (path == null)
        {
          _logger.LogError("Error creating epub");
          return;
        }

        using (StreamReader sr = new StreamReader(path))
        {
          await _emailService.Send(Path.GetFileName(path), sr.BaseStream);
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