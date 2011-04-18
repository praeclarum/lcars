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
using MonoTouch.UIKit;
using System.Drawing;
using System.Reflection;
using System.Linq;
using MonoTouch.AddressBook;
using MonoTouch.Foundation;

namespace CommPadd
{
	public class IgnoreAttribute : Attribute
	{
	}
	public class OptionalAttribute : Attribute
	{
	}
	public class ChooseEmailInputAttribute : Attribute
	{
	}
	public class EmailInputAttribute : Attribute
	{
	}
	public class SectionAttribute : Attribute
	{
	}
	public class CustomUIAttribute : Attribute
	{
		public Type UIType { get; private set; }
		public CustomUIAttribute (Type uiType)
		{
			UIType = uiType;
		}
	}

	public interface IHelpful
	{
		string HelpForProperty (string propName);
	}


	class Form : UIScrollView, ILayoutable
	{
		object Source;
		Type SourceType;
		LcarsComp ConfirmButton, CancelButton;

		UITextView GeneralHelp;

		Row[] _propViews = new Row[0];

		public event Action OnOK;
		public event Action OnCancel;

		public bool ShowCancelButton {
			get { return !CancelButton.Hidden; }
			set { CancelButton.Hidden = !value; }
		}

		public string ConfirmButtonText {
			get { return ConfirmButton.Def.Caption; }
			set {
				ConfirmButton.Def.Caption = value;
				ConfirmButton.SetNeedsDisplay ();
			}
		}

		bool IsCustomUI;
		Type CustomUIType;
		ICustomUI CustomUI;

