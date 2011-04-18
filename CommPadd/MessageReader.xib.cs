
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;
using HtmlAgilityPack;
using MonoTouch.MediaPlayer;

namespace CommPadd
{
	public partial class MessageReader : UIViewController, IHasInfo, ILayoutable
	{
		#region Constructors

		// The IntPtr and initWithCoder constructors are required for controllers that need 
		// to be able to be created from a xib rather than from managed code

		public MessageReader (IntPtr handle) : base(handle)
		{
			Initialize ();
		}

		[Export("initWithCoder:")]
		public MessageReader (NSCoder coder) : base(coder)
		{
			Initialize ();
		}

		public MessageReader () : base("MessageReader", null)
		{
			Initialize ();
		}

		void Initialize ()
		{
		}

		#endregion

		Message Message;
		MessageRef _messageRef;

		public MessageRef MessageRef {
			get { return _messageRef; }
			set {
				if (_messageRef == null || _messageRef.Id != value.Id) {
					_messageRef = value;
					Message = Repo.Foreground.Resolve (_messageRef);
					OnModelChanged ();
				}
			}
		}

		public UIInfo Info { get; private set; }

		class WDel : UIWebViewDelegate
		{
			public WDel (MessageReader r)
			{
			}
		}
		WDel wdel;

		LcarsComp Filler;

		float CompWidth;

		public override void ViewDidLoad ()
		{
			try {
				Info = new UIInfo ();
				
				TextBox.MultipleTouchEnabled = true;
				
				CompWidth = CloseBtn.Frame.Width;
				if (!App.Inst.IsIPad) {
					CompWidth *= 0.75f;
				}
				
				wdel = new MessageReader.WDel (this);
				TextBox.Delegate = wdel;
				TextBox.MultipleTouchEnabled = true;
				
				OriginalBtn.Def.Caption = "ORIGINAL";
				OriginalBtn.Def.ComponentType = LcarsComponentType.NavigationFunction;
				OriginalBtn.Def.PlayCommandSound = delegate {
					if (Message != null && !string.IsNullOrEmpty (Message.Url)) {
						Sounds.PlayDataRetrieval ();
					} else {
						Sounds.PlayNotAllowed ();
					}
				};
				OriginalBtn.Def.Command = delegate { ToggleOriginal (); };
				
				ShareBtn.Def.ComponentType = LcarsComponentType.PrimaryFunction;
				ShareBtn.Def.Caption = "SHARE";
				ShareBtn.Def.Command = delegate {
					if (Message == null)
						return;
					var s = new ShareView ();
					s.Message = Message;
					App.Inst.ShowDialog (s);
				};
				
				Filler = new LcarsComp ();
				Filler.Frame = new RectangleF (ShareBtn.Frame.Left, ShareBtn.Frame.Bottom, ShareBtn.Frame.Width, 0);
				Filler.Def.ComponentType = LcarsComponentType.Static;
				Filler.Hidden = true;
				View.AddSubview (Filler);
				
				TextBox.Layer.CornerRadius = 10;
				
				
				CloseBtn.Def.Caption = "CLOSE";
				CloseBtn.Def.ComponentType = LcarsComponentType.DisplayFunction;
				CloseBtn.Def.Command = delegate { App.Inst.CloseSubView (true); };
				
				RelativeComp.Def.ComponentType = LcarsComponentType.SystemFunction;
				RelativeComp.Width = CloseBtn.Width;
				RelativeComp.Shape = LcarsShape.TopRight;
				RelativeComp.Height = Theme.HorizontalBarHeight;
				RelativeComp.Def.Command = delegate {
					var full = App.Inst.ToggleSubViewFullScreen (true);
					RelativeComp.Def.Caption = full ? "MIN" : "MAX";
					DoLayout (full);
					RelativeComp.SetNeedsDisplay ();
				};
				RelativeComp.Def.Caption = "MAX";
				
				TopBtn.Def.ComponentType = LcarsComponentType.MiscFunction;
				TopBtn.Def.Caption = "PREV";
				TopBtn.Def.Command = delegate { App.Inst.GotoNextMessage (Message, -1); };
				
				BottomBtn.Def.ComponentType = LcarsComponentType.MiscFunction;
				BottomBtn.Def.Caption = "NEXT";
				BottomBtn.Def.Command = delegate { App.Inst.GotoNextMessage (Message, 1); };
				
				Theme.MakeBlack (TextBox);
				TextBox.Hidden = true;
				SetHtml ("", "");
				
				DoLayout ();
			} catch (Exception error) {
				Log.Error (error);
			}
			
		}

