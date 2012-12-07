﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Windows.Forms;
using System.Resources;
using System.Drawing;
using System.ComponentModel;

namespace VisualLocalizer.Library {

    public abstract class AbstractCheckedGridView<ItemType> : DataGridView where ItemType : class {
                
        public event EventHandler HasErrorChanged;
        public int CheckedRowsCount { get; protected set; }   

        protected ToolTip ErrorToolTip;
        protected DataGridViewCheckBoxHeaderCell CheckHeader;             
        protected Timer ErrorTimer;
        protected bool ErrorToolTipVisible;                
        protected Color ErrorColor = Color.FromArgb(255, 213, 213);
        protected Color ExistingKeySameValueColor = Color.FromArgb(213, 255, 213);
        protected HashSet<DataGridViewRow> errorRows = new HashSet<DataGridViewRow>();
        protected DataGridViewRow previouslySelectedRow = null;
        protected List<DataGridViewRow> removedRows;

        public AbstractCheckedGridView(bool showContextColumn) {
            this.EnableHeadersVisualStyles = true;
            this.AutoGenerateColumns = false;
            this.AllowUserToAddRows = false;
            this.AllowUserToDeleteRows = false;
            this.AutoSize = true;
            this.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            this.MultiSelect = false;
            this.Dock = DockStyle.Fill;
            this.AllowUserToResizeRows = true;
            this.AllowUserToResizeColumns = true;
            this.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.ScrollBars = ScrollBars.Both;
            this.ShowContextColumn = showContextColumn;

            this.MouseMove += new MouseEventHandler(RowHeaderMouseMove);
            this.SelectionChanged += new EventHandler(RowSelectionChanged);

            this.removedRows = new List<DataGridViewRow>();

            ErrorToolTip = new ToolTip();
            ErrorToolTip.InitialDelay = 0;
            ErrorToolTip.AutoPopDelay = 0;
            ErrorToolTip.ShowAlways = true;
            ErrorToolTip.ReshowDelay = 0;

            ErrorTimer = new Timer();
            ErrorTimer.Interval = 1000;
            ErrorTimer.Tick += new EventHandler(errorTimer_Tick);

            CheckHeader = new DataGridViewCheckBoxHeaderCell();
            CheckHeader.ThreeStates = true;
            CheckHeader.Checked = true;
            CheckHeader.CheckBoxClicked += new EventHandler(OnCheckHeaderClicked);
            CheckHeader.Sort += new Action<SortOrder>(CheckHeader_Sort);

            CheckedRowsCount = 0;
            
            InitializeColumns();
        }        
        
        #region public members
        
        public bool HasError {
            get {
                return errorRows.Count > 0;
            }
        }
       
        public virtual List<ItemType> GetData() {
            List<ItemType> list = new List<ItemType>(Rows.Count);

            foreach (DataGridViewRow row in Rows)
                list.Add(GetResultItemFromRow(row));

            return list;
        }

        public virtual void RemoveUncheckedRows(bool remember) {
            if (string.IsNullOrEmpty(CheckBoxColumnName) || !Columns.Contains(CheckBoxColumnName)) return;

            List<DataGridViewRow> rowsToRemove = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in Rows) {
                bool check = (bool)row.Cells[CheckBoxColumnName].Value;
                if (!check) {
                    rowsToRemove.Add(row);
                    if (remember) removedRows.Add(row);
                }
            }

            foreach (DataGridViewRow row in rowsToRemove) {
                row.ErrorText = null;
                Rows.Remove(row);
            }

            UpdateCheckHeader();
        }

        public virtual void RestoreRemovedRows() {
            if (string.IsNullOrEmpty(CheckBoxColumnName) || !Columns.Contains(CheckBoxColumnName)) return;

            foreach (DataGridViewRow row in removedRows) {
                Rows.Add(row);
            }

            removedRows.Clear();
            UpdateCheckHeader();
        }

        public abstract void SetData(List<ItemType> list);
        public abstract string CheckBoxColumnName { get; }

        #endregion 

