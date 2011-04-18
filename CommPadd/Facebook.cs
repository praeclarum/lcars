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
using System.Net;
using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.Foundation;
using System.Json;

namespace CommPadd
{

	[CustomUI(typeof(FacebookAddUI))]
	public class Facebook : Source, IHelpful
	{
		public const string ClientId = "XXX"; // REPLACE

		public string UserId { get; set; }
		public string UserName { get; set; }
		public string AccessToken { get; set; }

		public Facebook ()
		{
			UserId = "";
			UserName = "";
			AccessToken = "";			
		}

		public static string AuthenticationUrl;
		static Facebook ()
		{			
			AuthenticationUrl = string.Format ("https://graph.facebook.com/oauth/authorize?client_id={0}&redirect_uri={1}&type=user_agent&scope={2}", ClientId, Uri.EscapeDataString ("http://www.facebook.com/connect/login_success.html"), "offline_access,read_stream");
		}

		public override bool Matches (Source other)
		{
			var o = other as Facebook;
			return (o != null) && (o.AccessToken == AccessToken);
		}

		public override TimeSpan GetExpirationDuration ()
		{
			return TimeSpan.FromMinutes (45);
		}

		public override string GetDistinguisher ()
		{
			return UserName;
		}

		public override string GetDistinguisherName ()
		{
			return "NAME";
		}

		protected override void PostProcess (Message m)
		{
		}

		public string HelpForProperty (string propName)
		{
			if (propName == "Email") {
				return "is the email address you use to log in to Facebook";
			}
			return "";
		}

		protected override void DoUpdate ()
		{
			var page = 0;
			var nextUrl = ProcessFeed (GetGraph ("me/home"));
			while (page < 5) {
				ProcessFeed (GetUrl (nextUrl));
				page++;
			}
		}

		string ProcessFeed (JsonObject feed)
		{
			foreach (JsonObject datum in (JsonArray)feed["data"]) {
				
				var id = datum.GetString ("id");
				var messageText = datum.GetString ("message");
				var description = datum.GetString ("description");
				var name = datum.GetString ("name");
				var picture = datum.GetString ("picture");
				var link = datum.GetString ("link");
				if (link == "http://www.facebook.com")
					link = "";
				var upTime = InternetTime.Parse (datum.GetString ("created_time"));
				var fr = (string)datum["from"]["name"];
				
				if (link.IndexOf("apps.facebook.com") >= 0) continue;				
				
				var m = GetMessageByRawId (id);
				
				m.From = fr;
				m.PublishTime = upTime;
				
				var subj = name;
				
				if (subj.Length == 0) {
					subj = Html.ReplaceHtmlEntities (messageText.TruncateWords (100));
				}
				if (subj.Length == 0) {
					subj = Html.ReplaceHtmlEntities (description.TruncateWords (100));
				}
				if (subj.Length == 0 && picture.Length != 0) {
					subj = "Picture";
				}
				if (subj.Length == 0) {
					Console.WriteLine (datum.ToString ());
				}
				m.Subject = subj;
				
				var html = "<div>";				
				
				if (name.Length > 0) {
						html += "<h2>" + Html.Encode (name) + "</h2>";
				}
				if (picture.Length > 0 && link.Length > 0) {
					html += "<img src=\"" + Html.Encode(picture) + "\" />";
					m.Url = link;
				}
				if (description.Length > 0) {
					html += "<blockquote>" + Html.Encode (description) + "</blockquote>";
				}

				html += Html.MakeLinks(messageText);
				
				html += "</div><div>";
				
				
				if (datum.ContainsKey("comments")) {
					var comments = "";
					var cs = (JsonArray)(datum["comments"]["data"]);
					
					foreach (var c in cs) {
						var cfr = c["from"].GetString("name");
						var cm = c.GetString("message");
						
						comments += "<h4>" + Html.Encode(cfr) + "</h4><blockquote>" + Html.Encode(cm) + "</blockquote>";
					}
					html += comments;
				}
				
				html += "</div>";
				
				m.BodyHtml = html;
				
				Save (m);
			}
			
			var nextUrl = (string)((JsonPrimitive)feed["paging"]["next"]);
			return nextUrl;
		}

