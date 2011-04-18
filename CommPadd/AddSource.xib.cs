
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Reflection;
using System.Drawing;

namespace CommPadd
{
	public partial class AddSource : UIViewController, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public AddSource (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public AddSource (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public AddSource () : base("AddSource", null)
		{
			Initialize ();
		}

		void Initialize ()
		{
			SourceType = typeof(News);
		}

		#endregion

		public Type SourceType { get; set; }

		public Source Source { get; private set; }

		Form _form;

		public override void ViewDidLoad ()
		{
			try {
				AddLabel.Font = Theme.HugeFont;
				
				Source = (Source)Activator.CreateInstance (SourceType);
				
				AddLabel.Text = "ADD " + SourceTypes.GetTitle (SourceType);
				
				var y = AddLabel.Frame.Bottom + App.Inst.LabelGap;
				_form = new Form (Source, new RectangleF (AddLabel.Frame.Left, y, View.Frame.Width, View.Frame.Height - y));
				_form.OnOK += delegate {
					try {
						Repo.Foreground.AddOrActivateSource (Source);
						SourceUpdater.SetSourcesChanged ();
						App.Inst.ShowSourceMessages (Source);
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
			App.Inst.PositionMainViewTitle (AddLabel);
			if (_form != null) {
				var y = AddLabel.Frame.Bottom + App.Inst.LabelGap;
				_form.Frame = new RectangleF (AddLabel.Frame.Left, y, AddLabel.Frame.Width, View.Frame.Height - y);
				_form.DoLayout ();
			}
		}
	}
}
