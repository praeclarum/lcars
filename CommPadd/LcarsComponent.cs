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
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.CoreGraphics;

namespace CommPadd
{
	public enum LcarsShape
	{
		Right = 0,
		TopRight = 45,
		Top = 90,
		TopLeft = 135,
		Left = 180,
		BottomLeft = 225,
		Bottom = 270,
		BottomRight = 315,
		Button = 1001
		//ChevronRight = 2001
	}

	public enum LcarsComponentType
	{
		UnavailableFunction,
		SystemFunction,
		MiscFunction,
		CriticalFunction,
		NavigationFunction,
		DisplayFunction,
		PrimaryFunction,
		OfflineFunction,
		Static,
		Gray
	}

	public class LcarsDef
	{
		public string Caption;

		public bool NeedsDoubleTap;
		public bool IsCommandable;
		public Action Command;
		public Action PlayCommandSound;
		public UIColor Color;

		LcarsComponentType _componentType;

		public LcarsComponentType ComponentType {
			get { return _componentType; }
			set {
				_componentType = value;
				Color = Lcars.ComponentColors[_componentType];
			}
		}

		public LcarsDef ()
		{
			Caption = "";
			ComponentType = LcarsComponentType.Gray;
			NeedsDoubleTap = false;
			IsCommandable = true;
			Command = null;
			PlayCommandSound = null;
			Color = Lcars.ComponentColors[ComponentType];
		}
		public void ExecuteCommand ()
		{
			if (Command != null) {
				Command ();
			}
		}
		public void UpdateWith (LcarsDef other)
		{
			if (other == null)
				return;
			Caption = other.Caption;
			ComponentType = other.ComponentType;
			NeedsDoubleTap = other.NeedsDoubleTap;
			IsCommandable = other.IsCommandable;
			Command = other.Command;
		}
	}

	[MonoTouch.Foundation.Register("LcarsComp")]
	public class LcarsComp : UIView
	{
		static readonly TimeSpan ConfirmWait = TimeSpan.FromSeconds (1);

