// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 4.0.30319.1
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------

namespace CommPadd {
	
	
	// Base type probably should be MonoTouch.UIKit.UIViewController or subclass
	[MonoTouch.Foundation.Register("TopLcars")]
	public partial class TopLcars {
		
		private MonoTouch.UIKit.UIView __mt_view;
		
		private LcarsComp __mt_PrimaryComp;
		
		private LcarsComp __mt_RelativeComp;
		
		private MonoTouch.UIKit.UILabel __mt_TitleLabel;
		
		private MonoTouch.UIKit.UITableView __mt_MsgTable;
		
		private LcarsComp __mt_PlayBtn;
		
		private LcarsComp __mt_HomeBtn;
		
		#pragma warning disable 0169
		[MonoTouch.Foundation.Connect("view")]
		private MonoTouch.UIKit.UIView view {
			get {
				this.__mt_view = ((MonoTouch.UIKit.UIView)(this.GetNativeField("view")));
				return this.__mt_view;
			}
			set {
				this.__mt_view = value;
				this.SetNativeField("view", value);
			}
		}
		
		[MonoTouch.Foundation.Connect("PrimaryComp")]
		private LcarsComp PrimaryComp {
			get {
				this.__mt_PrimaryComp = ((LcarsComp)(this.GetNativeField("PrimaryComp")));
				return this.__mt_PrimaryComp;
			}
			set {
				this.__mt_PrimaryComp = value;
				this.SetNativeField("PrimaryComp", value);
			}
		}
		
		[MonoTouch.Foundation.Connect("RelativeComp")]
		private LcarsComp RelativeComp {
			get {
				this.__mt_RelativeComp = ((LcarsComp)(this.GetNativeField("RelativeComp")));
				return this.__mt_RelativeComp;
			}
			set {
				this.__mt_RelativeComp = value;
				this.SetNativeField("RelativeComp", value);
			}
		}
		
		[MonoTouch.Foundation.Connect("TitleLabel")]
		private MonoTouch.UIKit.UILabel TitleLabel {
			get {
				this.__mt_TitleLabel = ((MonoTouch.UIKit.UILabel)(this.GetNativeField("TitleLabel")));
				return this.__mt_TitleLabel;
			}
			set {
				this.__mt_TitleLabel = value;
				this.SetNativeField("TitleLabel", value);
			}
		}
		
		[MonoTouch.Foundation.Connect("MsgTable")]
		private MonoTouch.UIKit.UITableView MsgTable {
			get {
				this.__mt_MsgTable = ((MonoTouch.UIKit.UITableView)(this.GetNativeField("MsgTable")));
				return this.__mt_MsgTable;
			}
			set {
				this.__mt_MsgTable = value;
				this.SetNativeField("MsgTable", value);
			}
		}
		
		[MonoTouch.Foundation.Connect("PlayBtn")]
		private LcarsComp PlayBtn {
			get {
				this.__mt_PlayBtn = ((LcarsComp)(this.GetNativeField("PlayBtn")));
				return this.__mt_PlayBtn;
			}
			set {
				this.__mt_PlayBtn = value;
				this.SetNativeField("PlayBtn", value);
			}
		}
		
		[MonoTouch.Foundation.Connect("HomeBtn")]
		private LcarsComp HomeBtn {
			get {
				this.__mt_HomeBtn = ((LcarsComp)(this.GetNativeField("HomeBtn")));
				return this.__mt_HomeBtn;
			}
			set {
				this.__mt_HomeBtn = value;
				this.SetNativeField("HomeBtn", value);
			}
		}
	}
}
