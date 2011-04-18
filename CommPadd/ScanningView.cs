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
using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.Foundation;

namespace CommPadd
{
	public class ScanningView : UIView
	{
		Reticle _ret;
		NSTimer _moveTimer;
		Random _rand = new Random ();
		BG _bg;

		float BGW = 1024;

		public static ScanningView Start (UIView View, RectangleF frame)
		{
			var _scanner = new ScanningView (frame);
			_scanner.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
			_scanner.Alpha = 0;
			_scanner.StartScanning ();
			View.AddSubview (_scanner);
			UIView.BeginAnimations ("RetFadeIn");
			_scanner.Alpha = 1;
			UIView.CommitAnimations ();
			return _scanner;
		}

		public ScanningView (RectangleF frame) : base(frame)
		{
			if (App.Inst.IsIPad) {
				BGW = 1024;
			} else {
				BGW = 1024;
			}
			_bg = new ScanningView.BG (new RectangleF (-BGW, -BGW, 2 * BGW, 2 * BGW));
			AddSubview (_bg);
			var rw = Math.Min (frame.Width, frame.Height) / 2;
			_ret = new ScanningView.Reticle (new RectangleF ((float)(_rand.NextDouble () - 0.5) * frame.Width * 2, (float)(_rand.NextDouble () - 0.5) * frame.Height * 2, rw, rw));
			_ret.Alpha = 0.7f;
			ClipsToBounds = true;
			Layer.CornerRadius = 20;
			AddSubview (_ret);
		}

		public void StartScanning ()
		{
			var moveTime = TimeSpan.FromSeconds (1.25);
			Scan ();
			_moveTimer = NSTimer.CreateRepeatingScheduledTimer (moveTime, Scan);
		}
		public void Scan ()
		{
			var rsize = _ret.Frame.Size;
			var scale = (float)(1.0 - 0.6 + 0.2 * (_rand.NextDouble ()));
			rsize.Width *= scale;
			rsize.Height *= scale;
			var box = new RectangleF (0, 0, Frame.Width - rsize.Width, Frame.Height - rsize.Height);
			var x = (float)_rand.NextDouble () * box.Width;
			var y = (float)_rand.NextDouble () * box.Height;
			
			BeginAnimations ("Scan_MoveRet");
			SetAnimationDuration (1.25);
			_ret.Frame = new RectangleF (x, y, rsize.Width, rsize.Height);
			_bg.Frame = new RectangleF (x - BGW, y - BGW, 2 * BGW, 2 * BGW);
			CommitAnimations ();
		}

		public void StopScanning ()
		{
			UIView.BeginAnimations ("ScanOut");
			Alpha = 0;
			UIView.CommitAnimations ();
			NSTimer.CreateScheduledTimer (TimeSpan.FromSeconds (1), delegate {
				try {
					RemoveFromSuperview ();
				} catch (Exception ex) {
					Log.Error (ex);
				}
			});
		}

		public class BG : UIView
		{
			static UIColor back = UIColor.FromPatternImage (UIImage.FromFile ("ScanningBackground.jpg"));

			public BG (RectangleF frame) : base(frame)
			{
				BackgroundColor = back;
				ClipsToBounds = true;
			}
		}

		public class Reticle : UIView
		{
			LcarsComp TL, TR, BR, BL;

			public Reticle (RectangleF frame) : base(frame)
			{
				TL = new LcarsComp ();
				TL.Shape = LcarsShape.TopLeft;
				BR = new LcarsComp ();
				BR.Shape = LcarsShape.BottomRight;
				TR = new LcarsComp ();
				TR.Shape = LcarsShape.TopRight;
				BL = new LcarsComp ();
				BL.Shape = LcarsShape.BottomLeft;
				TL.Def.ComponentType = TR.Def.ComponentType = BL.Def.ComponentType = BR.Def.ComponentType = LcarsComponentType.SystemFunction;
				TL.OuterCorner = BL.OuterCorner = TR.OuterCorner = BR.OuterCorner = 0.75f;
				TL.InnerCorner = BL.InnerCorner = TR.InnerCorner = BR.InnerCorner = 3.5f;
				AddSubview (TL);
				AddSubview (BL);
				AddSubview (TR);
				AddSubview (BR);
				LayoutSubviews ();
			}

			public override void LayoutSubviews ()
			{
				try {
					var r = 16;
					
					TL.Width = TR.Width = r / 2;
					BL.Width = BR.Width = r / 2;
					TL.Height = TR.Height = r;
					BL.Height = BR.Height = r;
					
					var h = Frame.Width / 2;
					var s = h / 2;
					if (s < 32)
						s = h;
					
					TL.Frame = new RectangleF (0, 0, s, h);
					TR.Frame = new RectangleF (2 * h - s, 0, s, h);
					BL.Frame = new RectangleF (0, h, s, h);
					BR.Frame = new RectangleF (2 * h - s, h, s, h);
					
					TL.AutoresizingMask = TR.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
					BL.AutoresizingMask = BR.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
				} catch (Exception error) {
					Log.Error (error);
				}
			}
		}
		
	}
}
