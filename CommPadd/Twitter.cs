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
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.Foundation;
using System.Net;
using Data;

namespace CommPadd
{
	[CustomUI(typeof(TwitterAddUI))]
	public class TwitterAccount : Source, IHelpful
	{

		public string Account { get; set; }
		public string Password { get; set; }

		public override bool Matches (Source other)
		{
			var o = other as TwitterAccount;
			return (o != null) && (o.Account == Account);
		}

		public TwitterAccount ()
		{
			Account = "";
			Password = "";
		}

		public string HelpForProperty (string propName)
		{
			if (propName == "Account") {
				return "is your username on Twitter";
			}
			return "";
		}

		public override TimeSpan GetExpirationDuration ()
		{
			return TimeSpan.FromMinutes (10);
		}

		public override string GetDistinguisher ()
		{
			return Account;
		}
		public override string GetDistinguisherName ()
		{
			return "ACCOUNT";
		}

		public override bool ShouldShowSubjectWithBody {
			get { return false; }
		}
		
		TwitterOAuthTokens _tokens = null;
		
		XDocument Api(string method, Uri uri) {
			
			if (_tokens == null) {				
				using (var repo = new Repo()) {					
					_tokens = repo.Table<TwitterOAuthTokens>().Where(t=>t.Account == Account).FirstOrDefault();					
				}				
			}
			
			if (_tokens == null) {
				throw new InvalidOperationException("Cannot access Twitter APIs without tokens");
			}
			
			var wc = new WebClient();
			
			OAuthAuthorizer.AuthorizeRequest(OAuthConfig, wc, _tokens.Token, _tokens.TokenSecret, method, uri, "");
			
			var res = "";
			
			if (method == "GET") {
				res = wc.DownloadString(uri);
			}
			else {
				res = wc.UploadString(uri, "");
			}
			
			return XDocument.Parse(res);
		}

		protected override void DoUpdate ()
		{
			var url = "http://api.twitter.com/1/statuses/home_timeline.xml";
			
			var doc = Api("GET", new Uri(url));
			
			foreach (var status in doc.Descendants ("status")) {
				Func<XName, string> ev = n =>
				{
					var e = status.Element (n);
					if (e != null)
						return e.Value.Trim ();
					else
						return "";
				};
				var rawId = ev ("id");
				var msg = GetMessageByRawId (rawId);
				
				if (msg.BodyHtml.Length == 0) {
					
					msg.Subject = status.Element ("text").Value;
					msg.From = status.Element ("user").Element ("screen_name").Value;
					msg.Url = "http://twitter.com/" + msg.From + "/status/" + msg.RawId;
					msg.PublishTime = InternetTime.Parse (ev ("created_at"));
					msg.BodyHtml = FilterBody (status.Element ("text").Value);
					
					Save (msg);
				}
			}
			Console.WriteLine ("U: Parsed Twitter Home");
		}

		static Regex TwitPicRe = new Regex (@"twitpic\.com/([A-Za-z0-9]+)");
		static Regex YfrogRe = new Regex (@"yfrog\.com/([A-Za-z0-9]+)");
		static Regex TweetPhotoRe = new Regex (@"tweetphoto.com/([A-Za-z0-9]+)");
		static Regex ShortFlickrRe = new Regex (@"flic\.kr/p/([A-Za-z0-9]+)");
		static Regex FlickrRe = new Regex (@"flickr\.com/photos/");
		static Regex YouTubeRe = new Regex (@"youtube\.com.*?v=([A-Za-z0-9\-]+)");
		static Regex ShortYouTubeRe = new Regex (@"youtu.be/(.*)$");
		static Regex ImgRe = new Regex (@"(\.jpg|\.jpeg|\.png|\.gif|\.tiff?)$");

		string FilterBody (string rawBody)
		{
			return Enrich (Html.MakeLinks (rawBody));
		}

		public static string Enrich (string body)
		{
			var n = ResolveUrlShorteners (body);
			n = InlineImagesAndVideos (n);
			return n;
		}

		static Regex ShortRe = new Regex (@"^http://[a-z]+\.[a-z]+/[0-9A-Za-z]+$");

