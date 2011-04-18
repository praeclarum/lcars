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
using Data;
using System.Linq;
using System.Xml.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using HtmlAgilityPack;

namespace CommPadd
{
	public abstract class RssSource : Source
	{
		public RssSource ()
		{
		}
		
		protected virtual string FilterBodyHtml(string bodyHtml) {
			return bodyHtml;
		}
		
		protected virtual string FilterFrom(string fr) {
			return fr;
		}
					
		protected override void DoUpdate() {
			var xml = Http.Get(GetUrl());
			
			var r = new System.Xml.XmlTextReader(new StringReader(xml));
			r.XmlResolver = new Resolver();
			
			var doc = XDocument.Load(r).Root;
			
			if (doc.Name.LocalName.ToLowerInvariant() == "rss") {
				ParseRss(doc);
			}
			else if (doc.Name.LocalName.ToLowerInvariant() == "rdf") {
				ParseRss(doc);
			}
			else if (doc.Name.LocalName.ToLowerInvariant() == "feed") {
				ParseAtom(doc);
			}
			else {
				Console.WriteLine (doc);
			}			
		}
		
		class Resolver : System.Xml.XmlResolver {
			public override Uri ResolveUri (Uri baseUri, string relativeUri)
			{
				return baseUri;
			}
			public override object GetEntity (Uri absoluteUri, string role, Type type)
			{
				return null;
			}		
			public override ICredentials Credentials {
				set {
				}
			}
		}
		
		public static bool IsParsable(string content) {
			try {
				var doc = XDocument.Parse(content);
				var root = doc.Root.Name.LocalName;
				return (root == "rss") || (root == "rdf") || (root == "feed");
			}
			catch (Exception) {
				return false;
			}
		}
		
		void ParseAtom(XElement atom) {
			var n = atom.Name.Namespace;
			foreach (var entry in atom.Descendants(n+"entry")) {
				Func<XName,string> ev = nn => {
					var e = entry.Element(nn);
					if (e != null) return e.Value.Trim();
					else return "";
				};
				
				var rawId = ev(n+"id");
				
				var msg = GetMessageByRawId(rawId);
				
				//Console.WriteLine (entry);
				
				if (msg.BodyHtml.Length == 0) {
					
					msg.Subject = ParseSubject(ev(n+"title"));
					msg.Url = entry.Element(n+"link").Attribute("href").Value;
					msg.PublishTime = InternetTime.Parse(ev(n+"published"));
					
					var contentE = entry.Element(n + "content");
					
					var body = "";
					if (contentE != null) {
						body = contentE.Value.Trim();
					}
					if (string.IsNullOrEmpty(body) && contentE != null && contentE.Attribute("src") != null) {
						body = contentE.Attribute("src").Value;
					}
					msg.BodyHtml = FilterBodyHtml(body);
				
					var a = entry.Element(n+"author");
					if (a != null) {
						var nameE = a.Element(n+"name");
						if (nameE != null) {
							var name = ParseName(nameE.Value.Trim());
							msg.From = FilterFrom(name);
						}
					}
					
					Save(msg);
				}
				//Console.WriteLine ("AM: " + msg.Subject);
			}
			Console.WriteLine ("U: Parsed ATOM");
		}
		
		Regex ParensRe = new Regex(@"\([^\)]*\)");
		
		string ParseName(string raw) {
			var name = GetHtmlText(raw);
			name = ParensRe.Replace(name, " ");
			name = name.Trim();
			return name;
		}
		
		string ParseSubject(string subj) {
			return GetHtmlText(subj);
		}
		
		Regex ContainsHtmlRe = new Regex(@"(\<[a-zA-Z]|\&\#?[a-z0-9]+\;)");
		string GetHtmlText(string possibleHtml) {
			var cm = ContainsHtmlRe.Match(possibleHtml);
			if (cm.Success) {
				return Html.GetText(possibleHtml);
			}
			else {
				return possibleHtml;
			}
		}
		
		void ParseRss(XElement rss) {
			var ns = rss.Elements().First().Name.Namespace;
			foreach (var item in rss.Descendants(ns+"item")) {
				Func<string,string> ev = n => {
					foreach (var ch in item.Elements()) {
						if (ch.Name.LocalName.ToLowerInvariant() == n) return ch.Value.Trim();
					}
					return "";
				};
				Func<XElement,string,XElement> c = (p,n) => {
					foreach (var ch in p.Elements()) {
						if (ch.Name.LocalName == n) return ch;
					}
					return null;
				};
				
				var rawId = ev("guid");
				if (rawId.Length == 0) {
					rawId = ev("link");
					if (rawId.Length == 0) {
						rawId = ev("pubDate");
					}
				}
				var msg = GetMessageByRawId(rawId);
				
				//Console.WriteLine (item);

				if (msg.BodyHtml.Length == 0) {
					
					msg.Subject = ParseSubject(ev("title"));
					msg.Url = ev("link");
					
					msg.BodyHtml = FilterBodyHtml(ev("description"));
					
					var content = ev("encoded");
					
					if (content != "") {
						msg.BodyHtml += "<div>" + content + "</div>";
					}
					
					var d = ev("pubdate");
					if (d.Length == 0) {
						d = ev("date");
					}
					msg.PublishTime = InternetTime.Parse(d);
				
					var a = c(item, "author");
					if (a != null) {
						msg.From = FilterFrom(ParseName(a.Value));
					}
					
					var enc = item.Element("enclosure");
					if (enc != null) {
						var url = enc.Attribute("url");
						if (url != null) {
							msg.MediaUrl = url.Value;
						}
					}
					
					Save(msg);
				}
				//Console.WriteLine ("RM: " + msg.Subject);
			}
			Console.WriteLine ("U: Parsed RSS");
		}
		
