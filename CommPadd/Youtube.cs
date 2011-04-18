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
using System.Text.RegularExpressions;

namespace CommPadd
{
	public abstract class Youtube : RssSource
	{
		Regex IDRE = new Regex(@"/v/([^\?]+)\?");
		
		protected override string FilterBodyHtml (string bodyHtml)
		{
			var m = IDRE.Match(bodyHtml);
			if (m.Success) {
				var id = m.Groups[1].Value;
				return GetVideoHtml(id);
			}
			else {
				return bodyHtml;
			}
		}
		
		public static string GetVideoHtml(string id) {
			var htmlT = @"<object width=""560"" height=""340""><param name=""movie"" value=""http://www.youtube.com/v/{0}&hl=en_US&fs=1&rel=0&color1=0x3a3a3a&color2=0x999999""></param><param name=""allowFullScreen"" value=""true""></param><param name=""allowscriptaccess"" value=""always""></param><embed src=""http://www.youtube.com/v/{0}&hl=en_US&fs=1&rel=0&color1=0x3a3a3a&color2=0x999999"" type=""application/x-shockwave-flash"" allowscriptaccess=""always"" allowfullscreen=""true"" width=""560"" height=""340""></embed></object>";
			return string.Format(htmlT, id);
		}
		
		protected override void PostProcess (Message m)
		{
			var c = m.RawId.LastIndexOf(':');
			if (c > 0 && c < m.RawId.Length - 3) {
				//var id = m.RawId.Substring(c + 1);
				m.MediaUrl = "";//"http://www.youtube.com/get_video?fmt=18&video_id=" + id + "&t=";
				//Console.WriteLine (m.MediaUrl);
			}
		}
	}

	public class YoutubeSearch : Youtube, IHelpful
	{
		public string Search { get; set; }

		public YoutubeSearch ()
		{
			Search = "";
		}
		
		public override string GetDistinguisher ()
		{
			return Search;
		}
		
		public override string GetDistinguisherName ()
		{
			return "SEARCH";
		}
		
		public string HelpForProperty(string propName) {
			if (propName == "Search") {
				return "is what to search for on YouTube";
			}
			return "";
		}
		
		public override bool Matches(Source other) {
			var o = other as YoutubeSearch;
			return (o != null) && (o.Search == Search);
		}
		
		public override TimeSpan GetExpirationDuration ()
		{
			return TimeSpan.FromDays(1.5);
		}

		public override string GetUrl ()
		{
			var url = string.Format("http://gdata.youtube.com/feeds/api/videos?v=2&q={0}",
			                        Uri.EscapeDataString(Search.Trim()));
			return url;
		}		
	}

	public abstract class YoutubeTags : Youtube
	{
		public string Tags { get; set; }

		public YoutubeTags ()
		{
			Tags = "";
		}
		
		public override string GetDistinguisher ()
		{
			return Tags;
		}
		
		public override string GetDistinguisherName ()
		{
			return "TAGS";
		}
		
		public override bool Matches(Source other) {
			var o = other as YoutubeTags;
			return (o != null) && (o.Tags.Trim().ToLowerInvariant() == Tags.Trim().ToLowerInvariant());
		}
		
		public override TimeSpan GetExpirationDuration ()
		{
			return TimeSpan.FromDays(1.5);
		}

		public override string GetUrl ()
		{
			var url = string.Format("http://gdata.youtube.com/feeds/api/videos?v=2&category={0}",
			                        Uri.EscapeDataString(Tags.Trim()));
			return url;
		}		
	}

}