		static string ResolveUrlShorteners (string rawBody)
		{
			var body = rawBody;
			foreach (var url in Html.GetAllLinks (rawBody)) {
				var u = url;
				var m = ShortRe.Match (url);
				var numSteps = 0;
				var maxSteps = 2;
				while (m != null && m.Success && numSteps < maxSteps) {
					try {
						u = Http.Resolve (url);
						m = ShortRe.Match (u);
						numSteps++;
					} catch (Exception) {
						m = null;
					}
				}
				body = body.Replace (url, u);
			}
			
			return body;
		}

		static string InlineImagesAndVideos (string rawBody)
		{
			var body = rawBody;
			foreach (var url in Html.GetAllLinks (rawBody)) {
				
				var m = TwitPicRe.Match (url);
				if (m.Success) {
					var id = m.Groups[1].Value;
					try {
						var html = Html.Get ("http://twitpic.com/" + id);
						var img = html.SelectSingleNode (@"//img[@id='photo-display']");
						var src = img.Attributes["src"].Value;
						body += "<br><br><img src='" + src + "'>";
					} catch (Exception) {
					}
				}
				m = YfrogRe.Match (url);
				if (m.Success) {
					var id = m.Groups[1].Value;
					try {
						var html = Html.Get ("http://yfrog.com/" + id);
						var img = html.SelectSingleNode (@"//img[@id='main_image']");
						var src = img.Attributes["src"].Value;
						body += "<br><br><img src='" + src + "'>";
					} catch (Exception) {
					}
				}
				m = TweetPhotoRe.Match (url);
				if (m.Success) {
					try {
						var html = Html.Get (url);
						var img = html.SelectSingleNode (@"//img[@id='medium_photo']");
						var src = img.Attributes["src"].Value;
						body += "<br><br><img src='" + src + "'>";
					} catch (Exception) {
					}
				}
				m = ShortFlickrRe.Match (url);
				if (m.Success) {
					try {
						var html = Html.Get (url);
						var img = html.SelectSingleNode (@"//img[@class='reflect']");
						var src = img.Attributes["src"].Value;
						body += "<br><br><img src='" + src + "'>";
					} catch (Exception) {
					}
				}
				m = FlickrRe.Match (url);
				if (m.Success) {
					try {
						var html = Html.Get (url);
						var img = html.SelectSingleNode (@"//img[@class='reflect']");
						if (img == null) {
							img = html.SelectSingleNode (@"//img[@class='pc_img']");
						}
						var src = img.Attributes["src"].Value;
						body += "<br><br><img src='" + src + "'>";
					} catch (Exception) {
					}
				}
				m = ImgRe.Match (url);
				if (m.Success) {
					body += "<br><br><img src='" + url + "'>";
				}
				m = YouTubeRe.Match (url);
				if (m.Success) {
					var id = m.Groups[1].Value;
					body += "<br><br>" + Youtube.GetVideoHtml (id);
				}
				m = ShortYouTubeRe.Match (url);
				if (m.Success) {
					var id = m.Groups[1].Value;
					body += "<br><br>" + Youtube.GetVideoHtml (id);
				}
			}
			return body;
		}

		public static OAuthConfig OAuthConfig = new OAuthConfig { 
			ConsumerKey = "XXX", // REPLACE
			Callback = "http://xxx.com/oauth",  // REPLACE
			ConsumerSecret = "XXX",  // REPLACE
			RequestTokenUrl = "https://api.twitter.com/oauth/request_token", 
			AccessTokenUrl = "https://api.twitter.com/oauth/access_token", 
			AuthorizeUrl = "https://api.twitter.com/oauth/authorize" };
	}
	
	public class TwitterOAuthTokens {
		
		[PrimaryKey]
		public string Account { get; set; }
		public string UserId { get; set; }
		public string Token { get; set; }
		public string TokenSecret { get; set; }
		
	}

	public class TwitterAddUI : UIView, ICustomUI
	{
		TwitterAccount _s;
		UIWebView _web;
		bool _started;
		ScanningView _scanner;

		public TwitterAddUI ()
		{
			_s = null;
			_web = new UIWebView ();
			
			KillCookies ();
			
			Frame = new RectangleF (PointF.Empty, new SizeF (200, 200));
			_web.Frame = new RectangleF (PointF.Empty, Frame.Size);
			_web.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
			_web.ScalesPageToFit = true;
			_web.Delegate = new Del (this);
			_web.MultipleTouchEnabled = true;
			ClipsToBounds = true;
			_web.Hidden = true;
			
			Theme.MakeBlack (_web);
			
			_scanner = ScanningView.Start (this, _web.Frame);
			
			AddSubview (_web);
		}
		public void SetModel (object s)
		{
			_s = s as TwitterAccount;
		}

