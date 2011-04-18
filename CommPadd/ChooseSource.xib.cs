
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;

namespace CommPadd
{
	public partial class ChooseSource : UIViewController, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public ChooseSource (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public ChooseSource (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public ChooseSource () : base("ChooseSource", null)
		{
			Initialize ();
		}

		void Initialize ()
		{
		}

		#endregion

		Type[] sourceTypes;

		SelectItem _select;

		public override void ViewDidLoad ()
		{
			try {
				TitleLabel.Font = Theme.HugeFont;
				sourceTypes = SourceTypes.All;
				
				var items = new List<LcarsDef> ();
				foreach (var sourceType in sourceTypes) {
					var t = sourceType;
					var def = new LcarsDef ();
					
					def.Caption = SourceTypes.GetTitle (t);
					def.Command = delegate { Choose (t); };
					items.Add (def);
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
			var y = TitleLabel.Frame.Bottom + App.Inst.LabelGap;
			_select.Frame = new RectangleF (TitleLabel.Frame.Left, y, TitleLabel.Frame.Width, View.Frame.Height - y);
		}

		void Choose (Type sourceType)
		{
			App.Inst.ShowAddSource (sourceType);
		}
	}
}