        #region overridable members
        protected virtual void InitializeColumns() {
            DataGridViewCheckBoxColumn checkColumn = new DataGridViewCheckBoxColumn(false);
            checkColumn.MinimumWidth = 50;
            checkColumn.Width = 50;
            checkColumn.Name = CheckBoxColumnName;
            checkColumn.HeaderCell = CheckHeader;
            checkColumn.ToolTipText = null;
            checkColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            checkColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            checkColumn.DefaultCellStyle.Padding = new Padding(4, 0, 0, 0);
            checkColumn.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            this.Columns.Add(checkColumn);

            DataGridViewTextBoxColumn lineColumn = new DataGridViewTextBoxColumn();
            lineColumn.MinimumWidth = 40;
            lineColumn.Width = 55;
            lineColumn.HeaderText = "Line";
            lineColumn.Name = LineColumnName;
            lineColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            lineColumn.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            this.Columns.Add(lineColumn);

            DataGridViewTextBoxColumn contextColumn = new DataGridViewTextBoxColumn();
            contextColumn.MinimumWidth = 40;
            contextColumn.Width = 350;
            contextColumn.HeaderText = "Context";
            contextColumn.Name = ContextColumnName;
            contextColumn.Visible = ShowContextColumn;
            this.Columns.Add(contextColumn);

        }

        protected virtual void RowSelectionChanged(object sender, EventArgs e) {
            if (!Columns.Contains(ContextColumnName) || !Columns[ContextColumnName].Visible) return;

            if (previouslySelectedRow != null) {
                DataGridViewDynamicWrapCell cell = previouslySelectedRow.Cells[ContextColumnName] as DataGridViewDynamicWrapCell;
                cell.SetWrapContents(false);
            }

            DataGridViewRow row = null;
            if (SelectedRows.Count == 1)
                row = SelectedRows[0];

            if (row != null) {
                DataGridViewDynamicWrapCell cell = row.Cells[ContextColumnName] as DataGridViewDynamicWrapCell;
                cell.SetWrapContents(true);
            }

            previouslySelectedRow = row;
        }

        protected virtual void OnCheckHeaderClicked(object sender, EventArgs e) {
            EndEdit();
            errorRows.Clear();            
            foreach (DataGridViewRow row in Rows) {                 
                row.Cells[CheckBoxColumnName].Value = CheckHeader.Checked == true;
                row.Cells[CheckBoxColumnName].Tag = CheckHeader.Checked == true;                

                if (!string.IsNullOrEmpty(row.ErrorText) && CheckHeader.Checked == true) errorRows.Add(row);
            }
            
            RefreshEdit();
            CheckedRowsCount = CheckHeader.Checked == true ? Rows.Count : 0;
            NotifyErrorRowsChanged();
        }

        protected override void OnSorted(EventArgs e) {
            base.OnSorted(e);
            if (this.SortedColumn.Name != CheckBoxColumnName && !string.IsNullOrEmpty(CheckBoxColumnName)) {
                CheckHeader.SortGlyphDirection = SortOrder.None;
            }
        }        

