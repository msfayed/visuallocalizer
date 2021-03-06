﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Resources;
using VisualLocalizer.Library;
using System.Drawing;
using System.IO;
using VisualLocalizer.Library.Components;
using VisualLocalizer.Library.Extensions;

namespace VisualLocalizer.Editor {

    /// <summary>
    /// Represents Icons tab in ResX editor. Can contain any Icon resource.
    /// </summary>
    internal sealed class ResXIconsList : AbstractListView {

        public ResXIconsList(ResXEditorControl editorControl) : base(editorControl) {
        }


        /// <summary>
        /// Returns true if given node's type matches the type of items this control holds
        /// </summary>  
        public override bool CanContainItem(ResXDataNode node) {
            if (node == null) throw new ArgumentNullException("node");
            return node.HasValue<Icon>(); // only Icons are allowed
        }

        /// <summary>
        /// Adds the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public override IKeyValueSource Add(string key, ResXDataNode value) {
            ListViewKeyItem item = base.Add(key, value) as ListViewKeyItem;
            if (referenceExistingOnAdd) {
                item.FileRefOk = true;                
            } else {
                ListViewItem.ListViewSubItem subSize = new ListViewItem.ListViewSubItem();
                subSize.Name = "Size";
                item.SubItems.Insert(2, subSize);
            }

            UpdateDataOf(item, false);

            return item;
        }

        /// <summary>
        /// Reloads displayed data from underlaying ResX node
        /// </summary>
        public override void UpdateDataOf(ListViewKeyItem item, bool reloadImages) {
            base.UpdateDataOf(item, reloadImages);            

            Icon ico = item.DataNode.GetValue<Icon>();
            if (LargeImageList.Images.ContainsKey(item.ImageKey) && reloadImages) {
                LargeImageList.Images.RemoveByKey(item.ImageKey);
                SmallImageList.Images.RemoveByKey(item.ImageKey);
            }

            // add the new image, if exists
            if (ico != null) {
                LargeImageList.Images.Add(item.ImageKey, ico);
                SmallImageList.Images.Add(item.ImageKey, ico);

                item.SubItems["Size"].Text = string.Format("{0} x {1}", ico.Width, ico.Height);
                item.FileRefOk = true;
            } else {
                item.SubItems["Size"].Text = null;
                item.FileRefOk = false;
            }
            
            item.UpdateErrorSetDisplay();            

            Validate(item);
            NotifyItemsStateChanged();

            if (item.ErrorMessages.Count > 0) {
                item.Status = KEY_STATUS.ERROR;
            } else {
                item.Status = KEY_STATUS.OK;
                item.LastValidKey = item.Key;
            }

            // update image display
            string p = item.ImageKey;
            item.ImageKey = null;
            item.ImageKey = p;
        }

        /// <summary>
        /// Create the GUI
        /// </summary>
        protected override void InitializeColumns() {
            base.InitializeColumns();

            ColumnHeader sizeHeader = new ColumnHeader();
            sizeHeader.Text = "Icon Size";
            sizeHeader.Width = 80;
            sizeHeader.Name = "Size";
            this.Columns.Insert(2, sizeHeader);            
        }

        /// <summary>
        /// Saves given node's content into random file in specified directory and returns the file path
        /// </summary>    
        protected override string SaveIntoTmpFile(ResXDataNode node, string name, string directory) {
            Icon value = node.GetValue<Icon>();
            string filename = name + ".ico";
            string path = Path.Combine(directory, filename);

            FileStream fs = null;
            try {
                fs = new FileStream(path, FileMode.Create);
                value.Save(fs);
            } finally {
                if (fs != null) fs.Close();         
            }

            return path;
        }
    }
}
