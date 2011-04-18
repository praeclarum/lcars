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

namespace CommPadd
{

	public class ShareUpdater
	{

		public ShareUpdater ()
		{
		}
		
		public static void Start() {
			var t = new Thread((ThreadStart)delegate {
				new ShareUpdater().Run();
			});
			t.Start();
		}
		
		static AutoResetEvent _wakeup = new AutoResetEvent(false);
		
		public static DateTime LastUpdateTime { get; private set; }
		
		public static void SetSharesChanged() {
			_wakeup.Set();
		}
		
		TimeSpan UpdateNextShare() {
			ShareMessage sh = null;
			Message m = null;
			
			using (var repo = new Repo()) {
				sh = repo.Table<ShareMessage>().Where(s => s.Status == ShareMessageStatus.Unsent).FirstOrDefault();
				if (sh != null) {
					m = repo.Table<Message>().Where(mm => mm.Id == sh.MessageId).FirstOrDefault();
				}				
			
				if (sh == null) {
					// Nothing to do
					return TimeSpan.FromMinutes(1);
				}
				
				if (sh != null && m == null) {
					// Missing the message, must have been deleted
					sh.Status = ShareMessageStatus.Abandoned;
					repo.Update(sh);
					return TimeSpan.FromSeconds(2);
				}
			}
			
			
			//
			// Ready to send!
			//
			var post = new Dictionary<string, string>();
			post["url"] = m.Url;
			post["title"] = m.Subject;
			post["from"] = sh.From;
			post["to"] = sh.To;
			
			try {
				Http.Post("http://lcarsreader.com/Share/Send", post);
				
				sh.Status = ShareMessageStatus.Sent;
				using (var repo = new Repo()) {
					repo.Update(sh);
				}
				Console.WriteLine ("SU: Successfully posted share for " + m.Subject);
				return TimeSpan.FromSeconds(2);
			}
			catch (Exception ex) {
				Console.WriteLine ("SU: ERROR: " + ex.Message);
				return TimeSpan.FromMinutes(1);
			}
		}
		
		void Run() {
			var done = false;
			while (!done) {
				var sleepTime = TimeSpan.FromMinutes(1);
				
				try {
					sleepTime = UpdateNextShare();
				}
				catch (Exception dataEx) {
					Console.WriteLine (dataEx.ToString());
				}					
				
				_wakeup.WaitOne(sleepTime);
			}
		}
	}
}
