using System;
using DotNetNuke.Entities.Modules;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.Collections.Generic;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.IO;
using System.Text;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using DotNetNuke.ComponentModel;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Sitemap;

namespace Risdall.Modules.DNN_AMP.Components
{
    public class GoogleAmpConverter
    {
        private readonly string source;

        public GoogleAmpConverter(string source)
        {
            this.source = source;
        }

        #region Conversions
        public static string Convert(string source)
        {
            var converter = new GoogleAmpConverter(source);
            return converter.Convert();
        }

        public static void ConvertUrl(string url, PortalSettings ps)
        {
            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.OptionFixNestedTags = true;
            
            //Pretend we are a browser
            var request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:31.0) Gecko/20100101 Firefox/31.0";
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-us,en;q=0.5");

            //Load page
            try
            {
                WebResponse response = request.GetResponse();
                htmlDoc.Load(response.GetResponseStream(), true);
            }
            catch (Exception ex)
            {
                // if something bad happens, skip it.  Maybe log it some day.
                return;
            }
            

            if (htmlDoc.DocumentNode != null)
            {
                //Get content pane
                var content = htmlDoc.DocumentNode
                                    .SelectSingleNode("//div[@id='dnn_ContentPane']");
                content.ParentNode.RemoveChild(content, true); //<--remove content div but keep inner

                var parsedHTML = Convert(content.OuterHtml);

                var sbNewAMPPage = new StringBuilder();
                //grab template for start of document
                sbNewAMPPage.Append(File.ReadAllText(HostingEnvironment.MapPath("~/DesktopModules/DNN_AMP/templates/start.html")));
                //token replace for template
                sbNewAMPPage.Replace("[ItemUrl]", url);
                sbNewAMPPage.Replace("[LOGO]", ps.HomeDirectory + ps.LogoFile);

                sbNewAMPPage.Append(parsedHTML);
                sbNewAMPPage.Append("</body></html>");

                //create page name based on current tab
                var normalizedUrl = url.Replace("//", "");
                var idx = normalizedUrl.IndexOf("/", StringComparison.InvariantCulture);
                var idx2 = normalizedUrl.LastIndexOf("/", StringComparison.InvariantCulture);
                string ampPageName = "home";
                string ampFolder = "";

                // get sub folder (if there is one)
                if (idx > -1 && idx != idx2)
                    ampFolder = normalizedUrl.Substring(idx, idx2-idx);

                // get pagename
                if(idx2 > -1)
                    ampPageName = normalizedUrl.Substring(idx2 + 1);

                //path to amp page
                string folderPath = HostingEnvironment.MapPath(ps.HomeDirectory + "amp" + ampFolder);
                
                // make sure the folder structure exists
                if(!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string strFileName = Path.Combine(folderPath, ampPageName);
                strFileName = strFileName + ".html";
                var fs = new FileStream(strFileName, FileMode.Create);
                var writer = new StreamWriter(fs, Encoding.UTF8);
                writer.Write(sbNewAMPPage.ToString());
                writer.Close();
            }
        }
        #endregion

        #region Conversion Helpers
        public string Convert()
        {
            var result = ReplaceIframeWithLink(source);
            result = UpdateAmpImages(result);
            result = StripStylesAndScripts(result);
            result = ReplaceEmbedWithLink(result);
            result = DivStripper(result);
            return result;
        }

        private string HarshStripper(string markup)
        {
            if (string.IsNullOrEmpty(markup)) return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(markup);

            var acceptableTags = new String[] { "strong", "em", "u", "p", "ul", "ol", "li", "span" };

            var nodes = new Queue<HtmlNode>(doc.DocumentNode.SelectNodes("./*|./text()"));

            while (nodes.Count > 0)
            {
                var node = nodes.Dequeue();
                var parentNode = node.ParentNode;

                if (!acceptableTags.Contains(node.Name) && node.Name != "#text")
                {
                    var childNodes = node.SelectNodes("./*|./text()");

                    if (childNodes != null)
                    {
                        foreach (var child in childNodes)
                        {
                            nodes.Enqueue(child);
                            parentNode.InsertBefore(child, node);
                        }
                    }

                    parentNode.RemoveChild(node);

                }
            }

            return doc.DocumentNode.InnerHtml;
        }