        protected virtual void CheckHeader_Sort(SortOrder direction) {
            this.Sort(Columns[CheckBoxColumnName], direction == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
        }

        protected override void OnCellBeginEdit(DataGridViewCellCancelEventArgs e) {
            base.OnCellBeginEdit(e);

            DataGridViewCell cell = (Rows[e.RowIndex].Cells[e.ColumnIndex] as DataGridViewCell);
            cell.Tag = cell.Value;
        }

        protected override void OnCellEndEdit(DataGridViewCellEventArgs e) {
            base.OnCellEndEdit(e);

            if (this.Columns[e.ColumnIndex].Name == CheckBoxColumnName && CheckBoxColumnName!=null) {
                DataGridViewCheckBoxCell cell = (DataGridViewCheckBoxCell)Rows[e.RowIndex].Cells[CheckBoxColumnName];
                DataGridViewRow row = Rows[e.RowIndex];
                if (cell.Value == null || cell.Tag == null) return;

                bool value=(bool)cell.Value;

                if (value != (bool)cell.Tag) {
                    CheckedRowsCount += value ? 1 : -1;
                    if (!value && !string.IsNullOrEmpty(row.ErrorText)) {
                        errorRows.Remove(row);
                    }
                    if (value && !string.IsNullOrEmpty(row.ErrorText)) {
                        errorRows.Add(row);
                    }
                    NotifyErrorRowsChanged();
                }

                UpdateCheckHeader();
            }           
        }

        protected override void OnRowErrorTextChanged(DataGridViewRowEventArgs e) {
            base.OnRowErrorTextChanged(e);
            if (CheckBoxColumnName==null || (bool)e.Row.Cells[CheckBoxColumnName].Value) {
                if (string.IsNullOrEmpty(e.Row.ErrorText)) {
                    errorRows.Remove(e.Row);
                } else {
                    errorRows.Add(e.Row);
                }
                NotifyErrorRowsChanged();
            }

            if (!string.IsNullOrEmpty(e.Row.ErrorText)) {
                if (e.Row.DefaultCellStyle.BackColor != ErrorColor) 
                    e.Row.DefaultCellStyle.Tag = e.Row.DefaultCellStyle.BackColor;
                e.Row.DefaultCellStyle.BackColor = ErrorColor;
            } else {
                e.Row.DefaultCellStyle.BackColor = e.Row.DefaultCellStyle.Tag == null ? Color.White : (Color)e.Row.DefaultCellStyle.Tag;
            }
            this.UpdateRowErrorText(e.Row.Index);            
        }

        #endregion                

        protected void UpdateCheckHeader() {
            if (CheckedRowsCount == Rows.Count) {
                CheckHeader.Checked = true;
            } else if (CheckedRowsCount == 0) {
                CheckHeader.Checked = false;
            } else {
                CheckHeader.Checked = null;
            }
        }

        protected void setCheckStateOfSelected(bool p) {
            if (string.IsNullOrEmpty(CheckBoxColumnName)) return;

            foreach (DataGridViewRow row in SelectedRows) {
                row.Cells[CheckBoxColumnName].Tag = row.Cells[CheckBoxColumnName].Value;
                row.Cells[CheckBoxColumnName].Value = p;
                OnCellEndEdit(new DataGridViewCellEventArgs(Columns[CheckBoxColumnName].Index, row.Index));
            }
            UpdateCheckHeader();
        }

        protected void OnContextMenuShow(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Right) {
                HitTestInfo hitTest = this.HitTest(e.X, e.Y);
                if (hitTest != null && hitTest.Type == DataGridViewHitTestType.Cell && hitTest.RowIndex >= 0 && hitTest.ColumnIndex >= 0) {
                    bool isRowSelected = Rows[hitTest.RowIndex].Selected;
                    if (!isRowSelected) {
                        this.ClearSelection();
                        Rows[hitTest.RowIndex].Selected = true;
                    }

                    this.ContextMenu.Show(this, e.Location);
                }
            }
        }

        protected abstract ItemType GetResultItemFromRow(DataGridViewRow row);

        private void errorTimer_Tick(object sender, EventArgs e) {
            ErrorToolTipVisible = false;
            ErrorTimer.Stop();
        }

        protected void NotifyErrorRowsChanged() {
            if (HasErrorChanged != null) HasErrorChanged(this, null);
        }

        private void RowHeaderMouseMove(object sender, MouseEventArgs e) {
            HitTestInfo info = this.HitTest(e.X, e.Y);
            if (info != null && info.Type == DataGridViewHitTestType.RowHeader && info.RowIndex >= 0) {
                if (!string.IsNullOrEmpty(Rows[info.RowIndex].ErrorText) && !ErrorToolTipVisible) {
                    ErrorToolTip.Show(Rows[info.RowIndex].ErrorText, this, e.X, e.Y, 1000);                    
                    ErrorToolTipVisible = true;
                    ErrorTimer.Start();
                }
            }
        }

        public string LineColumnName {
            get { return "Line"; }
        }

        public string ContextColumnName {
            get { return "Context"; }
        }

        public bool ShowContextColumn {
            get;
            private set;
        }
    }

   
}
