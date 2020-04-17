using System;
using System.Xml;
using System.Collections.Generic;
using appbox.Drawing;

namespace appbox.Reporting.RDL
{
    ///<summary>
    /// Represents the report item for a List (i.e. absolute positioning)
    ///</summary>
    [Serializable]
    internal class List : DataRegion
    {
        /// <summary>
        /// The expressions to group the data by Required if there are any DataRegions
        /// contained within this List
        /// </summary>
        internal Grouping Grouping { get; set; }

        /// <summary>
        /// The expressions to sort the repeated list regions by
        /// </summary>
        internal Sorting Sorting { get; set; }

        /// <summary>
        /// The elements of the list layout
        /// </summary>
        internal ReportItems ReportItems { get; set; }

        /// <summary>
        /// Indicates whether the list instances should
        /// appear in a data rendering. Ignored if there is
        /// a grouping for the list.  Default: output
        /// </summary>
        internal DataInstanceElementOutputEnum DataInstanceElementOutput { get; set; }

        private string _DataInstanceName;
        /// <summary>
        /// The name to use for the data element for the
        /// </summary>
        internal string DataInstanceName
        {
            get { return _DataInstanceName ?? "Item"; }
            set { _DataInstanceName = value; }
        }

        private bool _CanGrow;          // indicates that row height can increase in size
        private List<Textbox> _GrowList;    // list of TextBox's that need to be checked for growth

        internal List(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p, xNode)
        {
            Grouping = null;
            Sorting = null;
            ReportItems = null;
            _DataInstanceName = "Item";
            DataInstanceElementOutput = DataInstanceElementOutputEnum.Output;

            // Loop thru all the child nodes
            foreach (XmlNode xNodeLoop in xNode.ChildNodes)
            {
                if (xNodeLoop.NodeType != XmlNodeType.Element)
                    continue;
                switch (xNodeLoop.Name)
                {
                    case "Grouping":
                        Grouping = new Grouping(r, this, xNodeLoop);
                        break;
                    case "Sorting":
                        Sorting = new Sorting(r, this, xNodeLoop);
                        break;
                    case "ReportItems":
                        ReportItems = new ReportItems(r, this, xNodeLoop);
                        break;
                    case "DataInstanceName":
                        _DataInstanceName = xNodeLoop.InnerText;
                        break;
                    case "DataInstanceElementOutput":
                        DataInstanceElementOutput = RDL.DataInstanceElementOutput.GetStyle(xNodeLoop.InnerText, OwnerReport.rl);
                        break;
                    default:
                        if (DataRegionElement(xNodeLoop))   // try at DataRegion level
                            break;
                        // don't know this element - log it
                        OwnerReport.rl.LogError(4, "Unknown List element '" + xNodeLoop.Name + "' ignored.");
                        break;
                }
            }
            DataRegionFinish();         // Tidy up the DataRegion
        }

        override internal void FinalPass()
        {
            base.FinalPass();

            if (Grouping != null)
                Grouping.FinalPass();
            if (Sorting != null)
                Sorting.FinalPass();
            if (ReportItems != null)
                ReportItems.FinalPass();

            // determine if the size is dynamic depending on any of its
            //   contained textbox have cangrow true
            if (ReportItems == null)    // no report items in List region
                return;

            foreach (ReportItem ri in this.ReportItems.Items)
            {
                if (!(ri is Textbox))
                    continue;
                Textbox tb = ri as Textbox;
                if (tb.CanGrow)
                {
                    if (this._GrowList == null)
                        _GrowList = new List<Textbox>();
                    _GrowList.Add(tb);
                    _CanGrow = true;
                }
            }

            if (_CanGrow)				// shrink down the resulting list
                _GrowList.TrimExcess();

            return;
        }

        override internal void Run(IPresent ip, Row row)
        {
            Report r = ip.Report();
            WorkClass wc = GetValue(r);

            wc.Data = GetFilteredData(r, row);

            if (!AnyRows(ip, wc.Data))      // if no rows return
                return;                 //   nothing left to do

            RunSetGrouping(r, wc);

            base.Run(ip, row);

            if (!ip.ListStart(this, row))
                return;                         // renderer doesn't want to continue

            RunGroups(ip, wc, wc.Groups);

            ip.ListEnd(this, row);
            RemoveValue(r);
        }

