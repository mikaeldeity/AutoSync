namespace AutoSync
{
    partial class DismissDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.DismissButton = new System.Windows.Forms.Button();
            this.dialogtimer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(241, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Inactive Documents will be processed now.";
            // 
            // DismissButton
            // 
            this.DismissButton.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.DismissButton.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.DismissButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.DismissButton.FlatAppearance.BorderSize = 0;
            this.DismissButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DismissButton.Location = new System.Drawing.Point(14, 49);
            this.DismissButton.Margin = new System.Windows.Forms.Padding(5);
            this.DismissButton.Name = "DismissButton";
            this.DismissButton.Size = new System.Drawing.Size(237, 51);
            this.DismissButton.TabIndex = 1;
            this.DismissButton.Text = "Dismiss for 10 minutes";
            this.DismissButton.UseVisualStyleBackColor = false;
            this.DismissButton.Click += new System.EventHandler(this.DismissButton_Click);
            // 
            // dialogtimer
            // 
            this.dialogtimer.Enabled = true;
            this.dialogtimer.Interval = 5000;
            this.dialogtimer.Tick += new System.EventHandler(this.Dialogtimer_Tick);
            // 
            // DismissDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(265, 114);
            this.ControlBox = false;
            this.Controls.Add(this.DismissButton);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DismissDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "AutoSync";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button DismissButton;
        private System.Windows.Forms.Timer dialogtimer;
    }
}