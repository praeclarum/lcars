
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;

namespace CommPadd
{
	public partial class ShareView : UIViewController, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public ShareView (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public ShareView (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public ShareView () : base("ShareView", null)
		{
			Initialize ();
		}

		void Initialize ()
		{
		}

		#endregion

		Form _form;

		ShareInfo _info;

		public Message Message { get; set; }

		public override void ViewDidLoad ()
		{
			try {
				TitleLabel.Font = Theme.HugeFont;
				TitleLabel.Text = "EMAIL";
				
				_info = Repo.Foreground.Table<ShareInfo> ().FirstOrDefault ();
				if (_info == null) {
					_info = new ShareInfo { From = UIDevice.CurrentDevice.Name, To = "" };
					Repo.Foreground.Insert (_info);
				}
				
				var y = TitleLabel.Frame.Bottom + App.Inst.LabelGap;
				_form = new Form (_info, new RectangleF (TitleLabel.Frame.Left, y, View.Frame.Width, View.Frame.Height - y));
				_form.ConfirmButtonText = "SHARE";
				_form.ShowCancelButton = true;
				
				_form.OnCancel += delegate {
					try {
						App.Inst.PopDialog ();
					} catch (Exception error) {
						Log.Error (error);
					}
				};
				_form.OnOK += delegate {
					try {
						Repo.Foreground.Update (_info);
						
						var sh = new ShareMessage { From = _info.From, To = _info.To, Comment = "", MessageId = Message.Id, Status = ShareMessageStatus.Unsent };
						Repo.Foreground.Insert (sh);
						
						ShareUpdater.SetSharesChanged ();
						
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