        override internal void RunPage(Pages pgs, Row row)
        {
            Report r = pgs.Report;
            if (IsHidden(r, row))
                return;

            WorkClass wc = GetValue(r);
            wc.Data = GetFilteredData(r, row);

            SetPagePositionBegin(pgs);

            if (!AnyRowsPage(pgs, wc.Data))     // if no rows return
                return;                     //   nothing left to do

            RunPageRegionBegin(pgs);

            RunSetGrouping(pgs.Report, wc);

            RunPageGroups(pgs, wc, wc.Groups);

            RunPageRegionEnd(pgs);
            SetPagePositionEnd(pgs, pgs.CurrentPage.YOffset);
            RemoveValue(r);
        }

        private void RunSetGrouping(Report rpt, WorkClass wc)
        {
            GroupEntry[] currentGroups;

            // We have some data
            if (Grouping != null ||
                Sorting != null)        // fix up the data
            {
                Rows saveData = wc.Data;
                wc.Data = new Rows(rpt, null, Grouping, Sorting);
                wc.Data.Data = saveData.Data;
                wc.Data.Sort();
                PrepGroups(rpt, wc);
            }
            // If we haven't formed any groups then form one with all rows
            if (wc.Groups == null)
            {
                wc.Groups = new List<GroupEntry>();
                GroupEntry ge = new GroupEntry(null, null, 0);
                if (wc.Data.Data != null)       // Do we have any data?
                    ge.EndRow = wc.Data.Data.Count - 1; // yes
                else
                    ge.EndRow = -1;                 // no
                wc.Groups.Add(ge);          // top group
                currentGroups = new GroupEntry[1];
            }
            else
                currentGroups = new GroupEntry[1];

            wc.Data.CurrentGroups = currentGroups;

            return;
        }

        private void PrepGroups(Report rpt, WorkClass wc)
        {
            if (Grouping == null)
                return;

            int i = 0;
            // 1) Build array of all GroupExpression objects
            List<GroupExpression> gea = Grouping.GroupExpressions.Items;
            GroupEntry[] currentGroups = new GroupEntry[1];
            Grouping.SetIndex(rpt, 0);  // set the index of this group (so we can find the GroupEntry)
            currentGroups[0] = new GroupEntry(Grouping, Sorting, 0);

            // Save the typecodes, and grouping by groupexpression; for later use
            TypeCode[] tcs = new TypeCode[gea.Count];
            Grouping[] grp = new Grouping[gea.Count];
            i = 0;
            foreach (GroupExpression ge in gea)
            {
                grp[i] = (Grouping)(ge.Parent.Parent);          // remember the group
                tcs[i++] = ge.Expression.GetTypeCode(); // remember type of expression
            }

            // 2) Loop thru the data, then loop thru the GroupExpression list
            wc.Groups = new List<GroupEntry>();
            object[] savValues = null;
            object[] grpValues = null;
            int rowCurrent = 0;

            foreach (Row row in wc.Data.Data)
            {
                // Get the values for all the group expressions
                if (grpValues == null)
                    grpValues = new object[gea.Count];

                i = 0;
                foreach (GroupExpression ge in gea)  // Could optimize to only calculate as needed in comparison loop below??
                {
                    grpValues[i++] = ge.Expression.Evaluate(rpt, row);
                }

                // For first row we just primed the pump; action starts on next row
                if (rowCurrent == 0)            // always start new group on first row
                {
                    rowCurrent++;
                    savValues = grpValues;
                    grpValues = null;
                    continue;
                }

                // compare the values; if change then we have a group break
                for (i = 0; i < savValues.Length; i++)
                {
                    if (Filter.ApplyCompare(tcs[i], savValues[i], grpValues[i]) != 0)
                    {
                        // start a new group; and force a break on every subgroup
                        GroupEntry saveGe = null;
                        for (int j = grp[i].GetIndex(rpt); j < currentGroups.Length; j++)
                        {
                            currentGroups[j].EndRow = rowCurrent - 1;
                            if (j == 0)
                                wc.Groups.Add(currentGroups[j]);        // top group
                            else if (saveGe == null)
                                currentGroups[j - 1].NestedGroup.Add(currentGroups[j]);
                            else
                                saveGe.NestedGroup.Add(currentGroups[j]);

                            saveGe = currentGroups[j];  // retain this GroupEntry
                            currentGroups[j] = new GroupEntry(currentGroups[j].Group, currentGroups[j].Sort, rowCurrent);
                        }
                        savValues = grpValues;
                        grpValues = null;
                        break;      // break out of the value comparison loop
                    }
                }
                rowCurrent++;
            }

            // End of all rows force break on end of rows
            for (i = 0; i < currentGroups.Length; i++)
            {
                currentGroups[i].EndRow = rowCurrent - 1;
                if (i == 0)
                    wc.Groups.Add(currentGroups[i]);        // top group
                else
                    currentGroups[i - 1].NestedGroup.Add(currentGroups[i]);
            }
            return;
        }

