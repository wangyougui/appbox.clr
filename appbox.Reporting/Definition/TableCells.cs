using System;
using System.Collections.Generic;
using System.Xml;

namespace appbox.Reporting.RDL
{
    ///<summary>
    /// TableCells definition and processing.
    ///</summary>
    [Serializable]
    internal class TableCells : ReportLink
    {
        /// <summary>
        /// list of TableCell
        /// </summary>
        internal List<TableCell> Items { get; }

        internal TableCells(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p)
        {
            TableCell tc;
            Items = new List<TableCell>();
            // Loop thru all the child nodes
            int colIndex = 0;           // keep track of the column numbers
            foreach (XmlNode xNodeLoop in xNode.ChildNodes)
            {
                if (xNodeLoop.NodeType != XmlNodeType.Element)
                    continue;
                switch (xNodeLoop.Name)
                {
                    case "TableCell":
                        tc = new TableCell(r, this, xNodeLoop, colIndex);
                        colIndex += tc.ColSpan;
                        break;
                    default:
                        tc = null;      // don't know what this is
                                        // don't know this element - log it
                        OwnerReport.rl.LogError(4, "Unknown TableCells element '" + xNodeLoop.Name + "' ignored.");
                        break;
                }
                if (tc != null)
                    Items.Add(tc);
            }
            if (Items.Count > 0)
                Items.TrimExcess();
        }

        override internal void FinalPass()
        {
            foreach (TableCell tc in Items)
            {
                tc.FinalPass();
            }
            return;
        }

        internal void Run(IPresent ip, Row row)
        {
            foreach (TableCell tc in Items)
            {
                tc.Run(ip, row);
            }
            return;
        }

        internal void RunPage(Pages pgs, Row row)
        {
            // Start each row in the same location
            //   e.g. if there are two embedded tables in cells they both start at same location
            Page savepg = pgs.CurrentPage;
            float savey = savepg.YOffset;
            Page maxpg = savepg;
            float maxy = savey;

            foreach (TableCell tc in Items)
            {
                tc.RunPage(pgs, row);
                if (maxpg != pgs.CurrentPage)
                {   // a page break
                    if (maxpg.PageNumber < pgs.CurrentPage.PageNumber)
                    {
                        maxpg = pgs.CurrentPage;
                        maxy = maxpg.YOffset;
                    }
                }
                else if (maxy > pgs.CurrentPage.YOffset)
                {
                    // maxy = maxy;      TODO what was this meant to do
                }
                // restore the beginning start of the row
                pgs.CurrentPage = savepg;
                savepg.YOffset = savey;
            }
            pgs.CurrentPage = maxpg;
            savepg.YOffset = maxy;
            return;
        }

    }
}
