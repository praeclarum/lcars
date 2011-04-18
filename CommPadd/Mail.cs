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
using System.Collections.Generic;

namespace CommPadd
{
	public abstract class Mail : Source {
		public string EmailAddress { get; set; }
		public string Password { get; set; }
		
		string PopUsername { get; set; }
		string PopServer { get; set; }
		int PopServerPort { get; set; }
		
		public Mail() {
			EmailAddress = "";
			Password = "";
			PopUsername = "";
			PopServer = "";
			PopServerPort = 110;
		}
		
		public override bool Matches (Source other)
		{
			var o = other as Mail;
			return (o != null) && (o.EmailAddress == EmailAddress);
		}

		public override string GetDistinguisher ()
		{
			return EmailAddress;
		}
		
		public override string GetDistinguisherName ()
		{
			return "EMAIL";
		}
		
		public override TimeSpan GetExpirationDuration() {
			return TimeSpan.FromMinutes(30);
		}

		protected override void DoUpdate ()
		{
			Console.WriteLine ("MAIL does not work yet");
		}
		
		void DeterminePopServer() {
			if (!string.IsNullOrEmpty(PopServer)) return;
		}
		
		string GetHost() {
			var i = EmailAddress.IndexOf("@");
			if (i < 0) return "";
			else if (i == EmailAddress.Length-1) return "";
			else return EmailAddress.Substring(i+1).Trim();
		}
		
		string GetUsername() {
			var i = EmailAddress.IndexOf("@");
			if (i < 0) return "";
			else return EmailAddress.Substring(0, i).Trim();
		}
		
		IEnumerable<string> GetUsernames() {
			yield return EmailAddress.Trim();
			yield return GetUsername();			
		}
			
		int[] GetPorts() {
			return new int[] { 110, 995 };
		}
		
		IEnumerable<string> GetPopServers() {
			var host = GetHost();
			yield return "pop." + host;
			yield return "pop.gmail.com";
		}
	}
	
}