		public LcarsComp (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public LcarsComp (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public LcarsComp () : base()
		{
			Initialize ();
		}

		UIFont _font;
		public UIFont Font {
			get { return _font; }
			set {
				_font = value;
				OnModelChanged ();
			}
		}

		LcarsDef _def;
		public LcarsDef Def {
			get { return _def; }
			set {
				if (value != null) {
					_def = value;
					OnModelChanged ();
				}
			}
		}

		float _width;
		public float Width {
			get { return _width; }
			set {
				_width = value;
				OnModelChanged ();
			}
		}

		float _height;
		public float Height {
			get { return _height; }
			set {
				_height = value;
				OnModelChanged ();
			}
		}

		float _padding;
		public float Padding {
			get { return _padding; }
			set {
				_padding = value;
				OnModelChanged ();
			}
		}


		LcarsShape _shape;
		public LcarsShape Shape {
			get { return _shape; }
			set {
				_shape = value;
				OnModelChanged ();
			}
		}

		public void Initialize ()
		{
			Def = new LcarsDef ();
			Width = Frame.Width;
			Height = Frame.Height;
			Shape = LcarsShape.Left;
			Padding = 1;
			Font = Theme.ComponentFont;
			ContentMode = UIViewContentMode.Redraw;
			BackgroundColor = UIColor.Clear;
		}

		bool CanCommand ()
		{
			return _def.IsCommandable && (_def.Command != null);
		}

		public const float GoldenRatio = 1.618034f;

		enum SelectionState
		{
			NotSelected,
			Pending,
			Selected
		}

		SelectionState _selState = SelectionState.NotSelected;

		bool _touchDown = false;

		public override void TouchesBegan (NSSet touches, UIEvent evt)
		{
			try {
				if (!_touchDown) {
					var touch = touches.ToArray<UITouch> ()[0];
					if (ActiveArea.Contains (touch.LocationInView (this))) {
						_touchDown = true;
						
						if (!CanCommand ()) {
							if (Def != null && Def.ComponentType == LcarsComponentType.Gray) {
								Sounds.PlayNotActive ();
							}
							return;
						}
						
						var prevState = _selState;
						
						if (prevState == LcarsComp.SelectionState.NotSelected) {
							if (_def.NeedsDoubleTap) {
								_selState = LcarsComp.SelectionState.Pending;
								NSTimer.CreateScheduledTimer (ConfirmWait, delegate {
									_selState = LcarsComp.SelectionState.NotSelected;
									OnModelChanged ();
								});
							} else {
								_selState = LcarsComp.SelectionState.Selected;
							}
						} else {
							_selState = LcarsComp.SelectionState.Selected;
						}
						
						if (_selState == LcarsComp.SelectionState.Pending) {
							Sounds.PlayPendingCommand ();
						} else {
							if (prevState == LcarsComp.SelectionState.Pending) {
								Sounds.PlayConfirmPendingCommand ();
							} else {
								PlayConfirmCommand ();
							}
						}
						
						OnModelChanged ();
					}
				}
			} catch (Exception error) {
				Log.Error (error);
			}
			
		}

		void PlayConfirmCommand ()
		{
			if (Def.PlayCommandSound != null) {
				Def.PlayCommandSound ();
			} else {
				Sounds.PlayBeep ();
			}
		}

		public override void TouchesEnded (NSSet touches, UIEvent evt)
		{
			try {
				if (_touchDown) {
					_touchDown = false;
					if (_selState == LcarsComp.SelectionState.Selected) {
						_selState = LcarsComp.SelectionState.NotSelected;
						if (Def.Command != null) {
							Def.Command ();
						}
					}
					SetNeedsDisplay ();
				}
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		public float OuterCorner = 0.5f;
		public float InnerCorner = 2.5f;

		public override void Draw (System.Drawing.RectangleF rect)
		{
			try {
				var c = UIGraphics.GetCurrentContext ();
				
				var color = GetColor ();
				
				color.SetFill ();
				
				var W = Frame.Width;
				var H = Frame.Height;
				var P = Padding;
				
				var w = Width - 2 * Padding;
				var h = Height - 2 * Padding;
				var big = OuterCorner * w;
				var small = InnerCorner * h;
				if (small > 0.75f * big) {
					small = 0.75f * big;
				}
				
				if (Shape == LcarsShape.Left) {
					var r = new RectangleF (P, P, W - 2 * P, H - 2 * P);
					c.FillRect (r);
					DrawCaptionInBox (c, r);
				} else if (Shape == LcarsShape.Button) {
					var d = Frame.Height - 2 * Padding;
					c.FillEllipseInRect (new RectangleF (Padding, Padding, d, d));
					c.FillEllipseInRect (new RectangleF (Frame.Width - Padding - d, Padding, d, d));
					var r = new RectangleF (Padding + d / 2, Padding, Frame.Width - d - 2 * Padding, d);
					c.FillRect (r);
					DrawCaptionInBox (c, r);
				} else if (Shape == LcarsShape.BottomLeft) {
					c.MoveTo (P, P);
					c.AddLineToPoint (P, H - P - big);
					c.AddArc (P + big, H - P - big, big, (float)(Math.PI), (float)(Math.PI / 2), true);
					c.AddLineToPoint (W - P, H - P);
					c.AddLineToPoint (W - P, H - P - h);
					c.AddLineToPoint (P + w + small, H - P - h);
					c.AddArc (P + w + small, H - P - h - small, small, (float)(Math.PI / 2), (float)(Math.PI), false);
					c.AddLineToPoint (P + w, P);
					c.AddLineToPoint (P, P);
					c.FillPath ();
				} else if (Shape == LcarsShape.TopLeft) {
					c.MoveTo (P, H - P);
					c.AddLineToPoint (P, P + big);
					c.AddArc (P + big, P + big, big, (float)(Math.PI), (float)(-Math.PI / 2), false);
					c.AddLineToPoint (W - P, P);
					c.AddLineToPoint (W - P, P + h);
					c.AddLineToPoint (P + w + small, P + h);
					c.AddArc (P + w + small, P + h + small, small, (float)(-Math.PI / 2), (float)(Math.PI), true);
					c.AddLineToPoint (P + w, H - P);
					c.AddLineToPoint (P, H - P);
					c.FillPath ();
					DrawCaptionInBox (c, new RectangleF (P, 0, w, H));
				} else if (Shape == LcarsShape.TopRight) {
					c.MoveTo (W - P, H - P);
					c.AddLineToPoint (W - P, P + big);
					c.AddArc (W - P - big, P + big, big, (float)(Math.PI), (float)(-Math.PI / 2), true);
					c.AddLineToPoint (P, P);
					c.AddLineToPoint (P, P + h);
					c.AddLineToPoint (W - w - small - P, P + h);
					c.AddArc (W - w - small - P, P + h + small, small, (float)(-Math.PI / 2), (float)(0), false);
					c.AddLineToPoint (W - P - w, H - P);
					c.AddLineToPoint (W - P, H - P);
					c.FillPath ();
					DrawCaptionInBox (c, new RectangleF (W - w - P, 0, w, H));
				} else if (Shape == LcarsShape.BottomRight) {
					c.MoveTo (P, H - P);
					c.AddLineToPoint (W - P - big, H - P);
					c.AddArc (W - P - big, H - P - big, big, (float)(3 * Math.PI / 2), (float)(0), true);
					c.AddLineToPoint (W - P, P);
					c.AddLineToPoint (W - P - w, P);
					c.AddLineToPoint (W - P - w, H - P - h - small);
					c.AddArc (W - P - w - small, H - P - h - small, small, (float)(0), (float)(Math.PI / 2), false);
					c.AddLineToPoint (P, H - P - h);
					c.AddLineToPoint (P, H - P);
					c.FillPath ();
					DrawCaptionInBox (c, new RectangleF (W - w - P, 0, w, H));
				}
			} catch (Exception error) {
				Log.Error (error);
			}
			
		}

		RectangleF ActiveArea = new RectangleF (0, 0, 0, 0);

		void DrawCaptionInBox (CGContext c, RectangleF box)
		{
			ActiveArea = box;
			if (Font == null) {
				return;
			}
			UIColor.Black.SetColor ();
			
			var caption = _def.Caption;
			if (_selState == LcarsComp.SelectionState.Pending) {
				caption = "CONFIRM?";
			}
			
			var s = StringSize (caption, Font);
			var pad = Shape == LcarsShape.Button ? 0.0f : 4.0f;
			
			if (App.Inst.IsIPad) {
				box.Width -= 4;
			}
			
			var left = box.Right - s.Width - pad;
			if (left < 2) {
				left = 2;
			}
			
			DrawString (caption, new RectangleF (left, box.Bottom - (s.Height * 1.1f), box.Right - left, s.Height), Font, UILineBreakMode.Clip, UITextAlignment.Right);
		}

		UIColor GetColor ()
		{
			if (_selState == LcarsComp.SelectionState.Selected) {
				return UIColor.White;
			} else if (_selState == LcarsComp.SelectionState.Pending) {
				return UIColor.White;
			} else {
				return Def.Color;
			}
		}

		UIColor GetTextColor ()
		{
			return UIColor.Black;
		}

		void OnModelChanged ()
		{
			SetNeedsDisplay ();
		}
	}
}

