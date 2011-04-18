
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;

namespace CommPadd
{
	public partial class ChooseEmail : UIViewController, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public ChooseEmail (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public ChooseEmail (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public ChooseEmail () : base("ChooseEmail", null)
		{
			Initialize ();
		}

		void Initialize ()
		{
		}

		#endregion

		SelectItem _select;

		public event Action<string> EmailSelected;

		public override void ViewDidLoad ()
		{
			try {
				TitleLabel.Text = "CHOOSE EMAIL";
				TitleLabel.Font = Theme.HugeFont;
				
				//View.BackgroundColor = UIColor.Yellow;
				
				var items = new List<LcarsDef> ();
				
				var a = new MonoTouch.AddressBook.ABAddressBook ();
				var people = a.GetPeople ();
				foreach (var p in people) {
					
					var emails = p.GetEmails ();
					
					foreach (var e in emails) {
						var def = new LcarsDef ();
						
						var addr = e.Value;
						
						def.Caption = addr;
						def.Command = delegate { Choose (addr); };
						items.Add (def);
					}
				}
				
				var y = TitleLabel.Frame.Bottom + App.Inst.LabelGap;
				_select = new SelectItem (items, new RectangleF (TitleLabel.Frame.Left, y, View.Frame.Width, View.Frame.Height - y));
				View.AddSubview (_select);
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		public void DoLayout ()
		{
			App.Inst.PositionMainViewTitle (TitleLabel);
			if (_select != null && View != null) {
				var y = TitleLabel.Frame.Bottom + App.Inst.LabelGap;
				_select.Frame = new RectangleF (TitleLabel.Frame.Left, y, TitleLabel.Frame.Width, View.Frame.Height - y);
				_select.DoLayout ();
			}
		}

		void Choose (string email)
		{
			App.Inst.PopDialog ();
			if (EmailSelected != null) {
				EmailSelected (email);
			}
		}
		
	}
}
