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
using System.Threading;
using System.Linq;
using System.Net;
using System.Collections.Generic;

namespace CommPadd
{
	public class GoogleReaderConfig {
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string Account { get; set; }
		public string Password { get; set; }
		public DateTime LastUpdateTime { get; set; }
		
		public bool IsValid {
			get {
				return !string.IsNullOrEmpty(Account) && !string.IsNullOrEmpty(Password);
			}
		}
	}

	public class GoogleReaderUpdater
	{
		public static void Start() {
			var t = new Thread((ThreadStart)delegate {
				new GoogleReaderUpdater().Run();
			});
			t.Start();
		}
		
		CookieContainer _cookies;
		
		static AutoResetEvent _wakeup = new AutoResetEvent(false);
		
		public static DateTime LastUpdateTime { get; private set; }
		
		public GoogleReaderUpdater() {
			_cookies = new CookieContainer();
		}
		
		public static void SetReaderChanged() {
			_wakeup.Set();
		}
		
		void LogIn(GoogleReaderConfig conf) {
			var url = "https://www.google.com/accounts/ServiceLogin?hl=en&nui=5&service=reader&ltmpl=mobile&btmpl=mobile&continue=http%3A%2F%2Fwww.google.com%2Freader%2Fm%2Fsubscriptions";
			var req = Http.NewRequest(url);
			req.CookieContainer = _cookies;
			
			var rawResp = Http.ReadResponse(req);
			var resp = Html.Parse(rawResp);
			
			//Console.WriteLine (rawResp);
			
			var form = resp.SelectSingleNode("//form[@id='gaia_loginform']");
			
			var action = form.Attributes["action"].Value;
			
			var inputs = new Dictionary<string, string>();
			foreach (HtmlAgilityPack.HtmlNode i in form.SelectNodes("//input")) {
				var ka = i.Attributes["name"];
				if (ka == null) continue;
				
				var key = i.Attributes["name"].Value;
				var val = "";
				var va = i.Attributes["value"];
				if (va != null) {
					val = va.Value;
				}
				inputs[key] = val;
			}
			
			inputs["Email"] = conf.Account;
			inputs["Passwd"] = conf.Password;
			
			Http.Post(action, inputs, _cookies);
		}
		
		Dictionary<string, string> GetSubscriptions() {
			
			var rawSubs = Http.Get("http://www.google.com/reader/m/subscriptions", _cookies);
			
			var html = Html.Parse(rawSubs);
			
			var subs = html.SelectNodes("//li/a");
			
			var links = new Dictionary<string,string>();
			foreach (HtmlAgilityPack.HtmlNode a in subs) {
				var t = a.InnerText;
				var href = a.Attributes["href"].Value;
				href = href.Replace("/reader/m/view/feed%2F", "");
				href = href.Replace("?hl=en", "");
				href = Uri.UnescapeDataString(href);
				links.Add(t, href);
			}
			
			return links;
		}
		
		void Subscribe(Dictionary<string, string> subs) {			
			var sourceType = typeof(Blog);
			
			using (var repo = new Repo()) {				
				var sources = repo.GetActiveSources(sourceType);
				var existingSources = new Dictionary<string, Blog>();
				foreach (Blog s in sources) {
					existingSources.Add(s.Url, s);
				}
				
				foreach (var s in subs) {
					var url = s.Value;
					if (!existingSources.ContainsKey(url)) {
						var source = new Blog();
						source.Url = url;
						repo.Insert(source);
						Console.WriteLine ("GU: Subscribed to " + url);
					}					
				}
			}
			
			SourceUpdater.SetSourcesChanged();
			App.RunUI(delegate {
				App.Inst.RefreshInfo();
			});
		}
		
		void MarkRead() {
		}
		
		TimeSpan Update() {
			GoogleReaderConfig info = null;
			
			var now = DateTime.UtcNow;
			
			var updateInterval = TimeSpan.FromHours(8);
			
			using (var repo = new Repo()) {
				info = repo.Table<GoogleReaderConfig>().FirstOrDefault();
			}
			
			if (info == null || !info.IsValid) {
				return TimeSpan.FromHours(10);
			}
			
			if ((now - info.LastUpdateTime) < updateInterval) {				
				return updateInterval - (now - info.LastUpdateTime);				
			}
			
			Console.WriteLine ("GU: Gathering subscriptions");
			
			LogIn(info);
			
			var subs = GetSubscriptions();
			
			Subscribe(subs);
			
			MarkRead();
			
			using (var repo = new Repo()) {
				info.LastUpdateTime = now;
				repo.Update(info);
			}
			
			return TimeSpan.FromHours(8);
		}
		
		void Run() {
			
			
			using (var repo = new Repo()) {
				var info = repo.Table<GoogleReaderConfig>().FirstOrDefault();
				if (info != null) {
					info.Account = "";
					info.Password = "";
					repo.Update(info);
				}
			}

			/*
			
			
			var done = false;
			while (!done) {
				var sleepTime = TimeSpan.FromMinutes(1);
				
				try {
					sleepTime = Update();
				}
				catch (Exception dataEx) {
					Console.WriteLine (dataEx.ToString());
				}					
				
				_wakeup.WaitOne(sleepTime);
			}
			*/
		}
	}
}