        private void RunGroups(IPresent ip, WorkClass wc, List<GroupEntry> groupEntries)
        {
            foreach (GroupEntry ge in groupEntries)
            {
                // set the group entry value
                int index;
                if (ge.Group != null)   // groups?
                {
                    ge.Group.ResetHideDuplicates(ip.Report());  // reset duplicate checking
                    index = ge.Group.GetIndex(ip.Report()); // yes
                }
                else                    // no; must be main dataset
                    index = 0;
                wc.Data.CurrentGroups[index] = ge;
                if (ge.NestedGroup.Count > 0)
                    RunGroupsSetGroups(ip.Report(), wc, ge.NestedGroup);

                if (ge.Group == null)
                {   // need to run all the rows since no group defined
                    for (int r = ge.StartRow; r <= ge.EndRow; r++)
                    {
                        ip.ListEntryBegin(this, wc.Data.Data[r]);
                        if (ReportItems != null)
                            ReportItems.Run(ip, wc.Data.Data[r]);
                        ip.ListEntryEnd(this, wc.Data.Data[r]);
                    }
                }
                else
                {   // need to process just whole group as a List entry
                    ip.ListEntryBegin(this, wc.Data.Data[ge.StartRow]);

                    // pass the first row of the group
                    if (ReportItems != null)
                        ReportItems.Run(ip, wc.Data.Data[ge.StartRow]);

                    ip.ListEntryEnd(this, wc.Data.Data[ge.StartRow]);
                }
            }
        }

