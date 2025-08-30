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
  private readonly IHttpClientFactory _httpClientFactory;
  public string basePath;

  public Crawler(IServiceProvider serviceProvider, IEmailService emailService, ILogger<Crawler> logger, IHttpClientFactory httpClientFactory)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _emailService = emailService;
    _httpClientFactory = httpClientFactory;

    basePath = Path.Combine(Directory.GetCurrentDirectory(), "epubs");
  }

  private class TOCResult
  {
    public string Title { get; set; }

    public string Url { get; set; }
  }

  private async Task<IHtmlDocument> GetPage(string url, string CookieName, string CookieValue)
  {
    if (string.IsNullOrWhiteSpace(url))
    {
      _logger.LogError("GetPage called with null or empty URL");
      throw new ArgumentException("URL cannot be null or empty", nameof(url));
    }

    try
    {
      using var httpClient = _httpClientFactory.CreateClient();
      
      // Set timeout to avoid hanging requests
      httpClient.Timeout = TimeSpan.FromSeconds(30);
      
      if (CookieName != null && CookieValue != null && !CookieName.Equals(""))
      {
        var cookie = new Cookie(CookieName, CookieValue) { Domain = new Uri(url).Host };
        // Note: For cookie support with HttpClientFactory, consider using a named client with configured handler
        // For now, we'll use headers for simple cases
        httpClient.DefaultRequestHeaders.Add("Cookie", $"{CookieName}={CookieValue}");
      }
      
      httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

      using var request = await httpClient.GetAsync(url);
      
      // Ensure successful status code
      request.EnsureSuccessStatusCode();
      
      using var response = await request.Content.ReadAsStreamAsync();

      var parser = new HtmlParser();
      var document = parser.ParseDocument(response);

      return document;
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "HTTP request failed for URL: {Url}", url);
      throw new InvalidOperationException($"Failed to fetch page from {url}", ex);
    }
    catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
    {
      _logger.LogError(ex, "Request timeout for URL: {Url}", url);
      throw new TimeoutException($"Request to {url} timed out", ex);
    }
    catch (UriFormatException ex)
    {
      _logger.LogError(ex, "Invalid URL format: {Url}", url);
      throw new ArgumentException($"Invalid URL format: {url}", nameof(url), ex);
    }
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
    using var httpClient = _httpClientFactory.CreateClient();

    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

    using var content = new FormUrlEncodedContent(new Dictionary<string, string> {
      { formName, formValue }
    });

    using var request = await httpClient.PostAsync(url, content);

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
          _logger.LogInformation("Checking for updates for series {SeriesId} - {SeriesName}", s.ID, s.Name);

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

              _logger.LogInformation("Found new chapter {ChapterTitle} for series {SeriesId} - {SeriesName}", title, s.ID, s.Name);

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
      catch (Exception e)
      {
        _logger.LogError(e, "Error occurred while checking TOC for series {SeriesId}", id);
      }
    }
  }

  [AutomaticRetry(Attempts = 0)]
  public async Task CrawlChapter(int id, PerformContext pc)
  {
    using (IServiceScope scope = _serviceProvider.CreateScope())
    using (ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>())
    {
      WSTKNG.Models.Chapter? chapter = null;
      try
      {
        chapter = await context.Chapters
            .Include(c => c.Series)
            .ThenInclude(s => s.Template)
            .FirstOrDefaultAsync(c => c.ID == id);

        if (chapter == null) return;

        _logger.LogInformation("Starting to crawl chapter {ChapterId} - {ChapterTitle} for series {SeriesName}", chapter.ID, chapter.Title, chapter.Series.Name);

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
          _logger.LogError("Could not find content for chapter {ChapterId} - {ChapterTitle} for series {SeriesName}", chapter.ID, chapter.Title, chapter.Series.Name);
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
          _logger.LogWarning("Insufficient content found for chapter {ChapterId} - {ChapterTitle}. Content length: {ContentLength}", chapter.ID, chapter.Title, content?.Length ?? 0);
          return;
        }

        chapter.Content = content;
        chapter.Crawled = true;
        await context.SaveChangesAsync();

        _logger.LogInformation("Successfully crawled chapter {ChapterId} - {ChapterTitle} for series {SeriesName}. Content length: {ContentLength}", 
          chapter.ID, chapter.Title, chapter.Series.Name, content?.Length ?? 0);
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Failed to crawl chapter {ChapterId} - {ChapterTitle} for series {SeriesName}", 
          id, chapter?.Title ?? "Unknown", chapter?.Series?.Name ?? "Unknown");
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
      WSTKNG.Models.Series? series = null;
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

        series = chapters.First().Series;

        _logger.LogInformation("Creating epub for series {SeriesId} - {SeriesName} with {ChapterCount} chapters: {ChapterIds}", 
          series.ID, series.Name, chapterIds.Count, chapterIds);

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
      catch (Exception e)
      {
        _logger.LogError(e, "Failed to create epub for series {SeriesId} - {SeriesName} with chapters {ChapterIds}", 
          series?.ID ?? 0, series?.Name ?? "Unknown", chapterIds);
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
      catch (Exception e)
      {
        _logger.LogError(e, "Failed to create epub for chapter {ChapterId}", id);
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
      WSTKNG.Models.Series? series = null;
      try
      {
        var chapters = context.Chapters.Include(c => c.Series).Where(c => chapterIds.Contains(c.ID)).ToList();

        if (chapters.Count() == 0)
        {
          _logger.LogInformation("No chapters to email");
          return;
        }

        series = chapters.First().Series;
        _logger.LogInformation("Emailing {ChapterCount} chapters for series {SeriesId} - {SeriesName}", 
          chapterIds.Count, series.ID, series.Name);

        await CrawlChapters(chapters.Where(c => !c.Crawled).Select(c => c.ID).ToList(), pc);

        string path = await CreateEpub(chapterIds, pc);

        if (path == null)
        {
          _logger.LogError("Error creating epub");
          return;
        }

        await _emailService.SendFile(Path.GetFileName(path), path);

        foreach (var chapter in context.Chapters.Where(c => chapterIds.Contains(c.ID)))
        {
          chapter.Sent = true;
        }

        await context.SaveChangesAsync();
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Failed to email chapters {ChapterIds} for series {SeriesId} - {SeriesName}", 
          chapterIds, series?.ID ?? 0, series?.Name ?? "Unknown");
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
      catch (Exception e)
      {
        _logger.LogError(e, "Failed during scheduled crawl of chapters");
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
      catch (Exception e)
      {
        _logger.LogError(e, "Failed during scheduled email sending");
      }
    }
  }
}