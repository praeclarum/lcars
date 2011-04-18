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
using System.Linq;
using System.Text.RegularExpressions;

namespace CommPadd
{

	public class Ebay : Source, IHelpful {
		public string Search { get; set; }
		[Optional]
		public int MinPrice { get; set; }
		[Optional]
		public int MaxPrice { get; set; }

		public override bool Matches (Source other)
		{
			var o = other as Ebay;
			return (o != null) && (o.Search == Search) && (o.MinPrice == MinPrice) && (o.MaxPrice == MaxPrice);
		}
		
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromMinutes(55);
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
		}
		
		public string HelpForProperty(string propName) {
			if (propName == "Search") {
				return "is what to search for";
			}
			else if (propName == "MinPrice") {
			}
			else if (propName == "MaxPrice") {
			}
			return "";
		}

		
		Regex ItemIdRe = new Regex(@"/(\d+)\?");
		
		protected override void DoUpdate ()
		{
			var form = new HtmlForm("/i.html");
			
			form.Inputs["_ipg"] = "50";
			form.Inputs["_in_kw"] = "1";
			form.Inputs["_ex_kw"] = "";
			form.Inputs["_sacat"] = "See-All-Categories";
			form.Inputs["_nkw"] = Search;
			form.Inputs["_okw"] = Search;
			form.Inputs["_oexkw"] = "";
			form.Inputs["_udlo"] = "";
			form.Inputs["_udhi"] = "";
			form.Inputs["_sabdlo"] = "";
			form.Inputs["_sabdhi"] = "";
			form.Inputs["_samilow"] = "";
			form.Inputs["_samihi"] = "";
			form.Inputs["_sabdlo"] = "";
			form.Inputs["LH_SALE_CURRENCY"] = "0";
			form.Inputs["_dmd"] = "1";
			form.Inputs["_fpos"] = "Zip code";
			
			if (MinPrice != 0 || MaxPrice != 0) {
				form.Inputs["_udlo"] = MinPrice.ToString();
				form.Inputs["_udhi"] = MaxPrice.ToString();
				form.Inputs["_mPrRngCbx"] = "1";
			}
			
			var now = DateTime.UtcNow;
			
			var url = "http://shop.ebay.com" + form.Action + "?" + Http.MakeQueryString(form.Inputs);
			var results = Html.Get(url);
			var lviews = results.SelectNodes("//table[@class='lview nol']");
			
			if (lviews == null || lviews.Count == 0) {
				Console.WriteLine ("No items?");
				return;
			}
			
			foreach (var lview in lviews) {
				
				var ttl = lview.SelectSingleNode(".//div[@class='ttl']/a");
				if (ttl == null) continue;
				
				var itemUrl = Html.ReplaceHtmlEntities(ttl.Attributes["href"].Value.Trim());
				var title = ttl.InnerText.Trim();
				if (title.Length == 0) continue;
				
				var msg = GetMessageByRawId(itemUrl);				
				msg.Url = itemUrl;
				
				//
				// Parse the price
				//
				var buyNowPrice = "";
				var curPrice = "";
				var bprice = lview.SelectSingleNode(".//td[@class='prices']/div[position()=2]");
				if (bprice != null) {
					buyNowPrice = bprice.InnerText;
				}
				var price = lview.SelectSingleNode(".//td[@class='prices g-b']");
				if (price == null) {
					price = lview.SelectSingleNode(".//div[@class='g-b']");
				}
				if (price != null) {
					curPrice = price.InnerText;
				}
				
				msg.Subject = title;
				if (curPrice != "") {
					msg.Subject += " - " + curPrice;
				}
				
				if (buyNowPrice != "") {
					msg.Subject += " (" + buyNowPrice + ")";
				}
				
				//
				// Parse the close date
				//
				var tds = lview.SelectNodes(".//tr/td");
				foreach (var td in tds) {
					var c = td.Attributes["class"];
					if (c == null) continue;
					if (c.Value.StartsWith("time")) {
						msg.PublishTime = now + ParseTime(Html.ReplaceHtmlEntities(td.InnerText));
					}
				}				
				
				//
				// Get the body
				//
				if (msg.BodyHtml.Length == 0) {
					
					try {
						var m = ItemIdRe.Match(msg.Url);
						var itemId = m.Groups[1].Value;
						var descUrl = string.Format("http://vi.ebaydesc.com/ws/eBayISAPI.dll?ViewItemDescV4&item={0}&bv=safari&t=0&js=-1",
						                            itemId);
						msg.BodyHtml = Http.Get(descUrl);					
					}
					catch (Exception) {
					}
				}
				
				Save(msg);
			}
		}
		
		TimeSpan ParseTime(string t) {
			try {
				var parts = t.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
				
				var r = TimeSpan.Zero;
				
				foreach (var p in parts) {
					if (p.Length <= 1) continue;
					var typ = p[p.Length - 1];
					var val = int.Parse(p.Substring(0, p.Length - 1));
					if (typ == 'm') {
						r += TimeSpan.FromMinutes(val);
					}
					else if (typ == 'h') {
						r += TimeSpan.FromHours(val);
					}
					else if (typ == 'd') {
						r += TimeSpan.FromDays(val);
					}
					else if (typ == 'w') {
						r += TimeSpan.FromDays(7*val);
					}
					else if (typ == 's') {
						r += TimeSpan.FromSeconds(val);
					}
					else if (typ == 'y') {
						r += TimeSpan.FromDays(365*val);
					}
				}
				
				return r;
			}
			catch (Exception) {
			}
			return TimeSpan.Zero;
						                                                
		}
	}
}
