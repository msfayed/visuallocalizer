﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Windows.Forms.VisualStyles;

namespace VisualLocalizer.Library {
    public class DataGridViewCheckBoxHeaderCell : DataGridViewColumnHeaderCell {

        public event EventHandler CheckBoxClicked;
        public event Action<SortOrder> Sort;

        private CheckBoxState CheckBoxState { get; set; }

        public DataGridViewCheckBoxHeaderCell() {
            ToolTipText = null;            
        }

        public Point CheckBoxPosition {
            get;
            private set;
        }

        public Size CheckBoxSize {
            get;
            private set;
        }

        private bool? _Checked;
        public bool? Checked {
            get {
                return _Checked;
            }
            set {
                _Checked = value;
                ChangeValue();
            }
        }

        public bool ThreeStates { get; set; }

        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
            DataGridViewElementStates dataGridViewElementState, object value, object formattedValue, string errorText,
            DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts) {            
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, dataGridViewElementState, value, null, errorText, cellStyle, advancedBorderStyle, paintParts);            
            
            CheckBoxSize = CheckBoxRenderer.GetGlyphSize(graphics, CheckBoxState);
            Point position = new Point(cellBounds.X + CheckBoxSize.Width / 2, cellBounds.Y + (cellBounds.Height - CheckBoxSize.Height) / 2);
            CheckBoxPosition = new Point(position.X - clipBounds.X, position.Y - clipBounds.Y);
            CheckBoxRenderer.DrawCheckBox(graphics, position, CheckBoxState);
        }        

        protected override void OnMouseUp(DataGridViewCellMouseEventArgs e) {
            base.OnMouseUp(e);

            if (e.X >= CheckBoxPosition.X && e.X <= CheckBoxPosition.X + CheckBoxSize.Width
                && e.Y >= CheckBoxPosition.Y && e.Y <= CheckBoxPosition.Y + CheckBoxSize.Height) {
                if (Checked == true) {
                    Checked = false;
                } else {
                    Checked = true;
                }
                NotifyCheckBoxClicked();                
            } else if (ContentBounds.Contains(e.Location)) {
                switch (SortGlyphDirection) {
                    case SortOrder.Ascending:
                        SortGlyphDirection = SortOrder.Descending;
                        break;
                    case SortOrder.Descending:
                        SortGlyphDirection = SortOrder.Ascending;
                        break;
                    case SortOrder.None:
                        SortGlyphDirection = SortOrder.Descending;
                        break;                 
                }
                NotifySortClicked(SortGlyphDirection);
            }
        }        

        protected void NotifyCheckBoxClicked() {
            if (CheckBoxClicked != null) {
                CheckBoxClicked(this, new EventArgs());
            }
        }

        protected void NotifySortClicked(SortOrder order) {
            if (Sort != null) {
                Sort(order);
            }
        }

        protected virtual void ChangeValue() {
            if (Checked == true) {
                CheckBoxState = CheckBoxState.CheckedNormal;
            } else if (Checked == null) {
                CheckBoxState = CheckBoxState.MixedNormal;
            } else {
                CheckBoxState = CheckBoxState.UncheckedNormal;
            }
            this.RaiseCellValueChanged(new DataGridViewCellEventArgs(this.ColumnIndex, this.RowIndex));
        }
    }
}