        private void RunPageGroups(Pages pgs, WorkClass wc, List<GroupEntry> groupEntries)
        {
            Report rpt = pgs.Report;
            Page p = pgs.CurrentPage;
            float pagebottom = OwnerReport.BottomOfPage;
            //			p.YOffset += (Top == null? 0: this.Top.Points);
            p.YOffset += this.RelativeY(rpt);
            float listoffset = GetOffsetCalc(pgs.Report) + LeftCalc(pgs.Report);

            float height;
            Row row;

            foreach (GroupEntry ge in groupEntries)
            {
                // set the group entry value
                int index;
                if (ge.Group != null)   // groups?
                {
                    ge.Group.ResetHideDuplicates(rpt);  // reset duplicate checking
                    index = ge.Group.GetIndex(rpt); // yes
                }
                else                    // no; must be main dataset
                    index = 0;
                wc.Data.CurrentGroups[index] = ge;
                if (ge.NestedGroup.Count > 0)
                    RunGroupsSetGroups(rpt, wc, ge.NestedGroup);

                if (ge.Group == null)
                {   // need to run all the rows since no group defined
                    for (int r = ge.StartRow; r <= ge.EndRow; r++)
                    {
                        row = wc.Data.Data[r];
                        height = HeightOfList(rpt, pgs.G, row);

                        if (p.YOffset + height > pagebottom && !p.IsEmpty())		// need another page for this row?
                            p = RunPageNew(pgs, p);					// yes; if at end this page is empty

                        float saveYoffset = p.YOffset;              // this can be affected by other page items

                        if (ReportItems != null)
                            ReportItems.RunPage(pgs, row, listoffset);

                        if (p == pgs.CurrentPage)       // did subitems force new page?
                        {   // no use the height of the list
                            p.YOffset = saveYoffset + height;
                        }
                        else
                        {   // got forced to new page; just add the padding on
                            p = pgs.CurrentPage;        // set to new page
                            if (this.Style != null)
                            {
                                p.YOffset += this.Style.EvalPaddingBottom(rpt, row);
                            }
                        }
                    }
                }
                else
                {   // need to process just whole group as a List entry
                    if (ge.Group.PageBreakAtStart && !p.IsEmpty())
                        p = RunPageNew(pgs, p);

                    // pass the first row of the group
                    row = wc.Data.Data[ge.StartRow];
                    height = HeightOfList(rpt, pgs.G, row);

                    if (p.YOffset + height > pagebottom && !p.IsEmpty())		// need another page for this row?
                        p = RunPageNew(pgs, p);					// yes; if at end this page is empty
                    float saveYoffset = p.YOffset;              // this can be affected by other page items

                    if (ReportItems != null)
                        ReportItems.RunPage(pgs, row, listoffset);


                    if (p == pgs.CurrentPage)       // did subitems force new page?
                    {   // no use the height of the list
                        p.YOffset = saveYoffset + height;
                    }
                    else
                    {   // got forced to new page; just add the padding on
                        p = pgs.CurrentPage;        // set to new page
                        if (this.Style != null)
                        {
                            p.YOffset += this.Style.EvalPaddingBottom(rpt, row);
                        }
                    }

                    if (ge.Group.PageBreakAtEnd ||                  // need another page for next group?
                        p.YOffset + height > pagebottom)
                    {
                        p = RunPageNew(pgs, p);                     // yes; if at end empty page will be cleaned up later
                    }
                }
                RemoveWC(rpt);
            }
        }

        internal override void RemoveWC(Report rpt)
        {
            base.RemoveWC(rpt);

            if (this.ReportItems == null)
                return;

            foreach (ReportItem ri in this.ReportItems.Items)
            {
                ri.RemoveWC(rpt);
            }
        }

        private void RunGroupsSetGroups(Report rpt, WorkClass wc, List<GroupEntry> groupEntries)
        {
            // set the group entry value
            GroupEntry ge = groupEntries[0];
            wc.Data.CurrentGroups[ge.Group.GetIndex(rpt)] = ge;

            if (ge.NestedGroup.Count > 0)
                RunGroupsSetGroups(rpt, wc, ge.NestedGroup);
        }

        internal float HeightOfList(Report rpt, Graphics g, Row r)
        {
            WorkClass wc = GetValue(rpt);

            float defnHeight = this.HeightOrOwnerHeight;
            if (!_CanGrow)
                return defnHeight;

            float height;
            foreach (Textbox tb in this._GrowList)
            {
                float top = (float)(tb.Top == null ? 0.0 : tb.Top.Points);
                height = top + tb.RunTextCalcHeight(rpt, g, r);
                if (tb.Style != null)
                    height += (tb.Style.EvalPaddingBottom(rpt, r) + tb.Style.EvalPaddingTop(rpt, r));
                defnHeight = Math.Max(height, defnHeight);
            }
            wc.CalcHeight = defnHeight;
            return defnHeight;
        }

        private WorkClass GetValue(Report rpt)
        {
            WorkClass wc = rpt.Cache.Get(this, "wc") as WorkClass;
            if (wc == null)
            {
                wc = new WorkClass(this);
                rpt.Cache.Add(this, "wc", wc);
            }
            return wc;
        }

        private void RemoveValue(Report rpt)
        {
            rpt.Cache.Remove(this, "wc");
        }

        class WorkClass
        {
            internal float CalcHeight;      // dynamic when CanGrow true
            internal Rows Data; // Runtime data; either original query if no groups
                                // or sorting or a copied version that is grouped/sorted
            internal List<GroupEntry> Groups;           // Runtime groups; array of GroupEntry
            internal WorkClass(List l)
            {
                CalcHeight = l.Height == null ? 0 : l.Height.Points;
                Data = null;
                Groups = null;
            }
        }
    }
}