        private string DivStripper(string markup)
        {

            var doc = GetHtmlDocument(markup);
            var elements = doc.DocumentNode.Descendants("//div[contains(@class,'DnnModule')]");
            foreach (var htmlNode in elements)
            {
                htmlNode.ParentNode.RemoveChild(htmlNode, true); //<-- keepGrandChildren
                markup = htmlNode.OuterHtml;
            }


            return markup;
        }

        private string ReplaceIframeWithLink(string markup)
        {

            var doc = GetHtmlDocument(markup);
            var elements = doc.DocumentNode.Descendants("iframe");
            foreach (var htmlNode in elements)
            {
                if (htmlNode.Attributes["src"] == null)
                {
                    continue;
                }
                var link = htmlNode.Attributes["src"].Value;
                var paragraph = doc.CreateElement("p");
                var text = link; // TODO: This might need to be expanded in the future
                var anchor = doc.CreateElement("a");
                anchor.InnerHtml = text;
                anchor.Attributes.Add("href", link);
                anchor.Attributes.Add("title", text);
                paragraph.InnerHtml = anchor.OuterHtml;

                var original = htmlNode.OuterHtml;
                var replacement = paragraph.OuterHtml;

                markup = markup.Replace(original, replacement);
            }

            return markup;
        }

        private string StripStylesAndScripts(string markup)
        {

            var doc = GetHtmlDocument(markup);


            doc.DocumentNode.Descendants()
                           .Where(n => n.Name == "script" || n.Name == "style" || n.Name == "#comment")
                           .ToList()
                           .ForEach(n => n.Remove());
            var elements = doc.DocumentNode.Descendants();
            foreach (var htmlNode in elements)
            {
                if (htmlNode.Attributes["style"] == null)
                {
                    continue;
                }

                htmlNode.Attributes.Remove(htmlNode.Attributes["style"]);
            }

            var builder = new StringBuilder();
            var writer = new StringWriter(builder);
            doc.Save(writer);
            return builder.ToString();
        }

        private string ReplaceEmbedWithLink(string markup)
        {

            var doc = GetHtmlDocument(markup);
            var elements = doc.DocumentNode.Descendants("embed");
            foreach (var htmlNode in elements)
            {
                if (htmlNode.Attributes["src"] == null) continue;

                var link = htmlNode.Attributes["src"].Value;
                var paragraph = doc.CreateElement("p");
                var anchor = doc.CreateElement("a");
                anchor.InnerHtml = link;
                anchor.Attributes.Add("href", link);
                anchor.Attributes.Add("title", link);
                paragraph.InnerHtml = anchor.OuterHtml;
                var original = htmlNode.OuterHtml;
                var replacement = paragraph.OuterHtml;

                markup = markup.Replace(original, replacement);
            }

            return markup;
        }

