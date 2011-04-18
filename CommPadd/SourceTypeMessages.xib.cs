
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;
using System.Threading;

namespace CommPadd
{
	public partial class SourceTypeMessages : UIViewController, IRefreshable, IHasInfo, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public SourceTypeMessages (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public SourceTypeMessages (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public SourceTypeMessages () : base("SourceTypeMessages", null)
		{
			Initialize ();
		}
		#endregion

		bool _showingData = false;

		void Initialize ()
		{
			table = new SourceTypeMessages.MessageTable (this);
			TheMessages = new MessageRef[0];
			SourceType = typeof(News);
			Info = new UIInfo ();
			QuerySources = new Source[0];
			
			Info.MainTop = new LcarsDef ();
			Info.MainTop.ComponentType = LcarsComponentType.SystemFunction;
			Info.MainTop.Caption = "ADD";
			Info.MainTop.Command = delegate { App.Inst.ShowAddSource (SourceType); };
			Info.MiscBtn = new LcarsDef { ComponentType = LcarsComponentType.DisplayFunction, NeedsDoubleTap = true, Caption = "REMOVE", Command = delegate { OnRemoveSource (); } };
		}

		public override void ViewDidLoad ()
		{
			
			try {
				DisHead.Font = Theme.TextFont;
				DisHead.TextColor = Theme.HeadingColor;
				FromHead.Font = Theme.TextFont;
				FromHead.TextColor = Theme.HeadingColor;
				SubHead.Font = Theme.TextFont;
				SubHead.TextColor = Theme.HeadingColor;
				DateHead.Font = Theme.TextFont;
				DateHead.TextColor = Theme.HeadingColor;
				
				tdata = new SourceTypeMessages.TData (this);
				tdel = new SourceTypeMessages.TDel (this);
				
				MsgTable.RowHeight = table.GetRowHeight (View);
				MsgTable.DataSource = tdata;
				MsgTable.Delegate = tdel;
				MsgTable.BackgroundColor = UIColor.Black;
				MsgTable.ContentMode = UIViewContentMode.Redraw;
				//MsgTable.Bounces = false;
				
				ObjectSelected += (i, obj) =>
				{
					try {
						SelectMessage (i, (MessageRef)obj);
					} catch (Exception err) {
						Log.Error (err);
					}
				};
				
				SourceUpdater.SourceWasUpdated += OnSourceUpdated;
				
				RefreshMessagesUI (false);
			} catch (Exception error) {
				Log.Error (error);
			}
			
		}

		public void DoLayout ()
		{
			RefreshMessagesUI (false);
		}

		public bool PlayAnimation { get; set; }

		public override void ViewDidUnload ()
		{
			try {
				SourceUpdater.SourceWasUpdated -= OnSourceUpdated;
			} catch (Exception error) {
				Log.Error (error);
			}
			
		}

		void OnRemoveSource ()
		{
			
			if (QuerySources.Length != 1)
				return;
			
			var s = QuerySources[0];
			var sourceType = s.GetType ();
			
			Repo.Foreground.RemoveSource (s);
			
			App.Inst.ShowSourceTypeMessages (sourceType, false);
		}

		public Type SourceType { get; set; }

		public void RefreshInfo (UIInfo info)
		{
			RefreshMessagesUI (false);
		}

		bool HasFrom ()
		{
			foreach (var m in TheMessages) {
				if (m.From.Length > 0) {
					return true;
				}
			}
			return false;
		}
		bool AllHaveFrom ()
		{
			foreach (var m in TheMessages) {
				if (m.From.Length == 0) {
					return false;
				}
			}
			return true;
		}

		public void RefreshMessagesUI (bool messagesChanged)
		{
			if (App.Inst.IsIPad) {
				table.FromCol.Hidden = !HasFrom ();
			} else {
				table.FromCol.Hidden = !AllHaveFrom ();
			}
			table.DisCol.Hidden = QuerySources.Length <= 1 || !App.Inst.IsIPad;
			
			if (messagesChanged) {
				table.Measure (View, TheMessages);
			}
			RedrawTable ();
			
			if (TheMessages.Length == 0) {
				DisHead.Hidden = true;
				FromHead.Hidden = true;
				SubHead.Hidden = true;
				DateHead.Hidden = true;
			} else {
				DisHead.Hidden = false;
				FromHead.Hidden = false;
				SubHead.Hidden = false;
				DateHead.Hidden = false;
			}
			
			var i = 0;
			var x = MsgTable.Frame.Left + table.LeftEdge;
			var y = MsgTable.Frame.Top - table.RowHeight;
			var h = table.RowHeight;
			
			if (!table.DisCol.Hidden) {
				DisHead.Frame = new RectangleF (x, y, table.Cols[i].Width, h);
				DisHead.Text = table.Cols[i].Title;
				x += table.Cols[i].Width;
				
				if (!DisHead.Hidden && QuerySources.Length > 0) {
					DisHead.Text = QuerySources[0].GetDistinguisherName ();
				}
				
			} else {
				DisHead.Hidden = true;
			}
			i++;
			
			if (!table.FromCol.Hidden) {
				FromHead.Frame = new RectangleF (x, y, table.Cols[i].Width, h);
				FromHead.Text = table.Cols[i].Title;
				x += table.Cols[i].Width;
			} else {
				FromHead.Hidden = true;
			}
			i++;
			
			SubHead.Frame = new RectangleF (x, y, table.Cols[i].Width, h);
			SubHead.Text = table.Cols[i].Title;
			x += table.Cols[i].Width;
			i++;
			
			DateHead.Frame = new RectangleF (x, y, table.Cols[i].Width, h);
			DateHead.Text = table.Cols[i].Title;
			x += table.Cols[i].Width;
			i++;
			
			MsgTable.ReloadData ();
		}

		void OnSourceUpdated (Source src)
		{
			try {
//			Console.WriteLine ("LIST ON SOURCE UPDATE");
				
				var showing = QuerySources.Any (s => s.Id == src.Id);
				if (showing) {
					RefreshMessages ();
				}
			} catch (Exception error) {
				Log.Error (error);
			}
		}

		void SelectMessage (NSIndexPath p, MessageRef m)
		{
			Sounds.PlayBeep ();
			SelectMessage (m);
		}

		void SelectMessage (MessageRef m)
		{
			App.Inst.ShowMessage (m, TheMessages);
		}

		public void GotoNextMessage (IMessage curMsg, int offset)
		{
			// Validate that we can do this
			if (curMsg == null || TheMessages == null || TheMessages.Length < 2) {
				return;
			}
			var i = TheMessages.IndexOf (m => m.Id == curMsg.Id);
			if (i < 0) {
				return;
			}
			
			// Move on to next
			i += offset;
			
			// Make it circular			
			if (i < 0)
				i += TheMessages.Length;
			if (i >= TheMessages.Length)
				i -= TheMessages.Length;
			
			SelectMessage (TheMessages[i]);
		}


		public UIInfo Info { get; private set; }

		void UpdateTitle ()
		{
			var s = SourceTypes.GetTitle (SourceType);
			if (QuerySources.Length == 1) {
				s += ": " + QuerySources[0].GetDistinguisher ().ToUpperInvariant ();
			}
			if (Info.ScreenTitle != s) {
				Info.ScreenTitle = s;
				App.Inst.RefreshInfo ();
			}
		}

		public Source[] QuerySources { get; private set; }

		LcarsComponentType[] PossibleTypes = new LcarsComponentType[] { LcarsComponentType.NavigationFunction, LcarsComponentType.MiscFunction, LcarsComponentType.CriticalFunction, LcarsComponentType.DisplayFunction, LcarsComponentType.PrimaryFunction, LcarsComponentType.SystemFunction };

		int _activeItemId = -1;

		void RefreshAll (UIView v)
		{
			v.SetNeedsDisplay ();
			foreach (var s in v.Subviews) {
				RefreshAll (s);
			}
		}

		void RedrawTable ()
		{
			RefreshAll (MsgTable);
		}

		public void SetActiveItem (int i)
		{
			if (i >= 0 && i < TheMessages.Length) {
				_activeItemId = TheMessages[i].Id;
				MsgTable.ScrollToRow (NSIndexPath.FromRowSection (i, 0), UITableViewScrollPosition.Middle, true);
				RedrawTable ();
			}
		}

		public void SetSources (params Source[] sources)
		{
			QuerySources = sources;
			
			SourceType = QuerySources.Length > 0 ? QuerySources[0].GetType () : typeof(News);
			
			//
			// Buttons to get to related sources
			//
			var allSources = new Source[0];
			if (QuerySources != null && QuerySources.Length > 0) {
				allSources = Repo.Foreground.GetActiveSources (SourceType).OrderBy (s => s.GetDistinguisher ()).ToArray ();
			}
			var n = allSources.Length;
			var buttons = new LcarsDef[n];
			for (int i = 0; i < n; i++) {
				var s = allSources[i];
				var b = new LcarsDef ();
				b.Caption = s.GetShortDistinguisher ().ToUpperInvariant ();
				
				b.ComponentType = PossibleTypes[s.Id % PossibleTypes.Length];
				
				b.Command = delegate { App.Inst.ShowSourceMessages (s); };
				
				buttons[i] = b;
			}
			
			Info.CommandButtons = buttons;
			
			Info.MainTop.Caption = "ADD " + SourceTypes.GetTitle (SourceType);
			if (sources.Length == 1) {
				Info.MiscBtn.ComponentType = LcarsComponentType.DisplayFunction;
				if (App.Inst.IsIPad) {
					Info.MiscBtn.Caption = "REMOVE " + sources[0].GetShortDistinguisher ().TruncateWords (15).ToUpperInvariant ();
				} else {
					Info.MiscBtn.Caption = "REMOVE";
				}
				Info.MiscBtn.IsCommandable = true;
			} else {
				Info.MiscBtn.ComponentType = LcarsComponentType.Static;
				Info.MiscBtn.Caption = "";
				Info.MiscBtn.IsCommandable = false;
			}
			
			UpdateTitle ();
			
			if (!_showingData && QuerySources.Length > 1 && PlayAnimation) {
				_scanner = new ScanningView (new RectangleF (0, 20, View.Frame.Width, View.Frame.Height - 20));
				_scanner.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
				_scanner.Alpha = 0;
				_scanner.StartScanning ();
				View.AddSubview (_scanner);
				UIView.BeginAnimations ("RetFadeIn");
				_scanner.Alpha = 1;
				UIView.CommitAnimations ();
				
				_showingData = true;
			}
			
			RefreshMessages ();
		}

		ScanningView _scanner;

		void RefreshMessages ()
		{
//			Console.WriteLine ("LIST REFRESHING MESSAGES");
			var th = new Thread ((ThreadStart)delegate { RefreshMessagesInBackground (); });
			th.Start ();
		}

		void RefreshMessagesInBackground ()
		{
			var messages = new MessageRef[0];
			
			using (var repo = new Repo ()) {
				if (QuerySources != null && QuerySources.Length > 0) {
					var ms = new List<MessageRef> ();
					foreach (var src in QuerySources) {
						SourceType = src.GetType ();
						ms.AddRange (repo.GetRecentMessages (src));
					}
					ms.Sort ((a, b) => b.PublishTime.CompareTo (a.PublishTime));
					messages = ms.ToArray ();
					if (App.Inst.IsSimulator) {
						Thread.Sleep (TimeSpan.FromSeconds (new Random ().NextDouble () * 2));
					}
				}
			}
			
			App.RunUI (delegate {
				var ms = TheMessages;
				TheMessages = messages;
				
				var changed = ms == null || (ms.Length != messages.Length);
				
				if (_scanner != null) {
					_scanner.StopScanning ();
					
					UIView.BeginAnimations ("RetFadeIn");
					_scanner.Alpha = 0;
					UIView.CommitAnimations ();
					
					NSTimer.CreateScheduledTimer (TimeSpan.FromMilliseconds (30), delegate { Sounds.PlayDataFound (); });
					NSTimer.CreateScheduledTimer (TimeSpan.FromMilliseconds (250), delegate {
						if (_scanner != null) {
							_scanner.RemoveFromSuperview ();
							_scanner = null;
						}
					});
				}
				
				RefreshMessagesUI (changed);
			});
		}

		MessageTable table;

		class MessageTable : Table
		{
			public readonly Col DisCol;
			public readonly Col FromCol = new Col { Title = "FROM", Print = o =>
			{
				var m = (MessageRef)o;
				return m.From;
			}, MaxWidth = App.Inst.IsIPad ? 132 : 60, Hidden = true };
			SourceTypeMessages P;
			public MessageTable (SourceTypeMessages ms) : base()
			{
				P = ms;
				
				ShouldHighlight = o => !((MessageRef)o).IsRead;
				IsSelected = o => ((MessageRef)o).Id == P._activeItemId;
				
				DisCol = new Col { Title = "DIS", Print = o =>
				{
					var m = (MessageRef)o;
					var s = m.GetSource (P.QuerySources);
					if (s == null)
						return "";
					else
						return s.GetShortDistinguisher ();
				}, MaxWidth = 132, Hidden = true };
				
				Cols.Add (DisCol);
				Cols.Add (FromCol);
				Cols.Add (new Col { Title = "SUBJECT", Stretches = true, Print = o =>
				{
					var m = (MessageRef)o;
					return m.GetTextSummary ();
				} });
				Cols.Add (new Col { Title = "DATE", Stretches = false, Print = o =>
				{
					var m = (MessageRef)o;
					return Theme.FormatTime (m.PublishTime.ToLocalTime ());
				} });
				ShouldHighlight = o =>
				{
					var m = (MessageRef)o;
					return !m.IsRead;
				};
			}
		}



		TData tdata;
		TDel tdel;

		public MessageRef[] TheMessages { get; private set; }

		public event Action<NSIndexPath, object> ObjectSelected;

		class TDel : UITableViewDelegate
		{
			SourceTypeMessages C;
			public TDel (SourceTypeMessages c)
			{
				C = c;
			}

			public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
			{
				try {
					var obj = C.TheMessages[indexPath.Row];
					if (C.ObjectSelected != null) {
						C.ObjectSelected (indexPath, obj);
					}
					NSTimer.CreateScheduledTimer (TimeSpan.FromSeconds (0.25), delegate {
						tableView.DeselectRow (indexPath, true);
						C.RedrawTable ();
					});
				} catch (Exception error) {
					Log.Error (error);
				}
				
			}
		}

		class TData : UITableViewDataSource
		{
			SourceTypeMessages C;
			public TData (SourceTypeMessages c)
			{
				C = c;
			}

			public override int RowsInSection (UITableView tableview, int section)
			{
				try {
					return C.TheMessages.Length;
				} catch (Exception error) {
					Log.Error (error);
					return 0;
				}
				
			}
			public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
			{
				UITableViewCell cell = null;
				try {
					// Get cached cell
					cell = tableView.DequeueReusableCell ("M");
					if (cell == null) {
						cell = new UITableViewCell (UITableViewCellStyle.Default, "M");
					}
					
					// Make sure the cell has my custom view, if not add it
					var v = cell.ViewWithTag (42) as Row;
					if (v == null) {
						v = new SourceTypeMessages.Row ();
						v.Table = C.table;
						v.BackgroundColor = UIColor.Black;
						v.Tag = 42;
						cell.AddSubview (v);
					}
					
					// Format the base cell
					cell.SelectedBackgroundView.BackgroundColor = UIColor.FromRGB (0.1f, 0.1f, 0.1f);
					cell.SelectionStyle = UITableViewCellSelectionStyle.Gray;
					cell.BackgroundColor = UIColor.Black;
					
					if (cell.SelectedBackgroundView != null && !(cell.SelectedBackgroundView is SelBackView)) {
						cell.SelectedBackgroundView = new SelBackView ();
					}
					
					// Set view model for my custom cell view
					var msg = C.TheMessages[indexPath.Row];
					v.Data = msg;
					v.Frame = new System.Drawing.RectangleF (0, 0, tableView.Frame.Width, C.table.RowHeight);
					
				} catch (Exception error) {
					Log.Error (error);
				}
				return cell;
			}
		}

		class Col
		{
			public string Title = "";
			public Func<object, string> Print;
			public bool Stretches = false;
			public float Width = 30;
			public bool Hidden = false;
			public float MaxWidth = 0;
		}

		class Table
		{
			/*: UITableView*/
			public readonly List<Col> Cols = new List<Col> ();
			public UIFont RowFont = Theme.TextFont;
			public Func<object, bool> ShouldHighlight = o => false;
			public Func<object, bool> IsSelected = o => false;

			public float RowHeight { get; private set; }

			public float LeftEdge { get; private set; }

			public Table ()
			{
				RowHeight = 30;
			}

			public float GetRowHeight (UIView v)
			{
				RowHeight = v.StringSize ("HELLO", RowFont).Height * 1.1f;
				return RowHeight;
			}

			public void Measure<T> (UIView v, T[] objs)
			{
				
				//Sounds.PlayDataRetrieval();
				
				
				
				for (var i = 0; i < Cols.Count; i++) {
					var col = Cols[i];
					if (col.Hidden)
						continue;
					var cw = 60.0f;
					var maxLen = 0;
					var pad = i < Cols.Count - 1 ? 14 : 0;
					foreach (var o in objs) {
						var s = col.Print (o);
						if (s.Length < maxLen)
							continue;
						var w = v.StringSize (s, RowFont).Width + pad;
						if (w > cw) {
							cw = w;
							maxLen = s.Length;
						}
					}
					col.Width = cw;
				}
				
				var width = v.Frame.Width;
				var colsWidth = GetColsWidth (true);
				var noStretchWidth = GetColsWidth (false);
				
				var mwidth = width * 0.5f;
				
				if (colsWidth > width) {
					foreach (var c in Cols) {
						if (c.MaxWidth > 1 && c.Width > c.MaxWidth) {
							c.Width = c.MaxWidth;
						}
					}
					noStretchWidth = GetColsWidth (false);
					if (noStretchWidth < width) {
						var sw = width - noStretchWidth;
						foreach (var c in Cols) {
							if (c.Stretches && !c.Hidden) {
								c.Width = sw - 6;
								// Lots of room
								break;
							}
						}
					}
				} else if (colsWidth < mwidth) {
					var extra = (mwidth - colsWidth) / (Cols.Where (c => !c.Hidden).Count () - 1);
					for (var i = 0; i < Cols.Count - 1; i++) {
						var c = Cols[i];
						if (!c.Hidden) {
							c.Width += extra;
						}
					}
				}
				
				colsWidth = GetColsWidth (true);
				if (colsWidth >= width) {
					LeftEdge = 0;
				} else {
					LeftEdge = (width - colsWidth) / 2;
				}
			}
			float GetColsWidth (bool countStretchy)
			{
				var colsWidth = 0.0f;
				foreach (var c in Cols) {
					if (!c.Hidden) {
						if (countStretchy || (!c.Stretches)) {
							colsWidth += c.Width;
						}
					}
				}
				return colsWidth;
			}
		}
		class SelBackView : UIView
		{
			static UIColor Dark = UIColor.FromRGB (0.3f, 0.3f, 0.3f);
			public override void Draw (RectangleF rect)
			{
				try {
					Dark.SetFill ();
					var c = UIGraphics.GetCurrentContext ();
					c.FillRect (rect);
				} catch (Exception error) {
					Log.Error (error);
				}
				
			}
		}
		class Row : UIView
		{
			object data;
			Table table;
			public object Data {
				get { return data; }
				set {
					if (data != value) {
						data = value;
						vals = null;
						SetNeedsDisplay ();
					}
				}
			}
			public Table Table {
				get { return table; }
				set {
					if (table != value) {
						table = value;
						vals = null;
						SetNeedsDisplay ();
					}
				}
			}
			public Row ()
			{
			}
			string[] vals;
			public override void Draw (System.Drawing.RectangleF rect)
			{
				try {
					var n = table.Cols.Count;
					
					if (vals == null) {
						
						vals = new string[n];
						for (var i = 0; i < n; i++) {
							vals[i] = table.Cols[i].Print (data);
						}
					}
					
					var x = table.LeftEdge;
					
					
//				UIColor.Green.SetColor();
//				var c = UIGraphics.GetCurrentContext();
//				c.FillRect(rect);
//				var rr = new RectangleF(0, 0, 132, table.RowHeight);
//				DrawString("Hello", rr, table.RowFont, UILineBreakMode.TailTruncation);
//				return;
//				
//				
					if (table.IsSelected (data)) {
						Theme.HeadingColor.SetColor ();
					} else if (table.ShouldHighlight (data)) {
						UIColor.White.SetColor ();
					} else {
						Theme.TextColor.SetColor ();
					}
					
					for (var i = 0; i < n; i++) {
						var col = table.Cols[i];
						if (col.Hidden)
							continue;
						
						var r = new RectangleF (x, 0, col.Width, table.RowHeight);
						var s = vals[i];
						
						DrawString (s, r, table.RowFont, UILineBreakMode.TailTruncation);
						
						x += col.Width;
					}
				} catch (Exception error) {
					Log.Error (error);
				}
				
			}
		}
	}
}
