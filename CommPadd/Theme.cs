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
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.AudioToolbox;

namespace CommPadd
{
	public class Theme
	{
		static public UIFont RidiculousFont;
		static public UIFont HugeFont;
		static public UIFont TitleFont;
		static public UIFont InputFont;
		static public UIFont TextFont;
		static public UIFont ComponentFont;
		static public UIFont SmallComponentFont;
		
		static public UIColor TextColor;
		static public UIColor HeadingColor;
		static public UIColor TitleColor;
		
		public static string FormatTime(DateTime utc) {
			var t = utc;
			return string.Format("{0:00}{1:00}{2:00}.{3:00}",
			              t.Year-2000, t.Month, t.Day, (int)(100.0*t.TimeOfDay.TotalHours / 24.0));
		}
		
		static UIFont GetFont(float size) {
			return GetFont(size, size / 2);
		}
		static UIFont GetFont(float size, float smallSize) {
			var f = UIFont.FromName("lcars", size);
			if (f == null) {
				f = UIFont.BoldSystemFontOfSize(smallSize);
			}
			return f;
		}
		
		public static void Init() {
			RidiculousFont = GetFont(196);
			HugeFont = GetFont(96);
			TitleFont = GetFont(48);
			InputFont = GetFont(App.Inst.IsIPad ? 48 : 32);
			TextFont = GetFont(25);
			ComponentFont = GetFont(20, 9.5f);
			SmallComponentFont = GetFont(14);
			
			TextColor = UIColor.FromRGB(255/255.0f, 159/255.0f, 0/255.0f);
			TitleColor = TextColor;
			HeadingColor = UIColor.FromRGB(156/255.0f, 156/255.0f, 255/255.0f);
		}
		
		public static string GetTitle(string s) {
			var d = "";
			bool lastLower = false;
			foreach (var c in s) {
				if (lastLower && char.IsUpper(c)) {
					d += " ";
				}
				d += char.ToUpperInvariant(c);
				lastLower = char.IsLower(c);
			}
			return d;
		}
		
		public static float VerticalButtonsWidth {
			get {
				return App.Inst.IsIPad ? 160 : 60;
			}
		}
		
		public static float HorizontalBarHeight {
			get {
				return App.Inst.IsIPad ? 20 : 10;
			}
		}
		
		public static void MakeBlack (UIView v)
		{
			v.BackgroundColor = UIColor.Black;
			foreach (var s in v.Subviews) {
				MakeBlack (s);
			}
		}
	}
	
	public static class Lcars {
		public static UIColor HexColor (string paramValue)
		{
			if (paramValue.StartsWith ("#")) {
				paramValue = paramValue.Substring (1);
			}
			if (paramValue.Length != 6)
				return UIColor.Black;
			var red = (System.Int32.Parse (paramValue.Substring (0, (2) - (0)), System.Globalization.NumberStyles.AllowHexSpecifier));
			var green = (System.Int32.Parse (paramValue.Substring (2, (4) - (2)), System.Globalization.NumberStyles.AllowHexSpecifier));
			var blue = (System.Int32.Parse (paramValue.Substring (4, (6) - (4)), System.Globalization.NumberStyles.AllowHexSpecifier));
			return UIColor.FromRGB (red / 255f, green / 255f, blue / 255f);
		}
		
		public static readonly Dictionary<LcarsComponentType, UIColor> ComponentColors = new Dictionary<LcarsComponentType, UIColor> { 
			{ LcarsComponentType.UnavailableFunction, HexColor ("3366cc") }, 
			{ LcarsComponentType.SystemFunction, HexColor ("99ccff") },
			{ LcarsComponentType.MiscFunction, HexColor ("cc99cc")  },
			{ LcarsComponentType.CriticalFunction, HexColor ("ffcc00") },
			{ LcarsComponentType.NavigationFunction, HexColor ("ffff99") },
			{ LcarsComponentType.DisplayFunction, HexColor ("cc6666") },
			{ LcarsComponentType.PrimaryFunction, HexColor ("ffffff") },
			{ LcarsComponentType.Static, HexColor ("ffcc66") },
			{ LcarsComponentType.Gray, HexColor ("666666") },
		};
	}

}