		void KillCookies ()
		{
			var s = NSHttpCookieStorage.SharedStorage;
			var cs = s.CookiesForUrl (new NSUrl ("https://api.twitter.com"));
			foreach (var c in cs) {
				//Console.WriteLine ("TWITTERCOOKIE: {0} = {1}", c.Name, c.Value);
				s.DeleteCookie (c);
			}
		}
		
		OAuthAuthorizer _auth;
		
		void Start() {
			
			_auth = new OAuthAuthorizer(TwitterAccount.OAuthConfig);
			
			if (_auth.AcquireRequestToken()) {
				
				var authUrl = TwitterAccount.OAuthConfig.AuthorizeUrl + "?oauth_token=" + _auth.RequestToken;
				
				_web.LoadRequest (new NSUrlRequest (new NSUrl (authUrl)));
			}
			else {
				// FUCKING TWITTER
				_scanner.StopScanning();
			}
		}

		public override void Draw (RectangleF rect)
		{
			try {
				if (!_started) {
					Start();
					_started = true;
				}
			} catch (Exception error) {
				Log.Error (error);
			}
		}
		public event Action OnOK;

		void Verified (string oauthVerifier)
		{
			if (_auth != null) {
				if (_auth.AcquireAccessToken(oauthVerifier)) {
					_s.Account = _auth.ScreenName;
					_s.Password = _auth.AccessToken;
					
					SaveAccessTokens();
					
					if (OnOK != null) {
						OnOK();
					}
					
				}
				else {
				}
			}
		}
		
		void SaveAccessTokens() {
			using (var repo = new Repo()) {
				
				var tokens = repo.Table<TwitterOAuthTokens>().Where(t => t.Account == _auth.ScreenName).FirstOrDefault();
				
				if (tokens == null) {
					tokens = new TwitterOAuthTokens() {
						Account = _auth.ScreenName,
						Token = _auth.AccessToken,
						TokenSecret = _auth.AccessTokenSecret,
						UserId = _auth.UserId
					};
					repo.Insert(tokens);
				}
				else {
					tokens.Token = _auth.AccessToken;
					tokens.TokenSecret = _auth.AccessTokenSecret;
					tokens.UserId = _auth.UserId;
					repo.Update(tokens);
				}
			}
		}

		class Del : UIWebViewDelegate
		{
			TwitterAddUI _ui;
			bool _visible = false;
			
			public Del (TwitterAddUI ui)
			{
				_ui = ui;
			}

			public override void LoadingFinished (UIWebView webView)
			{
				try {
					if (!_visible) {
						_ui._scanner.StopScanning ();
						_ui._web.Alpha = 0;
						_ui._web.Hidden = false;
						UIView.BeginAnimations ("FbIn");
						_ui._web.Alpha = 1.0f;
						UIView.CommitAnimations ();
						_visible = true;
					}
					
					var url = webView.Request.Url.AbsoluteString;
					var uri = new Uri(url);
					var query = Http.ParseQueryString(uri.Query);
					
					if (query.ContainsKey("oauth_token") && query.ContainsKey("oauth_verifier")) {
						_ui.Verified (query["oauth_verifier"]);
					}
				} catch (Exception error) {
					Log.Error (error);
				}
			}
		}
	}

	public class TwitterSearch : RssSource, IHelpful
	{
		public string Search { get; set; }

		public override bool Matches (Source other)
		{
			var o = other as TwitterSearch;
			return (o != null) && (o.Search == Search);
		}

		public string HelpForProperty (string propName)
		{
			if (propName == "Search") {
				return "is what to search for on Twitter";
			}
			return "";
		}

		public override TimeSpan GetExpirationDuration ()
		{
			return TimeSpan.FromMinutes (20);
		}

		public override string GetDistinguisher ()
		{
			return Search;
		}
		public override string GetDistinguisherName ()
		{
			return "SEARCH";
		}

		public override bool ShouldShowSubjectWithBody {
			get { return false; }
		}

		protected override string FilterBodyHtml (string bodyHtml)
		{
			return TwitterAccount.Enrich (bodyHtml);
		}

		public override string GetUrl ()
		{
			var url = string.Format ("http://search.twitter.com/search.atom?q={0}", Uri.EscapeDataString (Search));
			return url;
		}
	}
	
}
