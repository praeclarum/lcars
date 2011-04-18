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
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.UIKit;
using System.Text.RegularExpressions;
using System.Net;
using MonoTouch.Foundation;

namespace CommPadd
{

	public class SourceUpdater
	{

		public SourceUpdater ()
		{
		}
		
		public static void Start() {
			var t = new Thread((ThreadStart)delegate {
				new SourceUpdater().Run();
			});
			t.Start();
		}
		
		static AutoResetEvent _wakeup = new AutoResetEvent(false);
		
		public static DateTime LastUpdateTime { get; private set; }
		
		public static void SetSourcesChanged() {
			_wakeup.Set();
		}
		
		public static void ForceRefresh(SourceRef first) {
			forceRefresh = true;
			forceSource = first;
			_wakeup.Set();
		}

		static bool forceRefresh;
		static SourceRef forceSource;
		
		List<Source> sourcesToUpdate = new List<Source>();
		
		TimeSpan UpdateNextSource() {
			using (var repo = new Repo()) {
				
				if (sourcesToUpdate.Count == 0) {				
					var sources = repo.GetActiveSources();

					var q = from s in sources
							let e = s.ExpirationTime
							orderby e
							select s;
					
					sourcesToUpdate.AddRange(q);
				}
				
				if (sourcesToUpdate.Count == 0) {
					Console.WriteLine ("U: No Sources");
					return TimeSpan.FromMinutes(5);
				}
				
				var newest = sourcesToUpdate[0];
				sourcesToUpdate.RemoveAt(0);
				
				var now = DateTime.UtcNow;
				
				if (newest.ExpirationTime > now) {
					Console.WriteLine ("U: Cache is good");
					sourcesToUpdate.Clear();
					return newest.ExpirationTime - now;
				}
				else {					
					return UpdateSource(repo, newest);
				}
			}
		}
		
		TimeSpan UpdateAllSources() {
			using (var repo = new Repo()) {				
				Action<Source> up = s => {
					try {
						UpdateSource(repo, s);
					}
					catch (Exception ex) {
						Console.WriteLine (ex);
					}				
				};
				
				var q = from s in repo.GetActiveSources()
						orderby s.GetExpirationDuration()
						select s;
				var sources = q.ToArray();
				foreach (var s in sources) {
					up(s);
				}				
			}
			return TimeSpan.FromMinutes(5);
		}
		
		public static event Action<Source> SourceWasUpdated;
		
		
		bool _informing;
		NSTimer _informingTimer;
		
		void InformUpdate(Source s) {
			if (_informing) return;
			
			_informing = true;
			App.RunUI(delegate {
		
				_informingTimer = NSTimer.CreateScheduledTimer(TimeSpan.FromSeconds(2),
				                                               delegate {
					_informing = false;
					
//					Console.WriteLine ("=========================================");
//					Console.WriteLine ("SOURCE UPDATER INFORMING");
					
					if (SourceWasUpdated != null) {
						SourceWasUpdated(s);
					}
				});
			});
		}
		
		TimeSpan UpdateSource(SourceRef r) {
			using (var repo = new Repo()) {
				var s = repo.Resolve(r);
				return UpdateSource(repo, s);
			}
		}
		
		TimeSpan UpdateSource(Repo repo, Source newest) {
			newest.LastUpdateTime = DateTime.UtcNow;
			newest.ExpirationTime = newest.LastUpdateTime + newest.GetExpirationDuration();
			repo.Update(newest);
			try {
				Console.WriteLine ("U: Updating {0} {1}", newest.GetType().Name, newest.GetDistinguisher());
				newest.Update(repo, InformUpdate);
				
				LastUpdateTime = DateTime.UtcNow;
				
				InformUpdate(newest);				
				
				return TimeSpan.FromSeconds(2);
			}
			catch (Exception ex) {
				Console.WriteLine ("U: Fail: {0}: {1}", ex.GetType().Name, ex);
				
				var webex = ex as WebException;
				if (webex != null && webex.Response != null && ((HttpWebResponse)webex.Response).StatusCode == HttpStatusCode.NotFound) {
					return TimeSpan.FromSeconds(2);
				}
				else {
					return TimeSpan.FromSeconds(30);
				}
			}
		}
		
		void Run() {
			var done = false;
			while (!done) {
				var sleepTime = TimeSpan.FromMinutes(1);
									
				App.RunUI(delegate {
					UIApplication.SharedApplication.NetworkActivityIndicatorVisible = true;
				});
				
				try {
					if (forceRefresh) {
						if (forceSource != null) {
							sleepTime = UpdateSource(forceSource);
						}
						else {
							sleepTime = UpdateAllSources();
						}
						forceRefresh = false;
					}
					else {						
						sleepTime = UpdateNextSource();
					}
				}
				catch (Exception dataEx) {
					Console.WriteLine (dataEx.ToString());
				}
				
				App.RunUI(delegate {
					UIApplication.SharedApplication.NetworkActivityIndicatorVisible = false;
				});
				
				_wakeup.WaitOne(sleepTime);
			}
		}
	}
}