		public Form (object source, RectangleF frame) : base(frame)
		{
			try {
				Source = source;
				SourceType = source.GetType ();
				
				Initialize ();
				UpdateUI ();
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		class Row
		{
			public PropertyInfo Property { get; set; }
			public UILabel Label { get; set; }
			public UITextField Text { get; set; }
		}

		void Initialize ()
		{
			
			AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
			DirectionalLockEnabled = true;
			
			var helpText = "";
			
			var vs = SourceType.GetCustomAttributes (typeof(CustomUIAttribute), true);
			IsCustomUI = vs.Length > 0;
			
			if (IsCustomUI) {
				CustomUIType = ((CustomUIAttribute)vs[0]).UIType;
				CustomUI = Activator.CreateInstance (CustomUIType) as ICustomUI;
				CustomUI.SetModel (Source);
				CustomUI.OnOK += delegate {
					try {
						if (OnOK != null) {
							OnOK ();
						}
					} catch (Exception error) {
						Log.Error (error);
					}
				};
				AddSubview (CustomUI as UIView);
				this.ExclusiveTouch = false;
				this.CanCancelContentTouches = false;
				this.DelaysContentTouches = false;
				
			} else {
				AlwaysBounceVertical = true;
				AlwaysBounceHorizontal = false;
				helpText = InitializePropsUI ();
			}
			
			ConfirmButton = new LcarsComp { Shape = LcarsShape.Button };
			ConfirmButton.Def.Caption = "ADD";
			ConfirmButton.Def.NeedsDoubleTap = false;
			ConfirmButton.Def.IsCommandable = false;
			ConfirmButton.Def.Command = delegate {
				HideKeyBoard (this);
				if (OnOK != null) {
					OnOK ();
				}
			};
			
			CancelButton = new LcarsComp { Shape = LcarsShape.Button, Hidden = true };
			CancelButton.Def.Caption = "CANCEL";
			CancelButton.Def.NeedsDoubleTap = false;
			CancelButton.Def.IsCommandable = true;
			CancelButton.Def.Command = delegate {
				HideKeyBoard (this);
				if (OnCancel != null) {
					OnCancel ();
				}
			};
			
			if (IsCustomUI) {
				ConfirmButton.Hidden = true;
				CancelButton.Hidden = true;
			}
			
			GeneralHelp = new UITextView ();
			GeneralHelp.BackgroundColor = UIColor.Black;
			GeneralHelp.TextColor = Theme.TitleColor;
			GeneralHelp.Font = Theme.TextFont;
			GeneralHelp.Editable = false;
			GeneralHelp.Text = helpText;
			
			AddSubview (CancelButton);
			AddSubview (ConfirmButton);
			AddSubview (GeneralHelp);
		}

		string InitializePropsUI ()
		{
			var help = Source as IHelpful;
			var helpText = "";
			
			
			var q = from t in SourceType.GetProperties ()
				where t.DeclaringType == SourceType && t.CanWrite
				select t;
			var props = q.ToArray ();
			
			var rows = new List<Row> ();
			
			
			foreach (var p in props) {
				
				var ignoreAttrs = p.GetCustomAttributes (typeof(IgnoreAttribute), true);
				if (ignoreAttrs.Length > 0)
					continue;
				
				var isEmail = p.GetCustomAttributes (typeof(EmailInputAttribute), true).Length > 0;
				if (!isEmail && p.Name == "Email") {
					isEmail = true;
				}
				var isChooseEmail = p.GetCustomAttributes (typeof(ChooseEmailInputAttribute), true).Length > 0;
				
				var title = Theme.GetTitle (p.Name);
				
				if (help != null) {
					var h = help.HelpForProperty (p.Name);
					if (h != "") {
						helpText += title + " " + h + "\n";
					}
				}
				
				var label = new UILabel { BackgroundColor = UIColor.Black, TextColor = Lcars.ComponentColors[LcarsComponentType.CriticalFunction], Text = title, TextAlignment = UITextAlignment.Right, BaselineAdjustment = UIBaselineAdjustment.AlignBaselines, Font = Theme.InputFont, AdjustsFontSizeToFitWidth = true };
				
				var row = new Row { Property = p };
				rows.Add (row);
				
				row.Label = label;
				UIKeyboardType kbd = UIKeyboardType.Default;
				if (isEmail || isChooseEmail) {
					kbd = UIKeyboardType.EmailAddress;
				} else if (p.Name == "Url") {
					kbd = UIKeyboardType.Url;
				} else if (p.PropertyType == typeof(int)) {
					kbd = UIKeyboardType.NumberPad;
				}
				
				var init = p.GetValue (Source, null);
				
				var text = new UITextField { Placeholder = title, BackgroundColor = UIColor.Black, TextColor = UIColor.White, Font = Theme.InputFont, AdjustsFontSizeToFitWidth = false, AutocapitalizationType = UITextAutocapitalizationType.None, KeyboardType = kbd, Text = init != null ? init.ToString () : "" };
				row.Text = text;
				if (p.Name.ToLowerInvariant ().IndexOf ("password") >= 0) {
					text.SecureTextEntry = true;
					text.AutocorrectionType = UITextAutocorrectionType.No;
				}
				if (p.Name != "Search") {
					text.AutocorrectionType = UITextAutocorrectionType.No;
				}
				if (text.Text.Length == 0 && !isChooseEmail) {
					text.BecomeFirstResponder ();
				}
				label.Hidden = text.Text.Length == 0;
				if (isChooseEmail) {
					text.EditingDidBegin += delegate {
						try {
							
							bool hasPeople = false;
							using (var adds = new ABAddressBook ()) {
								foreach (var pe in adds.GetPeople ()) {
									var es = pe.GetEmails ();
									if (es.Count > 0) {
										hasPeople = true;
										break;
									}
								}
							}
							
							if (hasPeople) {
								Sounds.PlayBeep ();
								var em = new ChooseEmail ();
								em.EmailSelected += emailAddress =>
								{
									text.Text = emailAddress;
									UpdateUI ();
								};
								App.Inst.ShowDialog (em);
								NSTimer.CreateScheduledTimer (TimeSpan.FromMilliseconds (30), delegate {
									try {
										HideKeyBoard (this);
									} catch (Exception err) {
										Log.Error (err);
									}
								});
							}
						} catch (Exception error) {
							Log.Error (error);
						}
					};
				}
				text.AllEditingEvents += delegate {
					try {
						label.Hidden = string.IsNullOrEmpty (text.Text);
						UpdateUI ();
					} catch (Exception error) {
						Log.Error (error);
					}
				};
				AddSubview (label);
				AddSubview (text);
			}
			
			
			_propViews = rows.ToArray ();
			
			return helpText;
		}

		public void DoLayout ()
		{
			var labelWidth = Frame.Width / 3;
			
			var rowHeight = StringSize ("HELLO", Theme.InputFont).Height * 1.2f;
			var off = 0.138f * rowHeight;
			
			var btnHeight = rowHeight;
			
			var w = Frame.Width;
			var h = Frame.Height;
			
			var y = 0.0f;
			
			if (IsCustomUI && CustomUI != null) {
				
//				var sz = CustomUI.DefaultSize;
//				
//				var a = sz.Width / sz.Height;
//				var x = 0.0f;
//				var ww = sz.Width;
//				var hh = sz.Height;
//				
//				if (ww > w) {
//					ww = w;
//					hh = ww / a;
//				}
//				
//				if (hh > h) {
//					hh = h;
//					ww = hh*a;
//				}
//				
//				if (ww < w) {
//					x = (w - ww)/2;
//				}
//				
//				y += hh;
				
				((UIView)CustomUI).Frame = new RectangleF (0, 0, w, h);
				y += Frame.Height;
				//new RectangleF(x, 0, ww, hh);
			} else {
				
				foreach (var r in _propViews) {
					r.Label.Frame = new RectangleF (0, y, labelWidth - 10, rowHeight - off);
					r.Text.Frame = new RectangleF (labelWidth + 10, y, Frame.Width - labelWidth - 12, rowHeight);
					y += rowHeight;
				}
			}
			
			y += rowHeight;
			
			ConfirmButton.Frame = new RectangleF (Frame.Width - labelWidth, y, labelWidth, btnHeight);
			CancelButton.Frame = new RectangleF (Frame.Width - labelWidth * 2, y, labelWidth, btnHeight);
			
			var right = CancelButton.Frame.Left;
			if (CancelButton.Hidden) {
				right = ConfirmButton.Frame.Left;
			}
			
			var gtH = 2.0f * StringSize (GeneralHelp.Text, GeneralHelp.Font, new SizeF (right, 10000)).Height;
			
			GeneralHelp.Frame = new RectangleF (0, y, right, gtH);
			
			if (IsCustomUI) {
				ContentSize = new SizeF (Frame.Width - 20, y + gtH);
			} else {
				ContentSize = new SizeF (Frame.Width - 20, y + gtH + Frame.Height / 2);
			}
		}

		void UpdateUI ()
		{
			ContentSize = new SizeF (Frame.Width - 20, ContentSize.Height);
			
			var valid = true;
			var partValid = false;
			foreach (var p in _propViews) {
				try {
					if (p.Text.Text.Trim ().Length == 0) {
						var isOptional = p.Property.GetCustomAttributes (typeof(OptionalAttribute), true).Length > 0;
						if (!isOptional) {
							throw new Exception ();
						}
					}
					p.Property.SetValue (Source, Convert.ChangeType (p.Text.Text.Trim (), p.Property.PropertyType), null);
					partValid = true;
				} catch (Exception) {
					valid = false;
				}
			}
			
			if (valid) {
				ConfirmButton.Def.ComponentType = LcarsComponentType.SystemFunction;
				ConfirmButton.Def.IsCommandable = true;
				CancelButton.Def.NeedsDoubleTap = false;
				CancelButton.Def.ComponentType = LcarsComponentType.NavigationFunction;
			} else {
				ConfirmButton.Def.ComponentType = LcarsComponentType.Gray;
				ConfirmButton.Def.IsCommandable = false;
				CancelButton.Def.NeedsDoubleTap = false;
				if (partValid) {
					CancelButton.Def.ComponentType = LcarsComponentType.NavigationFunction;
				} else {
					CancelButton.Def.ComponentType = LcarsComponentType.Static;
				}
			}
			ConfirmButton.SetNeedsDisplay ();
			CancelButton.SetNeedsDisplay ();
		}

		static void HideKeyBoard (UIView v)
		{
			try {
				v.ResignFirstResponder ();
				foreach (var s in v.Subviews) {
					HideKeyBoard (s);
				}
			} catch (Exception error) {
				Log.Error (error);
			}
		}
		
	}

	public enum ItemOrder
	{
		LeftToRight,
		RightToLeft,
		TopToBottom
	}

	public class SelectItem : UIScrollView, ILayoutable
	{

		LcarsDef[] Defs;
		Dictionary<LcarsDef, LcarsComp> Comps = new Dictionary<LcarsDef, LcarsComp> ();
		UIView _buttons;

		readonly float MinWidth;
		readonly float ButtonHeight;
		readonly float ButtonVerticalGap;
		readonly float ButtonHorizontalGap;

		float _btnWidth;

		readonly ItemOrder Align;

		public SelectItem (IEnumerable<LcarsDef> defs, RectangleF frame) : this(defs, frame, 0, 0, 0, ItemOrder.LeftToRight)
		{
		}

		public SelectItem (IEnumerable<LcarsDef> defs, RectangleF frame, float minWidth, float hgap, float vgap, ItemOrder align) : base(frame)
		{
			Defs = defs.ToArray ();
			Array.Sort (Defs, (a, b) => a.Caption.CompareTo (b.Caption));
			
			Align = align;
			
			if (minWidth > 1) {
				MinWidth = minWidth;
			} else {
				if (App.Inst.IsIPad) {
					MinWidth = 160;
				} else {
					MinWidth = 60;
				}
			}
			
			if (App.Inst.IsIPad) {
				ButtonHeight = 44;
			} else {
				ButtonHeight = 24;
			}
			
			if (vgap > 1) {
				ButtonVerticalGap = vgap;
			} else {
				ButtonVerticalGap = 0.3f * ButtonHeight;
			}
			
			if (hgap > 1) {
				ButtonHorizontalGap = hgap;
			} else {
				ButtonHorizontalGap = 20;
			}
			
			Initialize ();
		}

		static UIColor Rgb (int r, int g, int b)
		{
			return UIColor.FromRGB (r / 255.0f, g / 255.0f, b / 255.0f);
		}

		void Initialize ()
		{
			
			AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
			
			UIColor[] comps = new UIColor[] { Rgb (254, 233, 66), Rgb (208, 151, 254), Rgb (220, 183, 250), Rgb (251, 228, 117), Rgb (251, 218, 69), Rgb (226, 191, 255), Rgb (254, 191, 108), Rgb (255, 198, 120), Rgb (204, 141, 255), Rgb (177, 113, 252),
			Rgb (253, 219, 58), Rgb (230, 189, 255), Rgb (253, 228, 105), Rgb (145, 72, 255), Rgb (171, 115, 254), Rgb (252, 219, 62), Rgb (255, 206, 144), Rgb (211, 182, 249), Rgb (231, 201, 255), Rgb (148, 97, 250),
			Rgb (249, 228, 122), Rgb (226, 191, 254), Rgb (255, 207, 144), Rgb (204, 141, 255), Rgb (254, 230, 34) };
			
			var w = MinWidth;
			
			foreach (var def in Defs) {
				var sz = StringSize (def.Caption, Theme.ComponentFont);
				var dw = sz.Width + ButtonHeight;
				if (dw > w) {
					w = dw;
				}
			}
			
			_btnWidth = w;
			
			int numCols = (int)(Frame.Width / _btnWidth + 0.5f);
			var numRows = (Defs.Length / numCols) + 1;
			
			_buttons = new UIView (new RectangleF (0, 0, Frame.Width, numRows * 57));
			
			ContentSize = _buttons.Frame.Size;
			AddSubview (_buttons);
			
			// Create buttons
			foreach (var def in Defs) {
				
				var btn = new LcarsComp { Shape = LcarsShape.Button };
				
				btn.Def = def;
				//btn.Def.ComponentType = LcarsComponentType.NavigationFunction;
				btn.Def.Color = comps[(uint)(def.Caption.GetHashCode ()) % comps.Length];
				
				_buttons.AddSubview (btn);
				Comps.Add (def, btn);
			}
		}

		public void DoLayout ()
		{
			LayoutSubviews ();
		}

		float _margin = 0;

		public override void LayoutSubviews ()
		{
			try {
				int numCols = (int)(Frame.Width / (_btnWidth + ButtonHorizontalGap));
				var numRows = ((Defs.Length + numCols - 1) / numCols);
				
				_margin = (Frame.Width - numCols * _btnWidth - (numCols - 1) * ButtonHorizontalGap) / 2;
				
				_buttons.Frame = new RectangleF (0, 0, Frame.Width, numRows * (ButtonHeight + ButtonVerticalGap));
				ContentSize = new SizeF (_buttons.Frame.Width, _buttons.Frame.Height + ButtonHeight);
				
				var w = _btnWidth;
				var h = ButtonHeight;
				var x = InitialX;
				var y = 0.0f;
				
				var i = 0;
				foreach (var def in Defs) {
					
					var btn = Comps[def];
					
					btn.Frame = new System.Drawing.RectangleF (x, y, w, h);
					
					x += DX;
					if (((Align == ItemOrder.LeftToRight) && (x + DX > Frame.Width)) || ((Align == ItemOrder.RightToLeft) && (x < 0))) {
						x = InitialX;
						y += h + ButtonVerticalGap;
					}
					
					i++;
				}
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		float InitialX {
			get { return (Align == ItemOrder.LeftToRight) ? _margin : Frame.Width - _btnWidth; }
		}
		float DX {
			get {
				var d = ButtonHorizontalGap + _btnWidth;
				return (Align == ItemOrder.LeftToRight) ? d : -d;
			}
		}
	}
}