		public static string FindFeedUrl(string feedurl) {
			
			if (feedurl.IndexOf("://") < 0) {
				feedurl = "http://" + feedurl;
			}
			if (feedurl.StartsWith("feed://")) {
				feedurl = feedurl.Replace("feed://", "http://");
			}
			
			try {
				var html = Http.Get(feedurl);
				
				if (IsParsable(html)) {
					return feedurl;
				}
				
				var re = new Regex(@"<link ([^\>]*(rss|atom)[^\>]*)>", RegexOptions.IgnoreCase);
				var ms = re.Matches(html);
				if (ms.Count > 0) {
					var m = ms.Cast<Match>().Where(mm => mm.Groups[0].Value.IndexOf("rss") >= 0).FirstOrDefault();
					if (m == null) {
						m = ms.Cast<Match>().First();
					}
					var data = m.Groups[1].Value;
					var h = new Regex(@"href=[""'](.*?)[""']", RegexOptions.IgnoreCase);
					var hm = h.Match(data);
					if (hm.Success) {
						var u = hm.Groups[1].Value;
					
						if (u.StartsWith("http")) {
							feedurl = u;
						}
						else {
							feedurl = new Uri(new Uri(feedurl), u).ToString();
						}
					}
				}
				else {
					re = new Regex(@"<a.*?href=[""'](http://[^""']+(rss|feed|atom)[^""']*)[""']", RegexOptions.IgnoreCase);
					var m = re.Match(html);
					if (m.Success) {
						feedurl = m.Groups[1].Value;
						Console.WriteLine (feedurl);
					}
					else {
						re = new Regex(@"[""'](http://[^""']+(rss|feed|atom)[^""']*)[""']", RegexOptions.IgnoreCase);
					
						m = re.Match(html);
						if (m.Success) {
							feedurl = m.Groups[1].Value;
							Console.WriteLine (feedurl);
						}
					}
				}				
			}
			catch (Exception) {
			}
			
			return feedurl;
		}
		
		public abstract string GetUrl();
	}
	
	public class Rss : RssSource, IHelpful {
		public string Url { get; set; }		
		public override bool Matches(Source other) {
			var o = other as Rss;
			return (o != null) && (o.Url == Url);
		}				
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromMinutes(45);
		}		
		public override string GetDistinguisher ()
		{
			return Url;
		}
		public override string GetDistinguisherName ()
		{
			return "URL";
		}		
		public override string GetUrl() {
			var url = FindFeedUrl(Url);
			Console.WriteLine ("U:  " + Url + " => " + url);
			return url;
		}
		public string HelpForProperty(string propName) {
			if (propName == "Url") {
				return "of a website or an RSS/ATOM feed";
			}
			return "";
		}
	}
	
	public class Blog : RssSource, IHelpful {
		public string Url { get; set; }		
		public override bool Matches(Source other) {
			var o = other as Rss;
			return (o != null) && (o.Url == Url);
		}				
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromMinutes(65);
		}		
		public override string GetDistinguisher ()
		{
			return Url;
		}
		public override string GetDistinguisherName ()
		{
			return "URL";
		}		
		public override string GetUrl() {
			var url = FindFeedUrl(Url);
			Console.WriteLine ("U:  " + Url + " => " + url);
			return url;
		}
		public string HelpForProperty(string propName) {
			if (propName == "Url") {
				return "of the website or the RSS/ATOM feed of the blog";
			}
			return "";
		}
	}
	
	public class Podcast : RssSource, IHelpful {
		public string Url { get; set; }		
		public override bool Matches(Source other) {
			var o = other as Rss;
			return (o != null) && (o.Url == Url);
		}				
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromMinutes(75);
		}		
		public override string GetDistinguisher ()
		{
			return Url;
		}
		public override string GetDistinguisherName ()
		{
			return "URL";
		}		
		public override string GetUrl() {
			var url = FindFeedUrl(Url);
			Console.WriteLine ("U:  " + Url + " => " + url);
			return url;
		}
		public string HelpForProperty(string propName) {
			if (propName == "Url") {
				return "of the website or the RSS/ATOM feed of the podcast";
			}
			return "";
		}
	}
		
}