        private string UpdateAmpImages(string markup)
        {

            var doc = GetHtmlDocument(markup);
            var imageList = doc.DocumentNode.Descendants("img");

            const string ampImage = "amp-img";
            if (!imageList.Any())
            {
                return markup;
            }

            if (!HtmlNode.ElementsFlags.ContainsKey("amp-img"))
            {
                HtmlNode.ElementsFlags.Add("amp-img", HtmlElementFlag.Closed);
            }

            foreach (var imgTag in imageList)
            {
                var original = imgTag.OuterHtml;

                var replacement = imgTag.Clone();
                string width = "";
                if (imgTag.Attributes.Contains("style") && imgTag.Attributes["style"].Value.Contains("width:"))
                {
                    string Pattern = @"(?<=width: )[0-9A-Za-z]+(?=;)";
                    width = Regex.Match(replacement.OuterHtml, Pattern).Value;
                    width = width.Replace("px", "");
                }

                width = string.IsNullOrEmpty(width) ? "400" : width;

                string height = "";

                if (imgTag.Attributes.Contains("style") && imgTag.Attributes["style"].Value.Contains("height:"))
                {
                    string Pattern = @"(?<=height: )[0-9A-Za-z]+(?=;)";
                    height = Regex.Match(replacement.OuterHtml, Pattern).Value;
                    height = width.Replace("px", "");
                }

                height = string.IsNullOrEmpty(height) ? "300" : height;

                replacement.Name = ampImage;
                replacement.Attributes.Add("width", width);
                replacement.Attributes.Add("height", height);
                replacement.Attributes.Add("layout", "responsive");
                replacement.Attributes.Remove("caption");
                replacement.Attributes.Remove("style");
                replacement.Attributes.Remove("title");
                //replacement.Attributes.Add("test", Result);
                markup = markup.Replace(original, replacement.OuterHtml);
            }

            return markup;
        }
        private HtmlDocument GetHtmlDocument(string htmlContent)
        {
            var doc = new HtmlDocument
            {
                OptionOutputAsXml = false, //don't xml because it puts xml declaration in the string, which amp does not like
                OptionDefaultStreamEncoding = Encoding.UTF8
            };
            doc.LoadHtml(htmlContent);

            return doc;
        }
        #endregion

        #region Site Batching
        #region Provider configuration and setup

        private const string SITEMAP_VERSION = "0.9";
        private const string LAST_UPDATED = "AMP-lastupdated";
        private static List<SitemapProvider> _providers;

        private static readonly object _lock = new object();

        private static List<SitemapProvider> Providers
        {
            get
            {
                return _providers;
            }
        }

        private static void LoadProviders()
        {
            // Avoid claiming lock if providers are already loaded
            if (_providers == null)
            {
                lock (_lock)
                {
                    _providers = new List<SitemapProvider>();


                    foreach (KeyValuePair<string, SitemapProvider> comp in ComponentFactory.GetComponents<SitemapProvider>())
                    {
                        comp.Value.Name = comp.Key;
                        comp.Value.Description = comp.Value.Description;
                        _providers.Add(comp.Value);
                    }

                    //'ProvidersHelper.InstantiateProviders(section.Providers, _providers, GetType(SiteMapProvider))
                }
            }
        }

        #endregion

        internal static void BuildAmps()
        {
            // get all url providers
            LoadProviders();

            // get last run date/time
            var dt = HostController.Instance.GetString(LAST_UPDATED);
            DateTime.TryParse(dt, out var dtLastRun);

            // get all portals
            var portals = PortalController.Instance.GetPortals();

            foreach (PortalInfo p in portals)
            {
                // get portal settings
                var ps = new PortalSettings(p.PortalID);
                var urls = GetAllUrls(ps, dtLastRun);

                // convert all urls
                foreach (var u in urls)
                {
                    ConvertUrl(u.Url, ps);
                }
            }

            HostController.Instance.Update(LAST_UPDATED, DateTime.UtcNow.ToString());
        }


        private static List<SitemapUrl> GetAllUrls(PortalSettings ps, DateTime dtLastRun)
        {
            var allUrls = new List<SitemapUrl>();

            // get all urls
            foreach (SitemapProvider _provider in Providers)
            {
                var isProviderEnabled = bool.Parse(PortalController.GetPortalSetting(_provider.Name + "Enabled", ps.PortalId, "True"));

                if (isProviderEnabled)
                {
                    // Get all urls from provider
                    var urls = new List<SitemapUrl>();
                    try
                    {
                        urls = _provider.GetUrls(ps.PortalId, ps, SITEMAP_VERSION);
                    }
                    catch (Exception ex)
                    {
                        Exceptions.LogException(ex);
                    }

                    foreach (SitemapUrl url in urls)
                    {
                        // excluded urls older than the last updated date
                        if (url.LastModified > dtLastRun)
                            allUrls.Add(url);
                    }
                }
            }

            return allUrls;
        }
        #endregion
    }
}