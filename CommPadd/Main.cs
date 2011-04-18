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
using System.Drawing;
using Data;
using System.Threading;
using MonoTouch.MediaPlayer;
using MonoTouch.AVFoundation;

namespace CommPadd
{
	public interface IRefreshable
	{
		void RefreshInfo (UIInfo info);
	}

	public interface ILayoutable
	{
		void DoLayout ();
	}

	public interface IHasInfo
	{
		UIInfo Info { get; }
	}

	public class UIInfo
	{
		public const string DefaultScreenTitle = "READER";

		public string ScreenTitle = DefaultScreenTitle;

		public LcarsDef BottomLeft;
		public LcarsDef TopLeft;
		public LcarsDef TopMisc;
		public LcarsDef MainRel;
		public LcarsDef MainTop;
		public LcarsDef MainFill;
		public LcarsDef MiscBtn;
		public LcarsDef MainSec;

		public LcarsDef[] CommandButtons;

		public void UpdateWith (UIInfo other)
		{
			ScreenTitle = other.ScreenTitle;
			
			Up (other, i => i.BottomLeft, d => { this.BottomLeft = d; });
			Up (other, i => i.MainRel, d => { this.MainRel = d; });
			Up (other, i => i.MainTop, d => { this.MainTop = d; });
			Up (other, i => i.MainFill, d => { this.MainFill = d; });
			Up (other, i => i.TopLeft, d => { this.TopLeft = d; });
			Up (other, i => i.TopMisc, d => { this.TopMisc = d; });
			Up (other, i => i.MiscBtn, d => { this.MiscBtn = d; });
			Up (other, i => i.MainSec, d => { this.MainSec = d; });
			
			if (other.CommandButtons != null) {
				CommandButtons = other.CommandButtons;
			}
		}

		void Up (UIInfo other, Func<UIInfo, LcarsDef> g, Action<LcarsDef> s)
		{
			var mine = g (this);
			if (mine == null) {
				s (g (other));
			} else {
				mine.UpdateWith (g (other));
			}
		}
	}

	public class MainViewController : UIViewController
	{
		public TopLcars Top;
		public MainLcars Main;

		public UIViewController MainView;
		public UIViewController SubView;

		List<UIViewController> _dialogs = new List<UIViewController> ();

		bool IsSubViewMaximized = false;
		bool IsMainMaximized = false;

		public bool IsDialogActive {
			get { return _dialogs.Count > 0; }
		}

		public void BuildUI ()
		{
			Top = new TopLcars ();
			Top.View.Frame = new System.Drawing.RectangleF (0, 0, 768, 264);
			Main = new MainLcars ();
			Main.View.Frame = new System.Drawing.RectangleF (0, 264, 768, 760);
			Main.View.AutoresizingMask = UIViewAutoresizing.FlexibleRightMargin;
			
			View.AddSubview (Top.View);
			View.AddSubview (Main.View);
			View.BackgroundColor = UIColor.Clear;
			
			Main.View.BackgroundColor = UIColor.Clear;
		}

		public bool ToggleMainFullScreen (bool animated)
		{
			IsMainMaximized = !IsMainMaximized;
			Layout (animated);
			return IsMainMaximized;
		}

		public bool ToggleSubViewFullScreen (bool animated)
		{
			if (SubView == null)
				return false;
			
			IsSubViewMaximized = !IsSubViewMaximized;
			
			Layout (animated);
			
			return IsSubViewMaximized;
		}

		bool IsKeyboardActive = false;

		public void OnShowKeyboard (NSNotification notification)
		{
			IsKeyboardActive = true;
			Layout (true);
		}

		public void OnHideKeyboard (NSNotification notification)
		{
			IsKeyboardActive = false;
			Layout (true);
		}

		UIInterfaceOrientation _orient = UIInterfaceOrientation.Portrait;

