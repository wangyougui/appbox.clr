
using System;
using System.Xml;

namespace appbox.Reporting.RDL
{
	///<summary>
	/// Chart plot area style.
	///</summary>
	[Serializable]
	internal class PlotArea : ReportLink
	{
		Style _Style;	// Defines borders and background for the plot area		
	
		internal PlotArea(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p)
		{
			_Style=null;

			// Loop thru all the child nodes
			foreach(XmlNode xNodeLoop in xNode.ChildNodes)
			{
				if (xNodeLoop.NodeType != XmlNodeType.Element)
					continue;
				switch (xNodeLoop.Name)
				{
					case "Style":
						_Style = new Style(r, this, xNodeLoop);
						break;
					default:
						// don't know this element - log it
						OwnerReport.rl.LogError(4, "Unknown PlotArea element '" + xNodeLoop.Name + "' ignored.");
						break;
				}
			}
		}
		
		override internal void FinalPass()
		{
			if (_Style != null)
				_Style.FinalPass();
			return;
		}

		internal Style Style
		{
			get { return  _Style; }
			set {  _Style = value; }
		}
	}
}
