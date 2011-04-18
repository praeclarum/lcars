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
using System.Net;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace CommPadd
{

	public class Http {
		public static string Get(string url) {
			return Get(url, null, null, null);
		}
		public static string Get(string url, CookieContainer cookies) {
			return Get(url, null, null, cookies);
		}
		public static HttpWebRequest NewRequest(string url) {
			if (url.StartsWith("feed://")) {
				url = url.Replace("feed://", "http://");
			}
//			Console.WriteLine ("R> " + url);
//			Debug.PrintStack();
			var req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = "Mozilla/5.0 (Macintosh; U; Intel Mac OS X 10_6_2; en-us) AppleWebKit/531.21.8 (KHTML, like Gecko) Version/4.0.4 Safari/531.21.10";
			req.Accept = "application/xml,application/xhtml+xml,text/html;q=0.9,text/plain;q=0.8,image/png,*/*;q=0.5";
			req.AllowAutoRedirect = true;
			req.AutomaticDecompression = DecompressionMethods.GZip|DecompressionMethods.Deflate;
			req.Timeout = 60000;
			req.ReadWriteTimeout = 60000;
			return req;
		}
		public static string Get(string url, string username, string password, CookieContainer cookies) {
			var req = NewRequest(url);
			
			if (cookies != null) {
				req.CookieContainer = cookies;
			}

			if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)) {
				var authInfo = username + ":" + password;
			    authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
			    req.Headers["Authorization"] = "Basic " + authInfo;
			}
			
			return ReadResponse(req);
		}
		public static string ReadResponse(WebRequest req) {
			using (var resp = (HttpWebResponse)req.GetResponse()) {
				Encoding enc = null;
				try {
					enc = Encoding.GetEncoding(resp.CharacterSet);
				}
				catch (Exception) {
					enc = Encoding.Default;
				}
				using (var s = resp.GetResponseStream()) {
					using (var r = new StreamReader(s, enc)) {
						return r.ReadToEnd();
					}
				}
			}
		}
		public static string Post(string url, Dictionary<string,string> formInputs) {
			return Post(url, formInputs, null);
		}
		public static string Post(string url, Dictionary<string,string> formInputs, CookieContainer cookies) {
			var req = NewRequest(url);
			if (cookies != null) {
				req.CookieContainer = cookies;
			}
			req.Timeout = 60000;
			req.Method = "POST";
			req.ContentType = "application/x-www-form-urlencoded";
			var body = MakeQueryString(formInputs);
			var bodyBytes = Encoding.UTF8.GetBytes(body);
			req.ContentLength = bodyBytes.Length;
			var s = req.GetRequestStream();
			s.Write(bodyBytes, 0, bodyBytes.Length);
			
			return ReadResponse(req);
		}
		
		public static string MakeQueryString(Dictionary<string,string> formInputs) {
			var body = "";
			var head = "";
			foreach (var input in formInputs) {
				body += head;
				body += Uri.EscapeDataString(input.Key);
				body += "=";
				body += Uri.EscapeDataString(input.Value);
				head = "&";
			}
			return body;
		}

		public static string Resolve (string url)
		{
			var req = NewRequest(url);
			using (var resp = (HttpWebResponse)req.GetResponse()) {
				var r = resp.ResponseUri;
				return r.ToString();
			}
		}
		
		public static Dictionary<string,string> ParseQueryString(string queryString) {
			var r = new Dictionary<string,string>();			
			var kvs = queryString.Split('&');
			
			foreach (var kv in kvs) {				
				var parts = kv.Split('=');
				
				var k = "";
				var v = "";
				
				if (parts.Length == 2) {					
					k = Uri.UnescapeDataString(parts[0]);
					v = Uri.UnescapeDataString(parts[1]);
				}
				else {
					k = Uri.UnescapeDataString(kv);
				}
				
				if (k.StartsWith("?")) {
					k = k.Substring(1);
				}
				
				if (k.Length > 0) {
					r[k] = v;
				}
			}
			
			return r;
		}
		
	}	

}
