
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace appbox.Reporting.RDL
{
	///<summary>
	/// Collection of sort bys.
	///</summary>
	[Serializable]
	internal class Sorting : ReportLink
	{
        List<SortBy> _Items;			// list of SortBy

		internal Sorting(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p)
		{
			SortBy s;
            _Items = new List<SortBy>();
			// Loop thru all the child nodes
			foreach(XmlNode xNodeLoop in xNode.ChildNodes)
			{
				if (xNodeLoop.NodeType != XmlNodeType.Element)
					continue;
				switch (xNodeLoop.Name)
				{
					case "SortBy":
						s = new SortBy(r, this, xNodeLoop);
						break;
					default:	
						s=null;		// don't know what this is
						// don't know this element - log it
						OwnerReport.rl.LogError(4, "Unknown Sorting element '" + xNodeLoop.Name + "' ignored.");
						break;
				}
				if (s != null)
					_Items.Add(s);
			}
			if (_Items.Count == 0)
				OwnerReport.rl.LogError(8, "Sorting requires at least one SortBy be defined.");
			else
                _Items.TrimExcess();
		}
		
		override internal void FinalPass()
		{
			foreach (SortBy s in _Items)
			{
				s.FinalPass();
			}
			return;
		}

        internal List<SortBy> Items
		{
			get { return  _Items; }
		}
	}
}
