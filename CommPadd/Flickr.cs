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
using System.Linq;

namespace CommPadd
{
	public class FlickrUser : RssSource, IHelpful
	{
		const string ApiKey = "XXX"; // REPLACE
		//const string ApiSecret = "XXX"; // REPLACE
		
		
		public string User { get; set; }		
		
		protected override string FilterFrom (string fr)
		{
			return "";
		}
		
		protected override string FilterBodyHtml (string bodyHtml)
		{
			var n = bodyHtml.Replace("_m.jpg", ".jpg");
			return n;
		}
		
		public string HelpForProperty(string propName) {
			if (propName == "User") {
				return "is the screen name of the person to subscribe to";
			}
			return "";
		}
		
		public override string GetUrl ()
		{
			//
			// Get the username
			//
			var userid = User.Trim();
			
			try {
				var uu = string.Format("http://api.flickr.com/services/rest/?method=flickr.people.findByUsername&api_key={0}&username={1}",
				                       ApiKey,
				                       Uri.EscapeDataString(userid));
				var x = System.Xml.Linq.XDocument.Parse(Http.Get(uu));
				
				userid = x.Descendants("user").First().Attribute("id").Value;				
			}
			catch (Exception) {
			}
			
			return FindFeedUrl("http://www.flickr.com/photos/" + userid + "/");
		}
		
		public override bool Matches (Source other)
		{
			var o = other as FlickrUser;
			return (o != null) && (o.User == User);
		}
		
		public override string GetDistinguisher ()
		{
			return User;
		}
		
		public override TimeSpan GetExpirationDuration ()
		{
			return TimeSpan.FromMinutes(95);
		}
		
		public override string GetDistinguisherName ()
		{
			return "USER";
		}
	}

	public class FlickrTags : RssSource, IHelpful
	{
		public string Tags { get; set; }

		public override string GetUrl ()
		{
			return string.Format("http://api.flickr.com/services/feeds/photos_public.gne?tags={0}",
			                     Uri.EscapeDataString(Tags.Trim()));
			                     
		}
		
		public string HelpForProperty(string propName) {
			if (propName == "Tags") {
				return "are the terms to subscribe to";
			}
			return "";
		}
		
		protected override string FilterBodyHtml (string bodyHtml)
		{
			var n = bodyHtml.Replace("_m.jpg", ".jpg");
			return n;
		}
		
		public override bool Matches (Source other)
		{
			var o = other as FlickrTags;
			return (o != null) && (o.Tags == Tags);
		}
		
		public override string GetDistinguisher ()
		{
			return Tags;
		}
		
		public override TimeSpan GetExpirationDuration ()
		{
			return TimeSpan.FromMinutes(105);
		}
		
		public override string GetDistinguisherName ()
		{
			return "TAGS";
		}
	}
}