		public void DoLayout ()
		{
			DoLayout (true);
		}

		void SetWidth (UIView v, float w)
		{
			v.Frame = new RectangleF (v.Frame.Right - w, v.Frame.Top, w, v.Frame.Height);
		}

		void DoLayout (bool full)
		{
			if (full) {
				
				SetWidth (CloseBtn, CompWidth);
				SetWidth (ShareBtn, CompWidth);
				SetWidth (TopBtn, CompWidth);
				SetWidth (BottomBtn, CompWidth);
				SetWidth (OriginalBtn, CompWidth);
				SetWidth (PlayBtn, CompWidth);
				SetWidth (Filler, CompWidth);
				RelativeComp.Width = CompWidth;
				
				Filler.Frame = new RectangleF (ShareBtn.Frame.Left, ShareBtn.Frame.Bottom, ShareBtn.Frame.Width, CloseBtn.Frame.Top - ShareBtn.Frame.Bottom);
				Filler.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleHeight;
				Filler.Hidden = true;
				
				var top = RelativeComp.Frame.Top + 1.5f * RelativeComp.Height;
				var margin = RelativeComp.Height * 0.75f;
				TextBox.Frame = new RectangleF (margin, top, CloseBtn.Frame.Left - 2 * margin, View.Frame.Height - top - margin);
			} else {
				Filler.Hidden = true;
			}
		}

		bool _showingMessages = true;

		void UpdateOriginalToggleUI ()
		{
			if (Message == null || string.IsNullOrEmpty (Message.Url)) {
				OriginalBtn.Def.ComponentType = LcarsComponentType.Gray;
				OriginalBtn.Def.Caption = "";
				OriginalBtn.Def.IsCommandable = false;
			} else if (!_showingMessages) {
				OriginalBtn.Def.ComponentType = LcarsComponentType.Static;
				OriginalBtn.Def.Caption = "RESTORE";
				OriginalBtn.Def.IsCommandable = true;
			} else {
				OriginalBtn.Def.ComponentType = LcarsComponentType.NavigationFunction;
				OriginalBtn.Def.Caption = "ORIGINAL";
				OriginalBtn.Def.IsCommandable = true;
			}
			OriginalBtn.SetNeedsDisplay ();
		}

		void ToggleOriginal ()
		{
			if (Message == null || string.IsNullOrEmpty (Message.Url))
				return;
			
			if (_showingMessages) {
				SetHtml ("<div class='purple' style='font-size:300%;padding:0.1em;'>WORKING</div>", "");
				NSTimer.CreateScheduledTimer (TimeSpan.FromSeconds (0.333), delegate {
					TextBox.ScalesPageToFit = true;
					TextBox.LoadRequest (NSUrlRequest.FromUrl (new NSUrl (Message.Url)));
				});
				_showingMessages = false;
			} else {
				_lastHtml = "";
				OnModelChanged ();
			}
			
			UpdateOriginalToggleUI ();
			
			OriginalBtn.SetNeedsDisplay ();
			
		}

		bool _loadFromLocal = true;
		DateTime _lastLoadSoundTime = DateTime.UtcNow.AddMinutes (-1);

		void HandleTextBoxLoadStarted (object sender, EventArgs e)
		{
			var now = DateTime.UtcNow;
			
			if (_loadFromLocal) {
				_lastLoadSoundTime = now;
				_loadFromLocal = false;
				return;
			}
			
			
			if (now - _lastLoadSoundTime > TimeSpan.FromSeconds (5)) {
				Sounds.PlayDataRetrieval ();
				_lastLoadSoundTime = now;
			}
		}

