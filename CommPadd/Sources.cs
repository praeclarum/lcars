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
using System.Collections.Generic;

namespace CommPadd
{
	public class SourceRef {
		public int Id { get; set; }
		public string SourceType { get; set; }		
	}
	
	public abstract class Source {
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public DateTime LastReadTime { get; set; }
		public DateTime LastUpdateTime { get; set; }
		public DateTime ExpirationTime { get; set; }
		public bool IsActive { get; set; }
		
		public Source() {
			LastReadTime = DateTime.MinValue;
			LastUpdateTime = DateTime.MinValue;
			ExpirationTime = DateTime.MinValue;
			IsActive = true;
		}
		
		public SourceRef Reference {
			get { 
				return new SourceRef() {
					Id = Id,
					SourceType = GetType().Name
				};
			}
		}
		
		public virtual string GetHtmlBase() {
			return "http://";
		}
		
		public abstract bool Matches(Source other);
		
		public virtual bool ShouldShowSubjectWithBody {
			get { return true; }
		}		
		
		Repo repo = null;
		Action<Source> _act = null;
		DateTime _lastUpdateActTime;
		public void Update(Repo r, Action<Source> act) {
			_lastUpdateActTime = DateTime.UtcNow.AddHours(-1);
			repo = r;
			_act = act;
			DoUpdate();
		}
		
		public abstract string GetDistinguisher();
		public abstract string GetDistinguisherName();
		
		public string GetTinyTitle() {
			return GetShortDistinguisher().ToUpperInvariant().TruncateChars(5);
		}
		
		string _shortDist;		
		
		static Regex KillRe = new Regex(@"(\.ORG|\.NET|\.COM|WWW\.|BLOG\.|\.EDU|BLOGS\.|WEBLOGS|/FEED/?|/BLOGS/?|FEED://|FEEDS\.FEEDBURNER|FEEDS\.|RSS\.XML|HTTP\://|HTTPS\://|/|\.)",
		                                RegexOptions.IgnoreCase);
		
		public string GetShortDistinguisher() {
			
			if (_shortDist != null) return _shortDist;
			
			var r = GetDistinguisher();
			
			var lastSlash = r.LastIndexOf('/');
			if (lastSlash > 5) {
				if (r.IndexOf('.', lastSlash) > 0 || r.IndexOf('=', lastSlash) > 0) {
					r = r.Substring(0, lastSlash);
				}
			}
			r = KillRe.Replace(r, " ");
			r = r.Replace("_", " ");
			var parts = r.Split(' ');
			var rs = new StringBuilder();
			var head = "";
			foreach (var p in parts) {
				if (p.Length > 1) {
					rs.Append(head);
					rs.Append(p);
					head = " ";
				}
			}
			r = rs.ToString();
			
			if (r.Length == 0) {
				r = GetDistinguisher();
			}
			
			_shortDist = r;
			return r;
		}
				
		protected Message GetMessageByRawId(string rawId) {
			var st = GetType().Name;
			var q = from m in repo.Table<Message>()
					where m.RawId == rawId && m.SourceId == Id && m.SourceType == st
					select m;
			var msg = q.FirstOrDefault();
			if (msg == null) {
				msg = new Message();
				msg.RawId = rawId;
				msg.SourceId = Id;
				msg.SourceType = st;
			}
			return msg;
		}
		
		static Regex VimeoRe = new Regex(@"http://vimeo.com/([0-9]+)");
		static Regex MediaRe = new Regex(@"(\.mp3|\.mp4|\.mov)$");		
		
		void AddMediaLink (Message m)
		{
			if (m.MediaUrl == "") {				
				var links = Html.GetAllLinks(m.BodyHtml);
				foreach (var url in links) {
					if (m.MediaUrl != "") break;
					
					var ma = VimeoRe.Match(url);
					if (ma.Success) {
						m.MediaUrl = VimeoSearch.GetVideoUrl(ma.Groups[1].Value);
					}
					else {
						ma = MediaRe.Match(url);
						if (ma.Success) {
							m.MediaUrl = url;
						}
					}
				}
			}
		}
		
		protected void Save(Message m) {
			PostProcess(m);
			AddMediaLink(m);
			m.SourceType = GetType().Name;
			m.SourceId = Id;
			if (m.Id == 0) repo.Insert(m);
			else repo.Update(m);
			
			var now = DateTime.UtcNow;
			var timeSinceAct = DateTime.UtcNow - _lastUpdateActTime;
			if (_act != null && timeSinceAct > TimeSpan.FromSeconds(2)) {
				_lastUpdateActTime = now;
				_act(this);				
			}
		}
		
		protected virtual void PostProcess(Message m) { }
		public abstract TimeSpan GetExpirationDuration();
		protected abstract void DoUpdate();
	}
	
	public class SourceTypes {
		public static Type[] All;
		
		public static int Id(Source source) {
			return Array.IndexOf(All, source.GetType());
		}
		
		public static string GetTitle(Type t) {
			string s;
			if (!TypeTitles.TryGetValue(t, out s)) {
				s = Theme.GetTitle(t.Name);
				TypeTitles[t] = s;
			}
			return s;
		}
		
		static Dictionary<Type, string> TypeTitles = new Dictionary<Type, string>();
		
		static SourceTypes() {
			var tys = typeof(Source).Assembly.GetTypes();
			var q = from t in tys
					where t.IsSubclassOf(typeof(Source)) && !t.IsAbstract
					orderby t.Name
					select t;
			All = q.ToArray();
		}

		public static Type Get (string sourceTypeName)
		{
			var q = from s in All where s.Name == sourceTypeName select s;
			return q.FirstOrDefault();
		}
	}

}