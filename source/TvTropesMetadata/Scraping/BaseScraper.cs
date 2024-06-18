﻿using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Playnite.SDK.Models;
using PlayniteExtensions.Common;
using PlayniteExtensions.Metadata.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace TvTropesMetadata.Scraping
{
    public abstract class BaseScraper
    {
        protected readonly IWebDownloader downloader;
        public List<string> CategoryWhitelist = new List<string> { "VideoGame", "VisualNovel" };
        public List<string> BlacklistedWords = new List<string> { "deconstructed", "averted", "inverted", "subverted" };

        public abstract IEnumerable<TvTropesSearchResult> Search(string query);

        protected BaseScraper(IWebDownloader downloader)
        {
            this.downloader = downloader;
        }

        protected IEnumerable<TvTropesSearchResult> Search(string query, string type)
        {
            string url = $"https://tvtropes.org/pmwiki/elastic_search_result.php?q={HttpUtility.UrlEncode(query)}&page_type={type}&search_type=article";
            var doc = GetDocument(url);
            var searchResults = doc.QuerySelectorAll("a.search-result[href]");
            foreach (var a in searchResults)
            {
                var absoluteUrl = a.GetAttribute("href").GetAbsoluteUrl(url);
                var imgUrl = a.QuerySelector("img[src]")?.GetAttribute("src");
                var title = a.FirstElementChild.TextContent;
                string description = null;
                var descriptionElement = a.QuerySelector("div");
                if (descriptionElement != null)
                {
                    var childrenToRemove = descriptionElement.Children.Where(c => c.ClassName == "img-wrapper" || c.ClassName == "more-button");
                    foreach (var child in childrenToRemove)
                        descriptionElement.RemoveChild(child);

                    description = descriptionElement.TextContent.Trim();
                }
                yield return new TvTropesSearchResult
                {
                    Description = description,
                    ImageUrl = imgUrl,
                    Name = title,
                    Title = title.TrimEnd(" (VideoGame)").TrimEnd(" (VisualNovel)"),
                    Url = absoluteUrl,
                };
            }
        }

        protected IEnumerable<Tuple<string, string>> GetHeaderSegments(string content)
        {
            var headerSegments = content.Trim().Split(new[] { "<h2>" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in headerSegments)
            {
                var headerAndContent = segment.Trim().Split(new[] { "</h2>" }, StringSplitOptions.RemoveEmptyEntries);
                if (headerAndContent.Length != 2)
                    yield return new Tuple<string, string>(string.Empty, segment);
                else
                    yield return new Tuple<string, string>(headerAndContent[0].HtmlDecode(), headerAndContent[1]);
            }
        }

        protected string[] GetWikiPathSegments(string url)
        {
            var match = PathSplitter.Match(url);
            var segments = match.Groups["segment"].Captures.Cast<Capture>().Select(x => x.Value).ToArray();
            return segments;
        }

        private Regex PathSplitter = new Regex(@"pmwiki\.php(/(?<segment>\w+))+", RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        protected IHtmlDocument GetDocument(string url)
        {
            var pageSource = downloader.DownloadString(url).ResponseContent;
            var doc = new HtmlParser().Parse(pageSource);
            return doc;
        }

        protected static string GetTitle(IHtmlDocument document)
        {
            var titleElement = document.QuerySelector("h1.entry-title");
            if (titleElement == null)
                return null;

            var strong = titleElement.QuerySelector("strong");
            if(strong != null) 
                strong.Remove();

            return titleElement.TextContent.HtmlDecode();
        }
    }

    public class TvTropesSearchResult : IHasName, IGameSearchResult
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public string ImageUrl { get; set; }

        public string Name { get; set; }

        public IEnumerable<string> AlternateNames => Enumerable.Empty<string>();

        public IEnumerable<string> Platforms => Enumerable.Empty<string>();

        public ReleaseDate? ReleaseDate => null;

        public GenericItemOption<TvTropesSearchResult> ToGenericItemOption()
        {
            var id = Url.TrimStart("https://tvtropes.org/pmwiki/pmwiki.php/");
            var description = $"{id} | {Description}";

            return new GenericItemOption<TvTropesSearchResult>(this) { Description = description, Name = Title };
        }
    }

}