		string _lastHtml = "scuba";
		Message _lastMessage = null;
		AudioPlayerController _audioPlayer;

		bool ShouldSanitize (string html)
		{
			return html.IndexOf ("<object") < 0;
		}

		void OnModelChanged ()
		{
//			Console.WriteLine ("READER ON MODEL CHANGED");
			
			var html = "";
			if (Message != null) {
				html = Message.BodyHtml;
			}
			
			_showingMessages = true;
			UpdateOriginalToggleUI ();
			
			TopBtn.Def.ComponentType = LcarsComponentType.MiscFunction;
			TopBtn.Def.IsCommandable = true;
			BottomBtn.Def.ComponentType = LcarsComponentType.MiscFunction;
			BottomBtn.Def.IsCommandable = true;
			
			TopBtn.SetNeedsDisplay ();
			BottomBtn.SetNeedsDisplay ();
						
		    var eq = (_lastHtml == html && object.ReferenceEquals(Message, _lastMessage));
			if (!eq) {
				_lastHtml = html;
				_lastMessage = Message;
				
				var subj = "";
				var byline = "";
				if (Message != null) {
					subj = Message.Subject;
					byline = Message.From;
					var tm = Message.PublishTime.ToLocalTime ();
					byline += " " + tm.ToLongDateString ();
					byline += " " + tm.ToShortTimeString ();
				}				
				
				var head = "<div class='byline'>" + Html.Encode (byline) + "</div>";
				
				Source source = null;
				if (Message != null) {
					source = Repo.Foreground.Resolve (Message.GetSourceReference ());
				}
				
				var showSubj = source.ShouldShowSubjectWithBody;
				var subjIsBody = false;
				if (Message != null && Message.BodyHtml == "&nbsp;") {
					showSubj = false;
					subjIsBody = true;
				}
				
				if (source != null && showSubj) {
					head = "<h1>" + Html.Encode (subj) + "</h1>" + head;
				}
				
				var shtml = html;
				
				if (subjIsBody) {
					shtml = Message.Subject;
				}
				
				if (ShouldSanitize (shtml)) {
					shtml = Sanitize (shtml);
				}
				
				shtml = head + shtml;
				SetHtml (shtml, "");
			}
			TextBox.Hidden = string.IsNullOrEmpty (html);
			
			Info.ScreenTitle = Message.GetTextSummary ();
			
			KillMedia ();
			
			var isAudio = Message.MediaUrl.IndexOf (".mp3") > 0;
			
			if (Message.HasMedia) {
				
				PlayBtn.Def.IsCommandable = true;
				PlayBtn.Def.ComponentType = LcarsComponentType.SystemFunction;
				PlayBtn.Def.Caption = GetPlayCaption ();
				PlayBtn.Def.Command = delegate {
					if (isAudio) {
						if (_audioPlayer == null) {
							_audioPlayer = AudioPlayerController.Get (Message.MediaUrl);
							_audioPlayer.Play ();
							_updatePlayTimer = NSTimer.CreateRepeatingScheduledTimer (TimeSpan.FromSeconds (1), delegate {
								if (_audioPlayer != null) {
									PlayBtn.Def.Caption = _audioPlayer.GetStatus (false);
									PlayBtn.SetNeedsDisplay ();
								}
							});
						} else {
							if (_audioPlayer.IsPlaying) {
								_audioPlayer.Pause ();
							} else {
								_audioPlayer.Play ();
							}
						}
					} else {
						App.Inst.ShowFullScreenVideo (Message.MediaUrl);
					}
					PlayBtn.SetNeedsDisplay ();
				};
			} else {
				PlayBtn.Def.IsCommandable = false;
				PlayBtn.Def.ComponentType = LcarsComponentType.Gray;
				PlayBtn.Def.Caption = "";
			}
			PlayBtn.SetNeedsDisplay ();
			
			App.Inst.RefreshInfo ();
		}

