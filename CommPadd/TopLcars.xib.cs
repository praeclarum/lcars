
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;

namespace CommPadd
{
	public partial class TopLcars : UIViewController, IRefreshable, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code


		public TopLcars (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public TopLcars (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public TopLcars () : base("TopLcars", null)
		{
			Initialize ();
		}

		void Initialize ()
		{
		}

		#endregion

		SelectItem _buttons;

		public override void ViewDidLoad ()
		{
			try {
				TitleLabel.Font = Theme.TitleFont;
				
				HomeBtn.Def.ComponentType = LcarsComponentType.Static;
				PlayBtn.Def.ComponentType = LcarsComponentType.Static;
				PlayBtn.Def.Command = ToggleAudio;
				
				RelativeComp.Shape = LcarsShape.BottomLeft;
				
				//View.BackgroundColor = UIColor.DarkGray;
				
				if (App.Inst.IsIPad) {
					RelativeComp.Width = 160;
					RelativeComp.Height = 20;
				} else {
					var w = 60;
					RelativeComp.Width = w;
					RelativeComp.Height = 10;
					SetWidth (PrimaryComp, w);
					SetWidth (HomeBtn, w);
					SetWidth (PlayBtn, w);
				}
				
				RelativeComp.Def.ComponentType = LcarsComponentType.Gray;
				
				MsgTable.BackgroundColor = UIColor.Black;
				
				NSTimer.CreateRepeatingScheduledTimer (TimeSpan.FromSeconds (1), delegate {
					SetAudioActivityColorUI ();
					SetDataActivityColorUI ();
				});
			} catch (Exception error) {
				Log.Error (error);
			}
			
		}

		public void DoLayout ()
		{
			
			var left = HomeBtn.Frame.Right + 2;
			
			var h = View.StringSize (TitleLabel.Text, TitleLabel.Font).Height;
			
			TitleLabel.Lines = 1;
			TitleLabel.MinimumFontSize = Theme.TextFont.PointSize;
			
			TitleLabel.Frame = new RectangleF (left, 0, View.Frame.Width - left, h);
			
			if (_buttons != null) {
				_buttons.Frame = new RectangleF (left, h, View.Frame.Width - left, View.Frame.Height - h - 10);
				_buttons.DoLayout ();
			}
		}

		void SetWidth (LcarsComp c, float w)
		{
			c.Frame = new RectangleF (c.Frame.Left, c.Frame.Top, w, c.Frame.Height);
			c.Width = w;
		}

		void ToggleAudio ()
		{
			var player = AudioPlayerController.Inst;
			
			if (player != null) {
				if (player.IsPlaying) {
					player.Stop ();
				} else {
					player.Play ();
				}
			}
		}

		void SetAudioActivityColorUI ()
		{
			
			var player = AudioPlayerController.Inst;
			
			if (player != null) {
				PlayBtn.Def.ComponentType = LcarsComponentType.SystemFunction;
				PlayBtn.Def.Caption = player.GetStatus (true);
				PlayBtn.Def.IsCommandable = true;
			} else {
				PlayBtn.Def.ComponentType = LcarsComponentType.Static;
				PlayBtn.Def.Caption = "";
				PlayBtn.Def.IsCommandable = false;
			}
			
			PlayBtn.SetNeedsDisplay ();
			
		}

		void SetDataActivityColorUI ()
		{
			var lastUpdateTime = SourceUpdater.LastUpdateTime;
			var now = DateTime.UtcNow;
			
			if ((now - lastUpdateTime) < TimeSpan.FromSeconds (10)) {
				RelativeComp.Def.ComponentType = LcarsComponentType.SystemFunction;
			} else {
				RelativeComp.Def.ComponentType = LcarsComponentType.Gray;
			}
			RelativeComp.SetNeedsDisplay ();
		}

		void RefreshMessages ()
		{
		}


		LcarsDef[] Defs;

		public void RefreshInfo (UIInfo info)
		{
			TitleLabel.Text = info.ScreenTitle.ToUpperInvariant ();
			PrimaryComp.Def = info.TopLeft;
			
			HomeBtn.Def = info.TopMisc;
			
			//
			// Did the buttons actually change?
			//
			var oldNumButtons = Defs == null ? 0 : Defs.Length;
			var numButtons = info.CommandButtons != null ? info.CommandButtons.Length : 0;
			var sameButtons = numButtons == oldNumButtons;
			if (sameButtons) {
				for (var i = 0; i < numButtons && sameButtons; i++) {
					var a = Defs[i];
					var b = info.CommandButtons[i];
					sameButtons = a.Caption == b.Caption;
				}
			}
			
			Defs = info.CommandButtons;
			if (!sameButtons) {
				if (_buttons != null) {
					_buttons.RemoveFromSuperview ();
					_buttons = null;
				}
				
				if (numButtons > 0) {
					var w = 600;
					var h = 160;
					_buttons = new SelectItem (info.CommandButtons, new RectangleF (View.Frame.Width - w, TitleLabel.Frame.Bottom + 10, w, h), 100, 10, 0, ItemOrder.RightToLeft);
					_buttons.BackgroundColor = UIColor.Clear;
					View.AddSubview (_buttons);
					DoLayout ();
				}
			}
			
//			var leftButton = View.Frame.Width;
//			if (numButtons > 0) {
//				var b = buttons[numButtons - 1];
//				leftButton = b.Frame.Left;
//			}
//			
//			MsgTable.Frame = new System.Drawing.RectangleF(MsgTable.Frame.Left,
//			                                               MsgTable.Frame.Top,
//			                                               leftButton - 20 - MsgTable.Frame.Left,
//			                                               MsgTable.Frame.Height);
//			MsgTable.Hidden = MsgTable.Frame.Width < 100;			
		}
	}
}