		public override void WillAnimateRotation (UIInterfaceOrientation toInterfaceOrientation, double duration)
		{
			try {
				base.WillAnimateRotation (toInterfaceOrientation, duration);
				_orient = toInterfaceOrientation;
				Layout (false);
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		public override void WillRotate (UIInterfaceOrientation toInterfaceOrientation, double duration)
		{
			try {
				base.WillRotate (toInterfaceOrientation, duration);
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		public override void DidRotate (UIInterfaceOrientation fromInterfaceOrientation)
		{
			try {
				if (MainView is SourceTypeMessages) {
					((SourceTypeMessages)MainView).RefreshMessagesUI (true);
				}
				base.DidRotate (fromInterfaceOrientation);
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		public void Layout (bool animated)
		{
			if (_orient == UIInterfaceOrientation.Portrait || _orient == UIInterfaceOrientation.PortraitUpsideDown) {
				if (App.Inst.IsIPad) {
					LayoutIPadPortrait (animated);
				} else {
					LayoutIPhonePortrait (animated);
				}
			} else {
				if (App.Inst.IsIPad) {
					LayoutIPadLandscape (animated);
				} else {
					LayoutIPhoneLandscape (animated);
				}
			}
		}

		RectangleF _dialogFrame;

		void LayoutDialogs ()
		{
			if (MainView != null && _dialogs.Count > 0) {
				foreach (var d in _dialogs) {
					d.View.Frame = _dialogFrame;
					Relayout (d);
				}
			}
		}

		void Relayout ()
		{
			Relayout (Top);
			Relayout (Main);
			Relayout (MainView);
			Relayout (SubView);
			foreach (var d in _dialogs) {
				Relayout (d);
			}
		}

		void LayoutIPadPortrait (bool animated)
		{
			if (animated)
				UIView.BeginAnimations ("Layout");
			
			var yoff = 20;
			if (_orient == UIInterfaceOrientation.PortraitUpsideDown) {
				yoff = 0;
			}
			
			this.View.Frame = new RectangleF (0.0f, yoff, 768.0f, 1004.0f);
			Top.View.Hidden = false;
			if (Top != null) {
				if (IsKeyboardActive) {
					Top.View.Frame = new RectangleF (0.0f, -264.0f, 768.0f, 264.0f);
				} else {
					Top.View.Frame = new RectangleF (0.0f, 0.0f, 768.0f, 264.0f);
				}
			}
			if (Main != null) {
				if (IsKeyboardActive) {
					Main.View.Frame = new RectangleF (0.0f, 0.0f, 768.0f, 740.0f);
				} else {
					Main.View.Frame = new RectangleF (0.0f, 264.0f, 768.0f, 740.0f);
				}
			}
			
			Main.View.BackgroundColor = UIColor.Clear;
			
			if (MainView != null) {
				MainView.View.BackgroundColor = UIColor.Clear;
				if (SubView != null) {
					if (IsKeyboardActive) {
						MainView.View.Frame = new RectangleF (168.0f, 300.0f, 590.0f, 200.0f);
					} else {
						MainView.View.Frame = new RectangleF (168.0f, 300.0f, 590.0f, 200.0f);
					}
				} else {
					if (IsKeyboardActive) {
						MainView.View.Frame = new RectangleF (168.0f, 300.0f - 264, 590.0f, Main.View.Frame.Height - 50);
					} else {
						MainView.View.Frame = new RectangleF (168.0f, 300.0f, 590.0f, Main.View.Frame.Height - 50);
					}
				}
			}
			if (SubView != null) {
				if (IsSubViewMaximized) {
					SubView.View.Frame = new RectangleF (0, 0, 768.0f, 1004.0f);
				} else {
					SubView.View.Frame = new RectangleF (168.0f, 504.0f, 600.0f, 500.0f);
				}
			}
			
			if (IsKeyboardActive) {
				_dialogFrame = new RectangleF (168.0f, 300.0f - 264, 590.0f, Main.View.Frame.Height - 50);
			} else {
				_dialogFrame = new RectangleF (168.0f, 300.0f, 590.0f, Main.View.Frame.Height - 50);
			}
			
			LayoutDialogs ();
			
			Relayout ();
			
			if (animated)
				UIView.CommitAnimations ();
		}

		static void Relayout (UIViewController vc)
		{
			if (vc == null)
				return;
			var l = vc as ILayoutable;
			if (l != null) {
				l.DoLayout ();
			}
		}

		void LayoutIPhonePortrait (bool animated)
		{
			if (animated)
				UIView.BeginAnimations ("Layout");
			
			//IsMainMaximized = true;
			
			var yoff = 20;
			if (_orient == UIInterfaceOrientation.PortraitUpsideDown) {
				yoff = 0;
			}
			
			var scrWidth = 320;
			var scrHeight = 480;
			var topHeight = 132;
			var leftMargin = 62;
			
			var max = IsMainMaximized || IsKeyboardActive;
			
			this.View.Frame = new RectangleF (0.0f, yoff, scrWidth, scrHeight - 20);
			Top.View.Hidden = false;
			if (Top != null) {
				if (max) {
					Top.View.Frame = new RectangleF (0.0f, -topHeight, scrWidth, topHeight);
				} else {
					Top.View.Frame = new RectangleF (0.0f, 0.0f, scrWidth, topHeight);
				}
			}
			if (Main != null) {
				if (max) {
					Main.View.Frame = new RectangleF (0.0f, 0.0f, scrWidth, scrHeight - 20);
				} else {
					Main.View.Frame = new RectangleF (0.0f, topHeight, scrWidth, scrHeight - 20 - topHeight);
				}
			}
			
			Main.View.BackgroundColor = UIColor.Clear;
			
			var subViewHeight = (SubView != null && !IsSubViewMaximized) ? 200 : 0;
			
			if (MainView != null) {
				
				MainView.View.BackgroundColor = UIColor.Clear;
				if (max) {
					MainView.View.Frame = new RectangleF (leftMargin, 0, scrWidth - leftMargin - 2, scrHeight - 20 - 2 - subViewHeight);
				} else {
					MainView.View.Frame = new RectangleF (leftMargin, topHeight + 0, scrWidth - leftMargin - 2, scrHeight - topHeight - 20 - 2 - subViewHeight);
				}
			}
			
			if (SubView != null) {
				if (IsSubViewMaximized || MainView == null) {
					SubView.View.Frame = new RectangleF (0.0f, 0.0f, scrWidth, scrHeight - 20);
				} else {
					var top = MainView.View.Frame.Bottom;
					var left = MainView.View.Frame.Left;
					SubView.View.Frame = new RectangleF (left, top, scrWidth - left, scrHeight - top - 20);
				}
			}
			
			if (MainView != null) {
			}
			
			_dialogFrame = new RectangleF (0.0f, 0.0f, scrWidth, scrHeight - 20);
			
			LayoutDialogs ();
			
			Relayout ();
			
			if (animated)
				UIView.CommitAnimations ();
		}

		void LayoutIPhoneLandscape (bool animated)
		{
			if (animated)
				UIView.BeginAnimations ("Layout");
			
			var scrWidth = 320;
			var scrHeight = 480;
			var leftMargin = 62;
			
			//Console.WriteLine (	InterfaceOrientation );
			var xoff = 0.0f;
			var yoff = 0.0f;
			var h = scrWidth - 20.0f;
			var w = scrHeight;
			if (InterfaceOrientation == UIInterfaceOrientation.LandscapeLeft) {
				yoff = 0;
				xoff = 20;
			} else {
				yoff = 0;
				xoff = 0;
			}
			
			this.View.Frame = new RectangleF (xoff, yoff, h, w);
			
			Top.View.Hidden = true;
			Top.View.Frame = new RectangleF (0.0f, 0, scrHeight, scrWidth - 20);
			
			var maxFrame = new RectangleF (0.0f, 0, scrHeight, scrWidth - 20);
			
			Main.View.Frame = maxFrame;
			
			var inFrame = new RectangleF (leftMargin, 10.0f, w - leftMargin, Main.View.Frame.Height - 12);
			
			if (MainView != null) {
				MainView.View.BackgroundColor = UIColor.Clear;
				MainView.View.Frame = inFrame;
			}
			if (SubView != null) {
				SubView.View.BackgroundColor = UIColor.Black;
				SubView.View.Frame = maxFrame;
			}
			
			_dialogFrame = maxFrame;
			LayoutDialogs ();
			
			Relayout ();
			
			if (animated)
				UIView.CommitAnimations ();
		}

		void LayoutIPadLandscape (bool animated)
		{
			if (animated)
				UIView.BeginAnimations ("Layout");
			
			//Console.WriteLine (	InterfaceOrientation );
			var xoff = 0.0f;
			var yoff = 0.0f;
			var h = 748.0f;
			var w = 1024.0f;
			if (InterfaceOrientation == UIInterfaceOrientation.LandscapeLeft) {
				yoff = 0;
				xoff = 20;
			} else {
				yoff = 0;
				xoff = 0;
			}
			
			this.View.Frame = new RectangleF (xoff, yoff, h, w);
			
			Top.View.Hidden = true;
			Top.View.Frame = new RectangleF (0.0f, 0, 1024.0f, 748.0f);
			
			Main.View.Frame = new RectangleF (0.0f, 0, 1024.0f, 748.0f);
			
			w = w - 180;
			h = 200.0f;
			if (MainView != null) {
				MainView.View.BackgroundColor = UIColor.Clear;
				if (SubView != null) {
					if (IsKeyboardActive) {
						MainView.View.Frame = new RectangleF (168, 40.0f, w, h);
					} else {
						MainView.View.Frame = new RectangleF (168, 40.0f, w, h);
					}
				} else {
					if (IsKeyboardActive) {
						MainView.View.Frame = new RectangleF (168.0f, 40.0f, w, Main.View.Frame.Height - 50);
					} else {
						MainView.View.Frame = new RectangleF (168.0f, 40.0f, w, Main.View.Frame.Height - 50);
					}
				}
			}
			if (SubView != null) {
				SubView.View.BackgroundColor = UIColor.Black;
				if (IsSubViewMaximized) {
					SubView.View.Frame = new RectangleF (0.0f, 0.0f, 1024, 748);
				} else {
					SubView.View.Frame = new RectangleF (168.0f, 240.0f, w + 10, 500.0f);
				}
			}
			
			if (IsKeyboardActive) {
				_dialogFrame = new RectangleF (168.0f, 40.0f, w, Main.View.Frame.Height - 50);
			} else {
				_dialogFrame = new RectangleF (168.0f, 40.0f, w, Main.View.Frame.Height - 50);
			}
			LayoutDialogs ();
			
			Relayout ();
			
			if (animated)
				UIView.CommitAnimations ();
		}

		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			return true;
		}

		public override void ViewDidLoad ()
		{
		}

		public override void DismissMoviePlayerViewController ()
		{
			try {
				base.DismissMoviePlayerViewController ();
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		public void ShowDialog (UIViewController vc)
		{
			if (MainView != null)
				MainView.View.Hidden = true;
			if (SubView != null)
				SubView.View.Hidden = true;
			foreach (var d in _dialogs) {
				d.View.Hidden = true;
			}
			_dialogs.Add (vc);
			
			if (MainView != null) {
				vc.View.Frame = MainView.View.Frame;
			}
			View.AddSubview (vc.View);
			View.BringSubviewToFront (vc.View);
			Layout (true);
		}

		public void PopDialog ()
		{
			if (_dialogs.Count == 0)
				return;
			var vc = _dialogs[_dialogs.Count - 1];
			_dialogs.RemoveAt (_dialogs.Count - 1);
			vc.View.RemoveFromSuperview ();
			
			if (_dialogs.Count > 0) {
				var d = _dialogs[_dialogs.Count - 1];
				d.View.Hidden = false;
			} else {
				if (MainView != null)
					MainView.View.Hidden = false;
				if (SubView != null)
					SubView.View.Hidden = false;
			}
		}
	}

	public partial class AppDelegateIPhone : UIApplicationDelegate
	{
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			try {
				App.Inst = new App ();
				App.Inst.IsIPad = false;
				
				return App.Inst.FinishedLaunching (window, app, options);
			} catch (Exception error) {
				Log.Error (error);
				return true;
			}
		}

		public override void WillTerminate (UIApplication application)
		{
			try {
				App.Inst.WillTerminate (application);
			} catch (Exception error) {
				Log.Error (error);
			}
		}
	}

	public partial class AppDelegate : UIApplicationDelegate
	{
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			try {
				App.Inst = new App ();
				App.Inst.IsIPad = true;
				
				return App.Inst.FinishedLaunching (window, app, options);
			} catch (Exception error) {
				Log.Error (error);
				return true;
			}
		}

		public override void WillTerminate (UIApplication application)
		{
			try {
				App.Inst.WillTerminate (application);
			} catch (Exception error) {
				Log.Error (error);
			}
		}
	}

	public class App : NSObject
	{
		MainViewController MVC;
		NSTimer _netStartTimer;

		public static App Inst;

		public bool IsIPad { get; set; }

		public bool IsSimulator { get; private set; }

		public void WillTerminate (UIApplication application)
		{
			AudioPlayerController.Kill ();
		}

		public float LabelGap {
			get { return IsIPad ? 40 : 10; }
		}

		public bool FinishedLaunching (UIWindow window, UIApplication app, NSDictionary options)
		{
			IsSimulator = UIDevice.CurrentDevice.Model.IndexOf ("Simulator") >= 0;
			
			Inst = this;
			
			NSError err;
			
			AVAudioSession.SharedInstance ().SetCategory (AVAudioSession.CategoryAmbient, out err);
			
			Sounds.PlayStartUp ();
			
			Theme.Init ();
			
			Repo.CreateForeground ();
			
			MVC = new MainViewController ();
			MVC.View.BackgroundColor = UIColor.Black;
			MVC.View.Frame = new RectangleF (0, 0, 768, 1024 - 20);
			
			MVC.BuildUI ();
			
			window.AddSubview (MVC.View);
			
			NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, MVC.OnShowKeyboard);
			NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, MVC.OnHideKeyboard);
			
			MVC.Layout (false);
			
			RestoreUIState ();
			
			RefreshInfo ();
			
			_netStartTimer = NSTimer.CreateScheduledTimer (TimeSpan.FromSeconds (5), delegate {
				try {
					CheckNetwork ();
//					Console.WriteLine ("MAIN REFRESH INFO");
					SourceUpdater.SourceWasUpdated += delegate { RefreshInfo (); };
					SourceUpdater.Start ();
					ShareUpdater.Start ();
				} catch (Exception error) {
					Log.Error (error);
				}
				
			});
			
			window.MakeKeyAndVisible ();
			
			return true;
		}


		void CheckNetwork ()
		{
			var th = new Thread ((ThreadStart)delegate {
				
				var good = false;
				try {
					var g = Http.Get ("http://lcarsreader.com");
					good = g.Length > 0;
				} catch (Exception) {
					good = false;
				}
				
				if (!good) {
					var numSources = Repo.Foreground.GetActiveSources ().Length;
					if (numSources < 1) {
						RunUI (delegate {
							var title = "No Network";
							var message = "You are not currently connected to the network.\n\nYou can still read messages, but you won't be able to add sources, go to links, or see images.\n\nPlease connect to a network to take full advantage of this application.";
							var alert = new UIAlertView (title, message, null, "OK");
							alert.Show ();
						});
					}
				}
			});
			th.Start ();
		}

		public UIInfo GetDefaultUI ()
		{
			var i = new UIInfo ();
			
			i.ScreenTitle = UIInfo.DefaultScreenTitle;
			
			i.MainFill = new LcarsDef ();
			i.MainFill.ComponentType = LcarsComponentType.Static;
			
			i.MiscBtn = new LcarsDef { ComponentType = LcarsComponentType.Static };
			
			i.MainTop = new LcarsDef { ComponentType = LcarsComponentType.Static };
			
			i.TopLeft = new LcarsDef { ComponentType = LcarsComponentType.MiscFunction, Caption = "REFRESH", Command = delegate {
				SourceRef r = null;
				if (MVC.SubView is MessageReader) {
					var m = ((MessageReader)MVC.SubView).MessageRef;
					if (m != null) {
						r = m.GetSourceReference ();
					}
				} else if (MVC.MainView is SourceTypeMessages) {
					var ms = (SourceTypeMessages)MVC.MainView;
					var s = ms.QuerySources.FirstOrDefault ();
					if (s != null) {
						r = s.Reference;
					}
				}
				SourceUpdater.ForceRefresh (r);
			} };
			
			i.TopMisc = new LcarsDef { ComponentType = LcarsComponentType.SystemFunction, Caption = Sounds.IsMuted ? "UNMUTE" : "MUTE", Command = delegate {
				Sounds.ToggleMute ();
				var a = AudioPlayerController.Inst;
				if (Sounds.IsMuted && a != null && a.IsPlaying) {
					ResumeAudioOnNextUnmute = true;
					a.Pause ();
				} else if (!Sounds.IsMuted && ResumeAudioOnNextUnmute && a != null) {
					a.Play ();
					ResumeAudioOnNextUnmute = false;
				}
				
				RefreshInfo ();
			} };
			
			i.MainSec = new LcarsDef { ComponentType = LcarsComponentType.MiscFunction, Caption = "SETTINGS", Command = delegate {
				var c = new UserSettings ();
				ShowMain (c);
			} };
			
			i.MainRel = new LcarsDef { ComponentType = LcarsComponentType.DisplayFunction };
			if (!IsIPad) {
				i.MainRel.Command = delegate { MVC.ToggleMainFullScreen (true); };
			}
			
			i.BottomLeft = new LcarsDef { ComponentType = LcarsComponentType.SystemFunction, Caption = App.Inst.IsIPad ? "ADD SOURCE" : "ADD", Command = delegate {
				var c = new ChooseSource ();
				ShowMain (c);
			} };
			
			return i;
		}

		public static void RunUI (Action a)
		{
			App.Inst.InvokeOnMainThread (delegate {
				try {
					a ();
				} catch (Exception error) {
					Log.Error (error);
				}
			});
		}

		public void PositionMainViewTitle (UILabel label)
		{
			if (!App.Inst.IsIPad) {
				label.Font = Theme.HugeFont;
			}
			
			var h = label.StringSize (label.Text, label.Font).Height;
			
			if (App.Inst.IsIPad) {
				h *= 1.1f;
			} else {
				h *= 0.75f;
			}
			
			var w = 320.0f;
			if (MVC.MainView != null) {
				w = MVC.MainView.View.Frame.Width;
			}
			
			label.AdjustsFontSizeToFitWidth = true;
			label.Frame = new RectangleF (10, Theme.HorizontalBarHeight + 2, w - 10, h);
			label.TextAlignment = UITextAlignment.Left;
			label.Lines = 1;
			label.MinimumFontSize = 10;
		}


		bool ResumeAudioOnNextUnmute = false;

		public void ShowSourceMessages (Source source)
		{
			_uiState.ActiveScreen = "Source";
			_uiState.ActiveScreenValue = source.GetType ().Name + "-" + source.Id;
			SaveUIState ();
			
			var c = new SourceTypeMessages ();
			c.SetSources (source);
			ShowMain (c);
		}

		public void ShowDialog (UIViewController vc)
		{
			MVC.ShowDialog (vc);
		}

		public void PopDialog ()
		{
			MVC.PopDialog ();
		}

		public void ShowSourceTypeMessages (Type sourceType, bool playAnimation)
		{
			var c = new SourceTypeMessages ();
			
			c.PlayAnimation = playAnimation;
			
			var repo = Repo.Foreground;
			var sources = repo.GetActiveSources (sourceType);
			
			_uiState.ActiveScreen = "SourceType";
			_uiState.ActiveScreenValue = sourceType.Name;
			SaveUIState ();
			
			c.SetSources (sources);
			ShowMain (c);
		}

		public void ShowHome ()
		{
			_uiState.ActiveScreen = "Home";
			_uiState.ActiveScreenValue = "";
			SaveUIState ();
			
			var c = new Home ();
			ShowMain (c);
		}

		MPMoviePlayerController _moviePlayer;
		MPMoviePlayerViewController _moviePlayerVC;
		public void ShowFullScreenVideo (string url)
		{
			if (_moviePlayer != null) {
				_moviePlayer.Dispose ();
				_moviePlayer = null;
			}
			if (_moviePlayerVC != null) {
				_moviePlayerVC.Dispose ();
				_moviePlayerVC = null;
			}
			if (App.Inst.IsIPad) {
				_moviePlayerVC = new MPMoviePlayerViewController (new NSUrl (url));
				MVC.PresentMoviePlayerViewController (_moviePlayerVC);
			} else {
				_moviePlayer = new MPMoviePlayerController (new NSUrl (url));
				_moviePlayer.Play ();
			}
		}

		void TempHide (UIViewController vc)
		{
			if (vc != null)
				vc.View.Hidden = true;
		}
		void UnHide (UIViewController vc)
		{
			if (vc != null)
				vc.View.Hidden = false;
		}

		public void ShowAddSource (Type sourceType)
		{
			var c = new AddSource ();
			c.SourceType = sourceType;
			ShowMain (c);
		}

		public void ShowMessage (MessageRef m, MessageRef[] messages)
		{
			MessageReader reader = null;
			
			//
			// Remove the SubView if it is not a reader
			//
			if (MVC.SubView != null) {
				reader = MVC.SubView as MessageReader;
				if (reader == null) {
					PopInfo (reader);
					MVC.SubView.View.RemoveFromSuperview ();
					MVC.SubView = null;
				}
			}
			
			//
			// Now add the reader
			//
			if (reader == null) {
				reader = new MessageReader ();
				reader.View.Frame = new RectangleF (MVC.MainView.View.Frame.Left, 1024, 600, 520);
				PushInfo (reader);
				MVC.View.AddSubview (reader.View);
				reader.View.Alpha = 0;
				UIView.BeginAnimations ("SV");
				reader.View.Frame = new RectangleF (MVC.MainView.View.Frame.Left, 1024 - 520, 600, 520);
				reader.View.Alpha = 1;
				MVC.MainView.View.Frame = new RectangleF (MVC.MainView.View.Frame.Left, MVC.MainView.View.Frame.Top, MVC.MainView.View.Frame.Width, 720 - 520);
				UIView.CommitAnimations ();
			}
			reader.MessageRef = m;
			
			//
			// Save the UI State
			//
			_uiState.ActiveScreen = "Message";
			_uiState.ActiveScreenValue = m.Id.ToString ();
			SaveUIState ();
			
			//
			// Mark the message as being read
			//
			if (!m.IsRead) {
				m.IsRead = true;
				var message = Repo.Foreground.Resolve (m);
				if (message != null) {
					message.IsRead = true;
					Repo.Foreground.Update (message);
				}
			}
			
			//
			// Update scrolly
			//
			var ms = MVC.MainView as SourceTypeMessages;
			if (ms != null) {
				ms.SetActiveItem (messages.IndexOf (mm => mm.Id == m.Id));
			}
			
			MVC.SubView = reader;
			MVC.Layout (true);
		}

		public bool ToggleSubViewFullScreen (bool animated)
		{
			return MVC.ToggleSubViewFullScreen (animated);
		}

		public void ShowMain (UIViewController vc)
		{
			
			if (MVC.IsDialogActive) {
				Sounds.PlayNotAllowed ();
				return;
			}
			
			if (MVC.MainView != null) {
				CloseSubView (false);
				
				MVC.MainView.View.RemoveFromSuperview ();
				PopInfo (MVC.MainView);
				MVC.MainView = null;
			}
			MVC.MainView = vc;
			vc.View.Frame = new RectangleF (1024, 60, vc.View.Frame.Width, vc.View.Frame.Height);
			PushInfo (vc);
			MVC.View.AddSubview (vc.View);
			MVC.View.BringSubviewToFront (vc.View);
			MVC.Layout (false);
			
			RefreshInfo ();
		}

		public void CloseSubView (bool animated)
		{
			if (MVC.SubView != null) {
				MVC.SubView.View.RemoveFromSuperview ();
				PopInfo (MVC.SubView);
				MVC.SubView = null;
				
				MVC.Layout (false);
			}
		}

		public void GotoNextMessage (IMessage curMsg, int offset)
		{
			var ms = MVC.MainView as SourceTypeMessages;
			if (ms != null) {
				ms.GotoNextMessage (curMsg, offset);
			}
		}

		List<UIInfo> InfoStack = new List<UIInfo> ();

		void PushInfo (UIViewController c)
		{
			if (c == null)
				return;
			var d = c as IHasInfo;
			if (d != null) {
				InfoStack.Add (d.Info);
			}
		}

		void PopInfo (UIViewController c)
		{
			if (c == null)
				return;
			var d = c as IHasInfo;
			if (d != null) {
				InfoStack.RemoveAt (InfoStack.Count - 1);
			}
		}

		UIInfo GetCurrentInfo ()
		{
			var CurrentInfo = GetDefaultUI ();
			foreach (var i in InfoStack) {
				CurrentInfo.UpdateWith (i);
			}
			return CurrentInfo;
		}

		void RefreshMessages (UIInfo info, UIViewController c)
		{
			var r = c as IRefreshable;
			if (r != null) {
				r.RefreshInfo (info);
			}
		}

		public void RefreshInfo ()
		{
			var info = GetCurrentInfo ();
			RefreshMessages (info, MVC.Top);
			RefreshMessages (info, MVC.Main);
		}

		UIState _uiState = new UIState ();

		void SaveUIState ()
		{
			Repo.Foreground.Update (_uiState);
		}

		bool LoadUIState (Repo repo)
		{
			var n = false;
			_uiState = repo.Table<UIState> ().FirstOrDefault ();
			if (_uiState == null) {
				n = true;
				_uiState = new UIState ();
				repo.Insert (_uiState);
			}
			return n;
		}

		void RestoreUIState ()
		{
			try {
				var repo = Repo.Foreground;
				var newState = LoadUIState (repo);
				
				if (newState) {
					InitialData.Load (repo);
				}
				
				if (_uiState.ActiveScreen == "Message") {
					var id = int.Parse (_uiState.ActiveScreenValue);
					var mq = from m in repo.Table<Message> ()
						where m.Id == id
						select m;
					var msg = mq.First ();
					var s = msg.GetSource (repo.GetActiveSources ());
					ShowSourceMessages (s);
					ShowMessage (msg.Reference, ((SourceTypeMessages)MVC.MainView).TheMessages);
				} else if (_uiState.ActiveScreen == "Source") {
					var parts = _uiState.ActiveScreenValue.Split ('-');
					var sourceTypeName = parts[0];
					var id = int.Parse (parts[1]);
					var s = repo.GetActiveSource (sourceTypeName, id);
					ShowSourceMessages (s);
				} else if (_uiState.ActiveScreen == "SourceType") {
					string sourceTypeName = _uiState.ActiveScreenValue;
					var sourceType = SourceTypes.Get (sourceTypeName);
					if (sourceType != null) {
						ShowSourceTypeMessages (sourceType, false);
					}
				} else if (_uiState.ActiveScreen == "Home") {
					ShowHome ();
				} else {
					ShowHome ();
				}
			} catch (Exception) {
				_uiState.ActiveScreen = "";
				_uiState.ActiveScreenValue = "";
				ShowHome ();
			}
		}
	}

	public class UIState
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public string ActiveScreen { get; set; }
		public string ActiveScreenValue { get; set; }
		public bool IsMuted { get; set; }

		public UIState ()
		{
			IsMuted = false;
			ActiveScreen = "";
			ActiveScreenValue = "";
		}
	}

	public static class ListEx
	{
		public static int IndexOf<T> (this IEnumerable<T> es, Func<T, bool> p)
		{
			int i = 0;
			foreach (var e in es) {
				if (p (e))
					return i;
				i++;
			}
			return -1;
		}
	}

	public class Application
	{
		static void Main (string[] args)
		{
			UIApplication.Main (args);
		}
	}
}