		JsonObject GetUrl (string url)
		{
			var json = Http.Get (url);
			return (JsonObject)JsonObject.Parse (json);
		}

		public JsonObject GetGraph (string path)
		{
			var url = "https://graph.facebook.com/" + path + "?access_token=" + AccessToken;
			return GetUrl (url);
		}
		
		public override bool ShouldShowSubjectWithBody {
			get {
				return false;
			}
		}		
	}

	public static class JsonEx
	{
		public static string GetString (this JsonValue valObj, string key)			
		{
			var obj = valObj as JsonObject;
			if (obj == null) return "";
			JsonValue v;
			if (!obj.TryGetValue (key, out v)) {
				return "";
			}
			return (string)v;
		}
	}


	public interface ICustomUI
	{
		void SetModel (object s);
		event Action OnOK;
	}

	public class FacebookAddUI : UIView, ICustomUI
	{
		Facebook _s;
		UIWebView _web;
		bool _started;
		ScanningView _scanner;
		
		public FacebookAddUI ()
		{			
			_s = null;
			_web = new UIWebView ();
			
			KillCookies();
			
			Frame = new RectangleF (PointF.Empty, new SizeF (200, 200));
			_web.Frame = new RectangleF (PointF.Empty, Frame.Size);
			_web.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
			//_web.ScalesPageToFit = true;
			_web.Delegate = new Del (this);
			ClipsToBounds = true;
			_web.Hidden = true;
			
			Theme.MakeBlack(_web);
			
			_scanner = ScanningView.Start(this, _web.Frame);
			
			AddSubview (_web);
		}
		public void SetModel (object s)
		{
			_s = s as Facebook;
		}
		
		void KillCookies() {
			var s = NSHttpCookieStorage.SharedStorage;
			var cs = s.CookiesForUrl(new NSUrl("https://graph.facebook.com"));
			foreach (var c in cs) {
				//Console.WriteLine ("{0} = {1}", c.Name, c.Value);
				s.DeleteCookie(c);
			}
			
			
		}
		
		public override void Draw (RectangleF rect)
		{
			try {
				if (!_started) {
					
					_web.LoadRequest (new NSUrlRequest (new NSUrl (Facebook.AuthenticationUrl)));
					_started = true;
				}
			} catch (Exception error) {
				Log.Error (error);
			}
		}
		public event Action OnOK;

		Regex UserIdRe = new Regex(@"-(\d+)\%7C");
		
		void Success (string token)
		{
			_s.AccessToken = token;
			var m = UserIdRe.Match(token);
			if (m.Success) {
				_s.UserId = m.Groups[1].Value;
			}
			else {
				_s.UserId = token.TruncateChars(4);
			}
			
			//
			// Get their username
			//
			var me = _s.GetGraph("me");
			_s.UserName = (string)me["name"];
			
			if (OnOK != null) {
				OnOK ();
			}
		}

		class Del : UIWebViewDelegate
		{
			FacebookAddUI _ui;
			bool _visible = false;
			
			public Del (FacebookAddUI ui)
			{
				_ui = ui;
			}
			public override void LoadingFinished (UIWebView webView)
			{
				try {
					if (!_visible) {
						_ui._scanner.StopScanning();
						_ui._web.Alpha = 0;
						_ui._web.Hidden = false;
						UIView.BeginAnimations("FbIn");
						_ui._web.Alpha = 1.0f;
						UIView.CommitAnimations();
						_visible = true;
					}
					var url = webView.Request.Url.AbsoluteString;
					var ci = url.LastIndexOf ("access_token=");
					if (ci > 0) {
						var code = url.Substring (ci + "access_token=".Length);
						_ui.Success (code);
					}
					else {
						webView.ScalesPageToFit = true;
					}
				} catch (Exception error) {
					Log.Error (error);
				}
			}
			
		}
	}

	public static class StringExx
	{
		public static string TrimWhite (this string str)
		{
			var s = 0;
			while (s < str.Length && str[s] <= 32) {
				s++;
			}
			if (s >= str.Length)
				return "";
			
			var e = str.Length;
			while (e >= 1 && str[e - 1] <= 32) {
				e--;
			}
			
			if (s == 0 && e == str.Length)
				return str;
			if (e <= s)
				return "";
			
			return str.Substring (s, e - s);
		}
	}
}
