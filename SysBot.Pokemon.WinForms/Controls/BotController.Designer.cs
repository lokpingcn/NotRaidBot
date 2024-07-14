namespace SysBot.Pokemon.WinForms
{
    partial class BotController
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            L_Description = new System.Windows.Forms.Label();
            L_Left = new System.Windows.Forms.Label();
            PB_Lamp = new System.Windows.Forms.PictureBox();
            RCMenu = new System.Windows.Forms.ContextMenuStrip(components);
            ((System.ComponentModel.ISupportInitialize)PB_Lamp).BeginInit();
            SuspendLayout();
            // 
            // L_Description
            // 
            L_Description.AutoSize = true;
            L_Description.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            L_Description.Location = new System.Drawing.Point(200, 14);
            L_Description.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            L_Description.Name = "L_Description";
            L_Description.Size = new System.Drawing.Size(40, 17);
            L_Description.TabIndex = 2;
            L_Description.Text = "狀態";
            // 
            // L_Left
            // 
            L_Left.AutoSize = true;
            L_Left.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            L_Left.Location = new System.Drawing.Point(41, 3);
            L_Left.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            L_Left.Name = "L_Left";
            L_Left.Size = new System.Drawing.Size(128, 34);
            L_Left.TabIndex = 3;
            L_Left.Text = "192.168.123.123\r\n遭遇机器人";
            // 
            // PB_Lamp
            // 
            PB_Lamp.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            PB_Lamp.Location = new System.Drawing.Point(4, 5);
            PB_Lamp.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            PB_Lamp.Name = "PB_Lamp";
            PB_Lamp.Size = new System.Drawing.Size(34, 39);
            PB_Lamp.TabIndex = 4;
            PB_Lamp.TabStop = false;
            // 
            // RCMenu
            // 
            RCMenu.ImageScalingSize = new System.Drawing.Size(19, 19);
            RCMenu.Name = "RCMenu";
            RCMenu.ShowImageMargin = false;
            RCMenu.ShowItemToolTips = false;
            RCMenu.Size = new System.Drawing.Size(36, 4);
            // 
            // BotController
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ContextMenuStrip = RCMenu;
            Controls.Add(PB_Lamp);
            Controls.Add(L_Left);
            Controls.Add(L_Description);
            Margin = new System.Windows.Forms.Padding(0);
            Name = "BotController";
            Size = new System.Drawing.Size(547, 49);
            MouseEnter += BotController_MouseEnter;
            MouseLeave += BotController_MouseLeave;
            ((System.ComponentModel.ISupportInitialize)PB_Lamp).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.Label L_Description;
        private System.Windows.Forms.Label L_Left;
        private System.Windows.Forms.PictureBox PB_Lamp;
        private System.Windows.Forms.ContextMenuStrip RCMenu;
    }
}
