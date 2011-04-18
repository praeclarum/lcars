
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;

namespace CommPadd
{
	public partial class MainLcars : UIViewController, IRefreshable, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public MainLcars (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public MainLcars (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public MainLcars () : base("MainLcars", null)
		{
			Initialize ();
		}

		void Initialize ()
		{
		}

		#endregion

		public override void ViewDidLoad ()
		{
			try {
				RelativeComp.Shape = LcarsShape.TopLeft;
				
				var w = Theme.VerticalButtonsWidth;
				
				RelativeComp.Width = w;
				RelativeComp.Height = Theme.HorizontalBarHeight;
				
				SetWidth (Comp1, w);
				SetWidth (Comp2, w);
				SetWidth (Comp6, w);
				SetWidth (Comp7, w);
				SetWidth (MiscComp, w);
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		void SetWidth (LcarsComp c, float w)
		{
			c.Frame = new RectangleF (c.Frame.Left, c.Frame.Top, w, c.Frame.Height);
			c.Width = w;
		}

		public void RefreshInfo (UIInfo info)
		{
			Comp7.Def = info.BottomLeft;
			RelativeComp.Def = info.MainRel;
			Comp1.Def = info.MainTop;
			Comp2.Def = info.MainFill;
			MiscComp.Def = info.MiscBtn;
			Comp6.Def = info.MainSec;
			Comp6.Hidden = true;
			
			var activeTypes = Repo.Foreground.GetActiveSourceTypes ();
			
			foreach (var c in sourceComps) {
				c.RemoveFromSuperview ();
			}
			sourceComps.Clear ();
			if (activeTypes.Length != 0) {
				sourceComps = new List<LcarsComp> ();
				foreach (var t in activeTypes) {
					var tt = t;
					var c = new LcarsComp ();
					
					var cap = SourceTypes.GetTitle (t);
					
					if (!App.Inst.IsIPad && cap.Length > 8) {
						
						var parts = cap.Split (' ');
						for (var j = 0; j < parts.Length; j++) {
							parts[j] = parts[j].TruncateChars (3);
						}
						
						cap = string.Join (" ", parts);
					}
					
					c.Def.Caption = cap;
					c.Def.ComponentType = LcarsComponentType.NavigationFunction;
					c.Def.Command = delegate { App.Inst.ShowSourceTypeMessages (tt, true); };
					View.AddSubview (c);
					sourceComps.Add (c);
				}
			}
			DoLayout ();
		}

		public void DoLayout ()
		{
			
			
			var bot = Comp7;
			
			if (sourceComps.Count == 0) {
				Comp2.Frame = new RectangleF (Comp2.Frame.Left, Comp2.Frame.Top, Comp2.Frame.Width, bot.Frame.Top - Comp2.Frame.Top);
				Comp2.Hidden = false;
			} else {
				var y = 0.0f;
				if (App.Inst.IsIPad) {
					Comp2.Frame = new RectangleF (Comp2.Frame.Left, Comp2.Frame.Top, Comp2.Frame.Width, 44);
					
					y = Comp2.Frame.Bottom;
				} else {
					Comp2.Hidden = true;
					y = MiscComp.Frame.Bottom;
				}
				var w = MiscComp.Frame.Width;
				var h = (bot.Frame.Top - y) / sourceComps.Count;
				foreach (var c in sourceComps) {
					c.Frame = new RectangleF (0, y, w, h);
					y += h;
				}
			}
			
		}

		List<LcarsComp> sourceComps = new List<LcarsComp> ();
		
	}
}
