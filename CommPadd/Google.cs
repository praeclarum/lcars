//
// Copyright (c) 2009-2011 Krueger Systems, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Xml.Linq;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace CommPadd
{
	
	
	public class PicasaUser : RssSource, IHelpful {
		
		public string User { get; set; }
		
		public override string GetUrl ()
		{
			return string.Format(@"feed://picasaweb.google.com/data/feed/base/user/{0}?alt=rss&kind=album&hl=en_US&access=public",
			                     Uri.EscapeDataString(User));
		}
		
		public string HelpForProperty(string propName) {
			if (propName == "User") {
				return "is the user name of the person to subscribe to";
			}
			return "";
		}
		
		public override string GetDistinguisher ()
		{
			return User;
		}
		
		public override string GetDistinguisherName ()
		{
			return "USER";
		}
		
		public override TimeSpan GetExpirationDuration ()
		{
			return TimeSpan.FromDays(1.11);
		}

		public override bool Matches (Source other)
		{
			var o = other as PicasaUser;
			return (o != null) && (o.User == User);
		}
		
		static Regex PhotosRssRe = new Regex(@".photosRss = ""([^""]+)""");
		
		protected override void PostProcess (Message m)
		{
			var html = Html.Parse(m.BodyHtml);
			
			var imgs = html.SelectNodes("//img");
			if (imgs == null) return;
			
			var url = "";
			
			foreach (HtmlNode img in imgs) {
				var p = img.ParentNode;
				if (p == null) continue;
				if (p.Name != "a") continue;
				
				var href = p.Attributes["href"];
				if (href == null) continue;
				
				url = href.Value;
				if (url.StartsWith("http://picasaweb") && url.ToLowerInvariant().IndexOf(User.ToLowerInvariant()) > 0) {
					break;
				}
			}
			if (url == "") return;
			
			var albumHtml = Http.Get(url);
			
			var ma = PhotosRssRe.Match(albumHtml);
			
			if (ma.Success) {
				
				var jsUrl = ma.Groups[1].Value;
				url = jsUrl.Replace("\\x2F", "/").Replace("\\x26", "&");
				                        
				var xml = XDocument.Parse(Http.Get(url));
				
				foreach (var item in xml.Descendants("item")) {
					foreach (var c in item.Elements()) {
						if (c.Name.LocalName == "group") {
							foreach (var med in c.Elements()) {
								
								if (med.Name.LocalName == "content") {
								
									var img = med.Attribute("url").Value;
									m.BodyHtml += "<br/><img src='" + img + "' />";
								}
								
							}
						}
					}
				}
				
			}
			
		}
	}

	public abstract class ImageSearch : Source, IHelpful {
		
		public string Search { get; set; }
		
		public override bool Matches(Source other) {
			var o = other as ImageSearch;
			return (o != null) && (o.Search == Search);
		}
		
		public string HelpForProperty(string propName) {
			if (propName == "Search") {
				return "is what to search for";
			}
			return "";
		}
		
		public ImageSearch() {
			Search = "";
		}
		
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromDays(0.9);
		}

		public override string GetDistinguisher()
		{
			return Search;
		}
		
		public override string GetDistinguisherName()
		{
			return "SEARCH";
		}
		
		protected override void DoUpdate ()
		{
			int i = 1;
			bool gotNew = true;
			while (gotNew && i < 10) {
				gotNew = SearchPage(i);
				i++;
			}
		}
				
		bool SearchPage (int pageNumber)
		{
			//var now = DateTime.UtcNow;
			var url = string.Format("http://images.google.com/images?q={0}&hl=en&safe=off&tbo=1&gbv=2&tbs=isch:1&sa=N&start={1}&ndsp={2}",
			                        Uri.EscapeDataString(Search),
			                        (pageNumber-1)*20, 20);
			var html = Http.Get(url);
			
			Console.WriteLine (html);
			
			var gotNew = false;
			
			//var divs = doc.DocumentNode.SelectNodes("//div");
			
			//foreach (var div in divs) {
//				if (Class(div) == "detail") {
//					
//					Message msg = null;
//					var save = false;
//
//					foreach (var s in div.SelectNodes("div")) {
//						var c = Class(s);
//						if (c == "title") {
//							var rawid = s.SelectSingleNode("a").Attributes["href"].Value;
//							if (rawid.StartsWith("/")) {
//								rawid = rawid.Substring(1);
//							}
//							
//							msg = GetMessageByRawId(rawid);
//							save = msg.Subject.Length == 0;
//							
//							if (save) {
//								gotNew = true;
//							}
//							else {
//								break;
//							}
//							
//							msg.Url = "http://vimeo.com/" + msg.RawId;							
//							msg.MediaUrl = "http://vimeo.com/play_redirect?clip_id=" +
//								msg.RawId + "&quality=sd";
//							
//							msg.Subject = s.InnerText.Trim();
//						}
//						else if (c == "description" && msg != null) {
//							
//							var sib = div.PreviousSibling;
//							while (sib.NodeType != HtmlNodeType.Element && sib != div) {
//								sib = sib.PreviousSibling;
//							}
//							var img = sib.SelectSingleNode("a/img");
//							var imgSrc = img.Attributes["src"].Value;
//							
//							var h = "<img src='" + imgSrc + "'>";
//							h += s.InnerHtml.Trim();
//							
//							msg.BodyHtml = h;
//						}
//						else if (c == "credits" && msg != null) {
//							msg.From = s.InnerText.TruncateWords(30);
//						}
//						else if (c == "date" && msg != null) {
//							var ago = s.FirstChild.InnerText.Trim();
//							var ps = ago.Split(' ');
//							var f = double.Parse(ps[0]);
//							if (ps[1] == "years" || ps[1] == "year") {
//								msg.PublishTime = now - TimeSpan.FromDays(365*f);
//							}
//							else if (ps[1] == "months" || ps[1] == "month") {
//								msg.PublishTime = now - TimeSpan.FromDays(30*f);
//							}
//							else if (ps[1] == "days" || ps[1] == "day") {
//								msg.PublishTime = now - TimeSpan.FromDays(f);
//							}
//							else if (ps[1] == "hours" || ps[1] == "hour") {
//								msg.PublishTime = now - TimeSpan.FromHours(f);
//							}
//							else if (ps[1] == "minutes" || ps[1] == "minute") {
//								msg.PublishTime = now - TimeSpan.FromMinutes(f);
//							}
//							else {
//								Console.WriteLine (ago);
//							}
//						}
//					}
//					if (save) {
//						Save(msg);
//					}
//				}
			//}
			Console.WriteLine ("U: Parsed Image Search");
			return gotNew;
		}
	}

	public static class HtmlNodeEx {
		
		public static string GetClass(this HtmlNode div) {
			var a = div.Attributes["class"];
			if (a != null) {
				return a.Value;
			}
			else {
				return "";
			}
		}
	}

	public class News : RssSource, IHelpful {
		public string Search { get; set; }
		
		public override bool Matches (Source other)
		{
			var o = other as News;
			return (o != null) && (o.Search == Search);
		}
		
		public string HelpForProperty(string propName) {
			if (propName == "Search") {
				return "are the news terms to subscribe to";
			}
			return "";
		}

		
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromMinutes(15);
		}
		
		public override string GetDistinguisher ()
		{
			return Search;
		}
		public override string GetDistinguisherName()
		{
			return "SEARCH";
		}
		
		public override string GetUrl() {
			var url = string.Format("http://news.google.com/news?pz=1&cf=all&ned=us&hl=en&q={0}&cf=all&output=rss",
			                        Uri.EscapeDataString(Search));
			return url;             
		}
		
		protected override void PostProcess (Message m)
		{
			//m.BodyHtml = Html.GetCleanArticle(m.Url);
		}
	}

}
