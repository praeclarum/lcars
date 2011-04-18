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
using HtmlAgilityPack;
using System.Collections.Generic;

namespace CommPadd
{

	public class CraigslistForSale : RssSource, IHelpful {
		public string List { get; set; }
		public string Search { get; set; }
		[Optional]
		public int MinPrice { get; set; }
		[Optional]
		public int MaxPrice { get; set; }
		
		public string HelpForProperty(string propName) {
			if (propName == "List") {
				return "is the city to search.\ndenver, newyork, seattle, sfbay, washingtondc, etc.";
			}
			else if (propName == "Search") {
				return "is what to search for";
			}
			else if (propName == "MinPrice") {
			}
			else if (propName == "MaxPrice") {
			}
			return "";
		}

		public override string GetUrl() {
			var url = "";
			
			var basUrl = string.Format("http://{0}.craigslist.org/",
			                        Uri.EscapeDataString(List.TrimWhite().ToLowerInvariant()));
			
			var home = Html.Get(basUrl);
			
			var searchForm = home.SelectSingleNode("//form[@id='search']");
			
			if (searchForm == null) throw new ApplicationException("Craigslist did not return a search form");
			
			var form = Html.InterpretForm(searchForm);
			
			var action = form.Action;
			if (action.StartsWith("/")) {
				action = action.Substring(1);
			}
			
			form.Inputs["query"] = Search;			
			
			var q = Http.MakeQueryString(form.Inputs);
			
			url = string.Format("{0}{1}?{2}&format=rss",
			                        basUrl,
			                        action,
			                        q);
			
			if (MinPrice != 0 || MaxPrice != 0) {
				url += string.Format("&minAsk={0}&maxAsk={1}",
			                        MinPrice,
			                        MaxPrice);
			}
			
			return url;
		}

		public override bool Matches (Source other)
		{
			var o = other as CraigslistForSale;
			return (o != null) && (o.Search == Search) && (o.List == List);
		}
		
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromMinutes(25);
		}
		
		public override string GetDistinguisher ()
		{
			return Search;
		}
		
		public override string GetDistinguisherName()
		{
			return "SEARCH";
		}
		
		protected override void PostProcess (Message m)
		{
			var root = Html.Get(m.Url);
			
			var userbody = root.SelectSingleNode("//div[@id='userbody']");
			
			if (userbody != null) {

				//
				// Remove the blurbs
				//
				var blurbs = userbody.SelectSingleNode("ul[@class='blurbs']");
				if (blurbs != null) {
					userbody.RemoveChild(blurbs);
				}
				
				//
				// Move the pictures to the top
				//
				var images = userbody.SelectSingleNode("table[@summary='craigslist hosted images']");
				if (images != null) {
					userbody.RemoveChild(images);
					userbody.InsertBefore(images, userbody.FirstChild);
				}
				
				m.BodyHtml = userbody.InnerHtml;
			}
		}
	}
}
