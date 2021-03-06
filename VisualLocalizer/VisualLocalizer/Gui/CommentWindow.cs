﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VisualLocalizer.Gui {

    /// <summary>
    /// Dialog enabling user to modify resource comments in ResX editor
    /// </summary>
    public partial class CommentWindow : Form {

        /// <summary>
        /// Creates new instance
        /// </summary>
        /// <param name="oldComment">Current comment, displayed as initial text</param>
        public CommentWindow(string oldComment) {
            InitializeComponent();
            this.Icon = VSPackage._400;

            commentBox.Text = oldComment;
        }

        /// <summary>
        /// Comment set in the dialog
        /// </summary>
        public string Comment {
            get;
            private set;
        }

        /// <summary>
        /// Initializes the comment from the value in the box
        /// </summary>        
        private void CommentWindow_FormClosing(object sender, FormClosingEventArgs e) {
            Comment = commentBox.Text;
        }

        /// <summary>
        /// Handle CTRL+Enter and Escape closing events
        /// </summary>
        private bool ctrlDown = false;
        private void CommentWindow_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Escape) {
                e.Handled = true;
                cancelButton.PerformClick();
            }

            if ((e.KeyCode == Keys.Enter) && ctrlDown) {
                e.Handled = true;
                okButton.PerformClick();
            }

            if (e.KeyCode == Keys.ControlKey) ctrlDown = true;
        }


        private void CommentWindow_KeyUp(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.ControlKey) ctrlDown = false;
        }
    }
}
