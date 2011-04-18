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
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace CommPadd
{


	public static class InternetTime
	{
		class TZ
		{
			public readonly Regex RE;
			public readonly Func<Match, TimeSpan> Parser;
			public TZ (string re, Func<Match, TimeSpan> f)
			{
				RE = new Regex (re);
				Parser = f;
			}
		}
		static List<TZ> TimeZones;

		static Regex TZRE = new Regex (@"^([^\s]+).*UTC\s+([^h]+)hours?");

		static InternetTime ()
		{
			TimeZones = new List<TZ> { new TZ (@"([\-\+])(\d\d)(\:|)(\d\d)$", m =>
			{
				var neg = m.Groups[1].Value == "-";
				var h = int.Parse (m.Groups[2].Value) + int.Parse (m.Groups[4].Value) / 60.0;
				if (neg)
					h *= -1;
				var o = TimeSpan.FromHours (h);
				return o;
			}), new TZ (@"Z$", m => TimeSpan.Zero), new TZ (@" GMT\+00\:00", m => TimeSpan.Zero), new TZ (@" \+0000$", m => TimeSpan.Zero), new TZ (@" \+0000 ", m => TimeSpan.Zero), new TZ (@" Z$", m => TimeSpan.Zero), new TZ (@" GMT$", m => TimeSpan.Zero), new TZ (@" UTC$", m => TimeSpan.Zero) };
			
			try {
				using (var f = File.OpenText ("TimeZones.txt")) {
					for (var line = f.ReadLine (); line != null; line = f.ReadLine ()) {
						var m = TZRE.Match (line);
						if (m.Success) {							
							var d = ParseTimeSpan(m.Groups[2].Value);
							var tz = new TZ(
								" " + m.Groups[1].Value.Trim() + "$",
								match => d
							);
							TimeZones.Add(tz);
						}
					}
				}
			} catch (Exception ex) {
				Console.WriteLine ("! " + ex);
			}
			
			TimeZones.Add (new TZ (@" [A-Z]+$", m => TimeSpan.Zero));
			// Catch all
		}
		static TimeSpan ParseTimeSpan(string s) {
			var ts = s.Trim();
			var pos = true;
			if (ts[0] == '-') pos = false;
			ts = ts.Substring(2).Trim();
			var c = ts.IndexOf(":");
			var h = 0.0;
			if (c > 0) {
				h = double.Parse(ts.Substring(0, c));
				var m = int.Parse(ts.Substring(c+1));
				h += m / 60.0;
			}
			else {
				h = double.Parse(ts);
			}
			var d = TimeSpan.FromHours(h);
			return pos ? d : -d;
		}
		static string[] DateTimeFormats = new string[] { 
			"ddd, d MMM yyyy H:mm:ss", 
			"d MMM yyyy H:mm:ss", 
			"yyyy-MM-dd\"T\"HH:mm:ss", 
			"ddd MMM d HH:mm:ss yyyy" };
		public static DateTime Parse (string timeStr)
		{			
			if (timeStr.Length == 0) return DateTime.UtcNow;
			
			//Console.WriteLine ("P: " + timeStr);
			var src = timeStr;
			TimeSpan? tz = null;
			foreach (var t in TimeZones) {
				var m = t.RE.Match (timeStr);
				if (m.Success) {
					tz = t.Parser (m);
					src = t.RE.Replace (src, " ");
					//Console.WriteLine ("{0} -> {1} ({2})", timeStr, src, tz);
				}
			}
			if (tz == null) {
				tz = TimeSpan.Zero;
				Console.WriteLine ("NO TZ!!: " + timeStr);
			}
			
			var local = DateTime.MinValue;
			
			var styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces;
			
			if (DateTime.TryParse (src, CultureInfo.InvariantCulture, styles, out local)) {
				// GOOD
			} else {
				if (DateTime.TryParseExact (src, DateTimeFormats, CultureInfo.InvariantCulture, styles, out local)) {
					// GOOD
				} else {
					Console.WriteLine ("BAAAAAAAAAAAAD: " + timeStr);
				}
			}
			
			var r = local - tz.Value;
			//Console.WriteLine ("{0} =====> {1}", local, r);
			
			return r;
		}
	}
	
}
