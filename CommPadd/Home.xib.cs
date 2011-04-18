
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace CommPadd
{
	public partial class Home : UIViewController, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public Home (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public Home (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public Home () : base("Home", null)
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
				StatusLabel.Font = Theme.RidiculousFont;
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		public void DoLayout ()
		{
			
			App.Inst.PositionMainViewTitle (StatusLabel);
			
		}
		
	}
}
