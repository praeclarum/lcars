
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;

namespace CommPadd
{
	public partial class UserSettings : UIViewController, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public UserSettings (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public UserSettings (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public UserSettings () : base("UserSettings", null)
		{
			Initialize ();
		}

		void Initialize ()
		{
		}

		#endregion

		class Settings
		{
//			[Optional]
//			public string Email { get; set; }
			[Section, Optional, EmailInput]
			public string GoogleAccount { get; set; }
			[Optional]
			public string GooglePassword { get; set; }
		}

		Settings _settings;
		Form _form;

		public override void ViewDidLoad ()
		{
			try {
				TitleLabel.Font = Theme.HugeFont;
				TitleLabel.Text = "USER SETTINGS";
				
				var info = Repo.Foreground.Table<GoogleReaderConfig> ().FirstOrDefault ();
				if (info == null) {
					info = new GoogleReaderConfig ();
					Repo.Foreground.Insert (info);
				}
				
				_settings = new UserSettings.Settings ();
				
				_settings.GoogleAccount = info.Account;
				_settings.GooglePassword = info.Password;
				
				var y = TitleLabel.Frame.Bottom + App.Inst.LabelGap;
				_form = new Form (_settings, new RectangleF (TitleLabel.Frame.Left, y, View.Frame.Width, View.Frame.Height - y));
				_form.ConfirmButtonText = "SAVE";
				
				_form.OnCancel += delegate {
					try {
						App.Inst.PopDialog ();
					} catch (Exception error) {
						Log.Error (error);
					}
				};
				_form.OnOK += delegate {
					try {
						info.Account = _settings.GoogleAccount;
						info.Password = _settings.GooglePassword;
						Repo.Foreground.Update (info);
						
						GoogleReaderUpdater.SetReaderChanged ();
						
						App.Inst.PopDialog ();
					} catch (Exception error) {
						Log.Error (error);
					}
				};
				
				View.AddSubview (_form);
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		public void DoLayout ()
		{
			App.Inst.PositionMainViewTitle (TitleLabel);
			if (_form != null && View != null) {
				var y = TitleLabel.Frame.Bottom + App.Inst.LabelGap;
				_form.Frame = new RectangleF (TitleLabel.Frame.Left, y, TitleLabel.Frame.Width, View.Frame.Height - y);
				_form.DoLayout ();
			}
		}
		
	}
}
