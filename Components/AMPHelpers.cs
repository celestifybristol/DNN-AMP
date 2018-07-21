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
		private readonly string httpalias; // example: somesite.com or http://somesite.com

		public GoogleAmpConverter(string source, string httpalias = "")
		{
			this.source = source;
			this.httpalias = !string.IsNullOrEmpty(httpalias) && httpalias.StartsWith("http") 
								? httpalias 
								: $"http://{httpalias}";
		}

		#region Conversions
		public static string Convert(string source, string httpalias = "")
		{
			var converter = new GoogleAmpConverter(source, httpalias);
			return converter.Convert();
		}

		public static void ConvertUrl(string url, PortalSettings ps)
		{
			var htmlDoc = new HtmlAgilityPack.HtmlDocument();
			htmlDoc.OptionFixNestedTags = true;

			// don't worry about bad certs
			ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

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
				// get page title
				var pagetitlenode = htmlDoc.DocumentNode.SelectSingleNode("//title");
				var pagetitle = pagetitlenode?.InnerText ?? url.Substring(url.LastIndexOf("/")).Replace("/", "");

				//Get content pane
				var content = htmlDoc.DocumentNode.SelectSingleNode("//body");
				content.ParentNode.RemoveChild(content, true); //<--remove content div but keep inner

				var parsedHTML = Convert(content.OuterHtml, ps.DefaultPortalAlias);

				var sbNewAMPPage = new StringBuilder();

				//grab template for start of document
				sbNewAMPPage.Append(File.ReadAllText(HostingEnvironment.MapPath("~/DesktopModules/DNN_AMP/templates/start.html")));
				//token replace for template
				sbNewAMPPage.Replace("[CANONICALURL]", url);
				sbNewAMPPage.Replace("[PAGETITLE]", pagetitle);
				sbNewAMPPage.Replace("[DATE]", DateTime.UtcNow.ToString("o"));
				sbNewAMPPage.Replace("[LOGOURL]", ps.HomeDirectory + ps.LogoFile); // this should change to something in the page that is more dynamic
				sbNewAMPPage.Replace("[BODY]", parsedHTML);

				// get google analytics
				var ga = PortalController.GetPortalSetting("googleanalytics", ps.PortalId, "");
				sbNewAMPPage.Replace("[GOOGLEANALYTICS]", ga);

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
			var result = AddDynamicMenu(source);
			result = ReplaceIframeWithLink(result);
			result = UpdateAmpImages(result);
			result = StripStylesAndScripts(result);
			result = ReplaceEmbedWithLink(result);
			result = DivStripper(result);
			result = HarshStripper(result);
			return result;
		}

		private string AddDynamicMenu(string markup)
		{
			/*
			<amp-sidebar id="sidebar" layout="nodisplay" side="left">
				<ul>
					<li><a href="/">Home</a></li>
					<li><a href="/">Nav item 1</a>
						<ul>
							<li><a href="/">Next</a></li>
							<li><a href="/">Level</a></li>
						</ul>    
					</li>
					<li>
						<amp-fit-text width="220" height="20" layout="responsive" max-font-size="24">
							<a href="/">Nav item 3 - &lt;amp-fit-text&gt; longer text</a>
						</amp-fit-text>
					</li>
					<li><a href="/">Nav item 4</a></li>
					<li><a href="/">Nav item 5</a></li>
					<li><a href="/">Nav item 6</a></li>
				</ul>
			</amp-sidebar>
			*/

			if (string.IsNullOrEmpty(markup)) return string.Empty;

			var doc = new HtmlDocument();
			doc.LoadHtml(markup);

			// find the ul with the "amp-menu" class
			var menuNode = doc.DocumentNode.Descendants().FirstOrDefault(d =>
				d.Attributes.Contains("class")
				&& d.Attributes["class"].Value.Contains("amp-menu"));

			if (menuNode == null)
				return markup;

			menuNode.Attributes.RemoveAll();

			// Construct the amp-sidebar from the given nav
			var nav = doc.CreateElement("amp-sidebar");            
			nav.Attributes.Add("id", "sidebar");
			nav.Attributes.Add("layout", "nodisplay");
			nav.Attributes.Add("side", "left"); // should make this a setting

			// need to clean this
			nav.ChildNodes.Add(menuNode);

			doc.DocumentNode.ChildNodes[0].PrependChild(nav);

			return doc.DocumentNode.InnerHtml;
		}

		private HtmlNode StripAttributes(HtmlNode n)
		{
			n.Attributes.RemoveAll();

			return n;
		}

		private string HarshStripper(string markup)
		{
			if (string.IsNullOrEmpty(markup)) return string.Empty;

			var doc = new HtmlDocument();
			doc.LoadHtml(markup);

			var acceptableTags = new String[] { "strong", "i", "em", "u", "p", "ul", "ol", "li", "span", "div", "a", "amp-sidebar" };

			var removeAllFromTheseTags = new[] { "noscript" };
			var removeAllFromTheseClasses = new[] { "no-amp", "aspNetHidden" };

			// find any tags with a bad class and remove those tags completely (with children)
			foreach(var c in removeAllFromTheseClasses)
			{
				var badNodes = doc.DocumentNode.Descendants().Where(d =>
				d.Attributes.Contains("class")
				&& d.Attributes["class"].Value.Contains(c)
			);

				foreach (var n in badNodes.ToList())
				{
					var parentNode = n.ParentNode;
					parentNode.RemoveChild(n);
				}
			}
			

			// now find all top level nodes and loop through them to make sure they are acceptable
			var nodes = new Queue<HtmlNode>(doc.DocumentNode.SelectNodes("./*|./text()"));

			while (nodes.Count > 0)
			{
				var node = nodes.Dequeue();

				if (node == null)
					continue;

				var parentNode = node.ParentNode;

				// remove these tags and their children completely
				if (removeAllFromTheseTags.Contains(node.Name))
				{
					parentNode.RemoveChild(node);
					continue;
				}

				// if not matching, then remove just the tag
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
				if (htmlNode.Attributes["style"] == null || htmlNode.Name == "amp-img")
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
				string height = "";

				if (imgTag.Attributes.Contains("width"))
				{
					width = imgTag.Attributes["width"].Value;
				}

				if (imgTag.Attributes.Contains("height"))
				{
					height = imgTag.Attributes["height"].Value;
				}

				if (width == "" && imgTag.Attributes.Contains("style") && imgTag.Attributes["style"].Value.Contains("width:"))
				{
					string Pattern = @"(?<=width: )[0-9A-Za-z]+(?=;)";
					width = Regex.Match(replacement.OuterHtml, Pattern).Value;
					width = width.Replace("px", "");
				}                

				if (height == "" && imgTag.Attributes.Contains("style") && imgTag.Attributes["style"].Value.Contains("height:"))
				{
					string Pattern = @"(?<=height: )[0-9A-Za-z]+(?=;)";
					height = Regex.Match(replacement.OuterHtml, Pattern).Value;
					height = width.Replace("px", "");
				}

				// if we don't have the width/height, then download the image and get it
				if((string.IsNullOrEmpty(width) || string.IsNullOrEmpty(height)) && imgTag.Attributes.Contains("src"))
				{
					var imgurl = imgTag.Attributes["src"].Value.StartsWith("http")
						? new Uri(imgTag.Attributes["src"].Value) // full url
						: new Uri($"{httpalias}{imgTag.Attributes["src"].Value}"); // relative url
					var dimensions = ImageUtilities.GetWebDimensions(imgurl);
					width = dimensions.Width > 0 ? dimensions.Width.ToString() : width;
					height = dimensions.Width > 0 ? dimensions.Height.ToString() : height;
				}

				width = string.IsNullOrEmpty(width) ? "200" : width;
				height = string.IsNullOrEmpty(height) ? "150" : height;

				replacement.Name = ampImage;
				replacement.Attributes.Add("width", width);
				replacement.Attributes.Add("height", height);
				replacement.Attributes.Add("layout", "responsive");
				replacement.Attributes.Remove("caption");
				replacement.Attributes.Remove("style");
				replacement.Attributes.Remove("title");
				if(width.ToInt() < 300)
					replacement.Attributes.Add("style", $"max-width:{width}px");
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

		private const string SITEMAP_VERSION = "0.1";
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
                ps.PortalAlias = PortalAliasController.Instance.GetPortalAliasesByPortalId(ps.PortalId).FirstOrDefault(o => o.IsPrimary);
				var urls = GetAllUrls(ps, dtLastRun);

				// convert all urls
				foreach (var u in urls)
				{
					ConvertUrl(u.Url, ps);
				}
			}

			// HostController.Instance.Update(LAST_UPDATED, DateTime.UtcNow.ToString());
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