		public override void ViewDidUnload ()
		{
			try {
				KillMedia ();
			} catch (Exception error) {
				Log.Error (error);
			}
			
		}

		void KillMedia ()
		{
			if (_audioPlayer != null) {
				_audioPlayer = null;
				_updatePlayTimer.Dispose ();
				_updatePlayTimer = null;
			}
		}

		NSTimer _updatePlayTimer = null;

		string GetPlayCaption ()
		{
			return "PLAY";
		}

		static bool ContainsMedia (string html)
		{
			return (html.IndexOf ("<img") >= 0) || (html.IndexOf ("<video") >= 0) || (html.IndexOf ("<obj") >= 0);
		}

		void SetHtml (string html, string bas)
		{
			_loadFromLocal = true;
			
			var head = HtmlHead;
			
			if (App.Inst.IsIPad) {
				TextBox.ScalesPageToFit = false;
			} else {
				if (ContainsMedia (html)) {
					head = head.Replace ("18px", "24px");
					TextBox.ScalesPageToFit = true;
				} else {
					head = head.Replace ("18px", "12px");
					TextBox.ScalesPageToFit = false;
				}
			}
			
			var page = head + html + HtmlTail;
			TextBox.LoadHtmlString (page, new NSUrl (bas));
		}

		void KillElems (HtmlNode n)
		{
			var cs = n.ChildNodes.Cast<HtmlNode> ().ToArray ();
			foreach (var c in cs) {
				var name = c.Name.ToLowerInvariant ();
				if (name == "input" || name == "textarea" || name == "button" || name == "script" || name == "form") {
					n.RemoveChild (c);
				} else if (name == "a") {
					var href = c.Attributes["href"];
					if (href == null || !href.Value.StartsWith ("http")) {
						n.RemoveChild (c);
					}
				}
			}
			foreach (var c in n.ChildNodes) {
				KillElems (c);
			}
		}

		void KillAttrs (HtmlNode n)
		{
			var toKill = new List<HtmlAttribute> ();
			foreach (HtmlAttribute a in n.Attributes) {
				if (a.Name != "href" && a.Name != "src") {
					toKill.Add (a);
				}
			}
			foreach (var k in toKill) {
				n.Attributes.Remove (k.Name);
			}
			foreach (var c in n.ChildNodes) {
				KillAttrs (c);
			}
		}

		string Sanitize (string htmlString)
		{
			try {
				var doc = Html.Parse (htmlString);
				
				var root = doc.FirstChild;
				if (root != null && root.Name == "html") {
					root = root.SelectSingleNode ("//body");
					if (root != null) {
						root.Name = "div";
						doc = root;
					}
				}
				
				KillAttrs (doc);
				KillElems (doc);
				
				var h = doc.WriteTo ();
				//Console.WriteLine (h);
				return h;
			} catch (Exception) {
				return "";
			}
		}

		const string HtmlHead = @"
<html>
<head></head>
<style>
body { 
	background-color:#000; color:rgb(255,159,0);
	font-family:Helvetica; font-size: 18px; font-weight: bold;
	margin: 0;
	padding: 0 0 10em 0;
}
tr, td { padding:0;margin:0; }
small, font, td { font-size: 18px; }
img { display: block; }
.purple {color:rgb(156,156,255);font-family:lcars, Helvetica;}
a { color:rgb(156,156,255); text-decoration:none;padding-right:5px; }
a:hover { background-color:#333;color:rgb(156,156,255);-webkit-border-radius:3px; }
.byline {color:rgb(156,156,255);font-family:lcars, Helvetica;margin-bottom:0.5em;}
h1,h2,h3,h4,h5 {color:rgb(156,156,255);font-family:lcars, Helvetica;margin:0;}
h4{margin-top:0.5em; }
blockquote {color:#ffff99;marign-top:0em;}
</style>
<body>";
		const string HtmlTail = @"
<script>
var vid = document.getElementById('thevideo');
if (vid) {
  vid.play();
}
</script>
</body></html>";
	}
}
