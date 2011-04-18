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
using System.Threading;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace CommPadd
{
	public class Html {
		public static string GetText(string html) {
			var doc = Html.Parse(html);
			var txt = doc.InnerText;
			return ReplaceHtmlEntities(txt);
		}
		
		static Regex EntityNumRe = new Regex(@"\&\#(\d+)\;");
		
		public static string ReplaceHtmlEntities(string txt) {
			txt = txt.Replace("&amp;","&");
			txt = txt.Replace("&gt;",">");
			txt = txt.Replace("&lt;","<");
			txt = txt.Replace("&nbsp;"," ");
			txt = txt.Replace("&euro;",((char)8364).ToString());
			txt = txt.Replace("&acute;",((char)180).ToString());
			txt = txt.Replace("&hearts;",((char)9829).ToString());
			txt = txt.Replace("&quot;","\"");
			
			txt = EntityNumRe.Replace(txt, m => {
				try {
					return ((char)int.Parse(m.Groups[1].Value)).ToString();
				}
				catch (Exception) {
					return " ";
				}
			});
			
			return txt;
		}
		
		class MaxNode {
			public HtmlNode Node;
			public int Value;			
		}
		
		public static HtmlNode Parse(string html) {
			var d = new HtmlDocument();
			d.LoadHtml(html);
			return d.DocumentNode;
		}
		
		static string ContentHtml(HtmlNode doc) {
		    var names = new string[] {"h1", "h2", "h3", "h4", "h5"};
		    HtmlNode h = null;
		    foreach (var n in names) {
		        h = NodeWithBiggestParent(doc.SelectNodes("//" + n));
		        if (h != null) break;
			}
		    if (h != null) {
		        var p = GetSiblingsHtml(h);
		        return p;
			} else {
		        return "";
			}
		}
		
		public static HtmlNode Get(string url, System.Net.CookieContainer cookies) {
			var html = Http.Get(url, cookies);
			var doc = Html.Parse(html);
			return doc;
		}
		
		public static HtmlNode Get(string url) {
			return Get(url, new System.Net.CookieContainer());
		}
		
		static string GetSiblingsHtml(HtmlNode n) {			
			var p = FindTextParent(n);			
			return p.InnerHtml;			
		}
		
		static HtmlNode NodeWithBiggestParent(HtmlNodeCollection nodes) {
			if (nodes == null) return null;
			
			var max = new MaxNode();
			foreach (var n in nodes) {
				var s = n.ParentNode.InnerHtml.Length;
				if (s > max.Value) {
					max.Node = n;
					max.Value = s;
				}
			}
			return max.Node;			
		}
		
		static HtmlNode FindTextParent(HtmlNode h) {
			var nStart = CountWords(h);
			var nEnd = nStart + 10;
			var p = h.ParentNode;
			while (CountWords(p) < nEnd) {
				p = p.ParentNode;
			}
			return p;
		}
		
		static int CountWords(HtmlNode n) {
			var text = n.InnerText;
			var words = text.Split(' ');
			return words.Length;
		}
		
		public static string GetCleanArticle(string url) {
			var html = Get(url);
			var cn = ContentHtml(html);
			return cn;
		}
		
		public static Regex LinkRe = new Regex(@"\b(?:(?:https?|ftp|file)://|www\.|ftp\.)
  (?:\([-A-Z0-9+&@#/%=~_|$?!:,.]*\)|[-A-Z0-9+&@#/%=~_|$?!:,.])*
  (?:\([-A-Z0-9+&@#/%=~_|$?!:,.]*\)|[A-Z0-9+&@#/%=~_|$])",
		                                RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
		
		public static string[] GetAllLinks(string rawBody) {
			var urls = new Dictionary<string, bool>();
			var hrefs = LinkRe.Matches(rawBody);
			foreach (Match m in hrefs) {				
				var href = m.Groups[0].Value.Trim();
				urls[href] = true;
			}			
			return urls.Keys.ToArray();
		}
		
		public static string Encode(string text) {
			return HtmlAgilityPack.HtmlDocument.HtmlEncode(text);
		}
		
		public static HtmlForm InterpretForm(HtmlNode searchForm) {
			var inputs = new Dictionary<string,string>();
			
			
			var aa = searchForm.Attributes["action"];
			var action = "";
			if (aa != null) {
				action = aa.Value;
			}
			
			foreach (var n in searchForm.SelectNodes("//input")) {
				var na = n.Attributes["name"];
				var va = n.Attributes["value"];
				if (na != null) {
					inputs[na.Value] = va == null ? "" : ReplaceHtmlEntities(va.Value);
				}
			}

			foreach (var n in searchForm.SelectNodes("//select")) {
				var na = n.Attributes["name"];
				
				if (na != null) {
					foreach (var s in n.SelectNodes(".//option")) {
						var sa = s.Attributes["selected"];
						var ova = s.Attributes["value"];
						if (sa != null && ova != null) {
							inputs[na.Value] = ReplaceHtmlEntities(ova.Value);
							break;
						}
					}
					if (!inputs.ContainsKey(na.Value)) {
						inputs[na.Value] = "";
					}
				}				
			}

			return new HtmlForm(action, inputs);
		}		
		
		public static IEnumerable<HtmlNode> Descs(HtmlNode p, string elementName) {
			var es = new List<HtmlNode>();
			Descs(p, elementName, es);
			return es;
		}
		
		static void Descs(HtmlNode p, string elementName, List<HtmlNode> nodes) {
			//Console.WriteLine (" >" + p.Name);
			if (p.Name == elementName) {
				nodes.Add(p);
			}
			foreach (var c in p.ChildNodes) {
				Descs(c, elementName, nodes);
			}			
		}
		
		public static string MakeLinks(string rawBody) {
			return LinkRe.Replace(rawBody, @"<a href=""$0"">$0</a>");
		}
	}
	
	public class HtmlForm {
		
		public string Action { get; private set; }
		public Dictionary<string,string> Inputs { get; private set; }
		
		public HtmlForm(string action) {
			Action = action;
			Inputs = new Dictionary<string, string>();
		}
		
		public HtmlForm(string action, Dictionary<string,string> inputs) {
			Action = action;
			Inputs = inputs;
		}
	}

}
