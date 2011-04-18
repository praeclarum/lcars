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

namespace CommPadd
{
	public class VimeoSearch : Source {
		
		public string Search { get; set; }
		
		public override bool Matches(Source other) {
			var o = other as VimeoSearch;
			return (o != null) && (o.Search == Search);
		}
		
		public VimeoSearch() {
			Search = "";
		}
		
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromMinutes(115);
		}

		public override string GetDistinguisher()
		{
			return Search;
		}
		
		public string HelpForProperty(string propName) {
			if (propName == "Search") {
				return "is what to search for on Vimeo";
			}
			return "";
		}
		
		public override string GetDistinguisherName()
		{
			return "SEARCH";
		}
		
		protected override void DoUpdate ()
		{
			int i = 1;
			bool gotNew = true;
			while (gotNew && i < 5) {
				gotNew = SearchPage(i);
				i++;
			}
		}
		
		public static string GetVideoUrl(string id) {
			return "http://vimeo.com/play_redirect?clip_id=" +
								id + "&quality=sd";
		}
		
		bool SearchPage (int pageNumber)
		{
			var now = DateTime.UtcNow;
			var url = string.Format("http://vimeo.com/videos/search:{0}/page:{1}/sort:newest/format:detail",
			                        Uri.EscapeDataString(Search),
			                        pageNumber);
			var html = Http.Get(url);
			var doc = Html.Parse(html);
			
			var gotNew = false;
			
			var divs = doc.SelectNodes("//div");
			
			foreach (var div in divs) {
				if (div.GetClass() == "detail") {
					
					Message msg = null;
					var save = false;

					foreach (var s in div.SelectNodes("div")) {
						var c = s.GetClass();
						if (c == "title") {
							var rawid = s.SelectSingleNode("a").Attributes["href"].Value;
							if (rawid.StartsWith("/")) {
								rawid = rawid.Substring(1);
							}
							
							msg = GetMessageByRawId(rawid);
							save = msg.Subject.Length == 0;
							
							if (save) {
								gotNew = true;
							}
							else {
								break;
							}
							
							msg.Url = "http://vimeo.com/" + msg.RawId;							
							msg.MediaUrl = GetVideoUrl(msg.RawId);
							
							msg.Subject = s.InnerText.Trim();
						}
						else if (c == "description" && msg != null) {
							
							var sib = div.PreviousSibling;
							while (sib.NodeType != HtmlNodeType.Element && sib != div) {
								sib = sib.PreviousSibling;
							}
							var img = sib.SelectSingleNode("a/img");
							var imgSrc = img.Attributes["src"].Value;
							
							var h = "<img src='" + imgSrc + "'>";
							h += s.InnerHtml.Trim();
							
							msg.BodyHtml = h;
						}
						else if (c == "credits" && msg != null) {
							msg.From = s.InnerText.TruncateWords(30);
						}
						else if (c == "date" && msg != null) {
							var ago = s.FirstChild.InnerText.Trim();
							var ps = ago.Split(' ');
							var f = double.Parse(ps[0]);
							if (ps[1] == "years" || ps[1] == "year") {
								msg.PublishTime = now - TimeSpan.FromDays(365*f);
							}
							else if (ps[1] == "months" || ps[1] == "month") {
								msg.PublishTime = now - TimeSpan.FromDays(30*f);
							}
							else if (ps[1] == "days" || ps[1] == "day") {
								msg.PublishTime = now - TimeSpan.FromDays(f);
							}
							else if (ps[1] == "hours" || ps[1] == "hour") {
								msg.PublishTime = now - TimeSpan.FromHours(f);
							}
							else if (ps[1] == "minutes" || ps[1] == "minute") {
								msg.PublishTime = now - TimeSpan.FromMinutes(f);
							}
							else {
								Console.WriteLine (ago);
							}
						}
					}
					if (save) {
						Save(msg);
					}
				}
			}
			Console.WriteLine ("U: Parsed Vimeo Search");
			return gotNew;
		}
	}
}
