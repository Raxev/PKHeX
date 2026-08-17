namespace PKHeX.WinForms
{
    partial class SAV_BulkQoL
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
            L_Scope = new System.Windows.Forms.Label();
            RB_Boxes = new System.Windows.Forms.RadioButton();
            RB_Party = new System.Windows.Forms.RadioButton();
            RB_Both = new System.Windows.Forms.RadioButton();
            CHK_Ball = new System.Windows.Forms.CheckBox();
            CB_Ball = new System.Windows.Forms.ComboBox();
            CHK_MetLocation = new System.Windows.Forms.CheckBox();
            CB_MetLocation = new System.Windows.Forms.ComboBox();
            CHK_Shiny = new System.Windows.Forms.CheckBox();
            RB_ShinyOn = new System.Windows.Forms.RadioButton();
            RB_ShinyOff = new System.Windows.Forms.RadioButton();
            CHK_MaxIVs = new System.Windows.Forms.CheckBox();
            CHK_NaturePreset = new System.Windows.Forms.CheckBox();
            CB_NaturePreset = new System.Windows.Forms.ComboBox();
            CHK_OptimizeIVs = new System.Windows.Forms.CheckBox();
            CHK_MaxPP = new System.Windows.Forms.CheckBox();
            CHK_FixMoves = new System.Windows.Forms.CheckBox();
            CHK_AutoLegalize = new System.Windows.Forms.CheckBox();
            L_LegalNotice = new System.Windows.Forms.Label();
            B_Run = new System.Windows.Forms.Button();
            B_Close = new System.Windows.Forms.Button();
            SuspendLayout();
            //
            // L_Scope
            //
            L_Scope.AutoSize = true;
            L_Scope.Location = new System.Drawing.Point(12, 15);
            L_Scope.Name = "L_Scope";
            L_Scope.Size = new System.Drawing.Size(46, 15);
            L_Scope.TabIndex = 0;
            L_Scope.Text = "Scope:";
            //
            // RB_Boxes
            //
            RB_Boxes.AutoSize = true;
            RB_Boxes.Location = new System.Drawing.Point(64, 13);
            RB_Boxes.Name = "RB_Boxes";
            RB_Boxes.Size = new System.Drawing.Size(58, 19);
            RB_Boxes.TabIndex = 1;
            RB_Boxes.TabStop = true;
            RB_Boxes.Text = "Boxes";
            RB_Boxes.UseVisualStyleBackColor = true;
            //
            // RB_Party
            //
            RB_Party.AutoSize = true;
            RB_Party.Location = new System.Drawing.Point(128, 13);
            RB_Party.Name = "RB_Party";
            RB_Party.Size = new System.Drawing.Size(53, 19);
            RB_Party.TabIndex = 2;
            RB_Party.Text = "Party";
            RB_Party.UseVisualStyleBackColor = true;
            //
            // RB_Both
            //
            RB_Both.AutoSize = true;
            RB_Both.Location = new System.Drawing.Point(187, 13);
            RB_Both.Name = "RB_Both";
            RB_Both.Size = new System.Drawing.Size(50, 19);
            RB_Both.TabIndex = 3;
            RB_Both.Text = "Both";
            RB_Both.UseVisualStyleBackColor = true;
            //
            // CHK_Ball
            //
            CHK_Ball.AutoSize = true;
            CHK_Ball.Location = new System.Drawing.Point(14, 50);
            CHK_Ball.Name = "CHK_Ball";
            CHK_Ball.Size = new System.Drawing.Size(89, 19);
            CHK_Ball.TabIndex = 4;
            CHK_Ball.Text = "Set Ball to:";
            CHK_Ball.UseVisualStyleBackColor = true;
            //
            // CB_Ball
            //
            CB_Ball.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_Ball.FormattingEnabled = true;
            CB_Ball.Location = new System.Drawing.Point(160, 47);
            CB_Ball.Name = "CB_Ball";
            CB_Ball.Size = new System.Drawing.Size(180, 23);
            CB_Ball.TabIndex = 5;
            //
            // CHK_MetLocation
            //
            CHK_MetLocation.AutoSize = true;
            CHK_MetLocation.Location = new System.Drawing.Point(14, 85);
            CHK_MetLocation.Name = "CHK_MetLocation";
            CHK_MetLocation.Size = new System.Drawing.Size(140, 19);
            CHK_MetLocation.TabIndex = 6;
            CHK_MetLocation.Text = "Set Met Location to:";
            CHK_MetLocation.UseVisualStyleBackColor = true;
            //
            // CB_MetLocation
            //
            CB_MetLocation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_MetLocation.FormattingEnabled = true;
            CB_MetLocation.Location = new System.Drawing.Point(160, 82);
            CB_MetLocation.Name = "CB_MetLocation";
            CB_MetLocation.Size = new System.Drawing.Size(180, 23);
            CB_MetLocation.TabIndex = 7;
            //
            // CHK_Shiny
            //
            CHK_Shiny.AutoSize = true;
            CHK_Shiny.Location = new System.Drawing.Point(14, 120);
            CHK_Shiny.Name = "CHK_Shiny";
            CHK_Shiny.Size = new System.Drawing.Size(100, 19);
            CHK_Shiny.TabIndex = 8;
            CHK_Shiny.Text = "Set Shiny state:";
            CHK_Shiny.UseVisualStyleBackColor = true;
            //
            // RB_ShinyOn
            //
            RB_ShinyOn.AutoSize = true;
            RB_ShinyOn.Location = new System.Drawing.Point(160, 119);
            RB_ShinyOn.Name = "RB_ShinyOn";
            RB_ShinyOn.Size = new System.Drawing.Size(58, 19);
            RB_ShinyOn.TabIndex = 9;
            RB_ShinyOn.TabStop = true;
            RB_ShinyOn.Text = "Shiny";
            RB_ShinyOn.UseVisualStyleBackColor = true;
            //
            // RB_ShinyOff
            //
            RB_ShinyOff.AutoSize = true;
            RB_ShinyOff.Location = new System.Drawing.Point(230, 119);
            RB_ShinyOff.Name = "RB_ShinyOff";
            RB_ShinyOff.Size = new System.Drawing.Size(89, 19);
            RB_ShinyOff.TabIndex = 10;
            RB_ShinyOff.Text = "Not Shiny";
            RB_ShinyOff.UseVisualStyleBackColor = true;
            //
            // CHK_MaxIVs
            //
            CHK_MaxIVs.AutoSize = true;
            CHK_MaxIVs.Location = new System.Drawing.Point(14, 155);
            CHK_MaxIVs.Name = "CHK_MaxIVs";
            CHK_MaxIVs.Size = new System.Drawing.Size(150, 19);
            CHK_MaxIVs.TabIndex = 11;
            CHK_MaxIVs.Text = "Set all IVs to 31";
            CHK_MaxIVs.UseVisualStyleBackColor = true;
            //
            // CHK_NaturePreset
            //
            CHK_NaturePreset.AutoSize = true;
            CHK_NaturePreset.Location = new System.Drawing.Point(14, 180);
            CHK_NaturePreset.Name = "CHK_NaturePreset";
            CHK_NaturePreset.Size = new System.Drawing.Size(220, 19);
            CHK_NaturePreset.TabIndex = 12;
            CHK_NaturePreset.Text = "Set Nature + EVs to preset:";
            CHK_NaturePreset.UseVisualStyleBackColor = true;
            //
            // CB_NaturePreset
            //
            CB_NaturePreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_NaturePreset.FormattingEnabled = true;
            CB_NaturePreset.Location = new System.Drawing.Point(240, 177);
            CB_NaturePreset.Name = "CB_NaturePreset";
            CB_NaturePreset.Size = new System.Drawing.Size(100, 23);
            CB_NaturePreset.TabIndex = 13;
            //
            // CHK_OptimizeIVs
            //
            CHK_OptimizeIVs.AutoSize = true;
            CHK_OptimizeIVs.Location = new System.Drawing.Point(14, 205);
            CHK_OptimizeIVs.Name = "CHK_OptimizeIVs";
            CHK_OptimizeIVs.Size = new System.Drawing.Size(340, 19);
            CHK_OptimizeIVs.TabIndex = 14;
            CHK_OptimizeIVs.Text = "Optimize IVs (best legal, prioritizing highest base stats)";
            CHK_OptimizeIVs.UseVisualStyleBackColor = true;
            //
            // CHK_MaxPP
            //
            CHK_MaxPP.AutoSize = true;
            CHK_MaxPP.Location = new System.Drawing.Point(14, 230);
            CHK_MaxPP.Name = "CHK_MaxPP";
            CHK_MaxPP.Size = new System.Drawing.Size(180, 19);
            CHK_MaxPP.TabIndex = 15;
            CHK_MaxPP.Text = "Set PP Ups to max (all moves)";
            CHK_MaxPP.UseVisualStyleBackColor = true;
            //
            // CHK_FixMoves
            //
            CHK_FixMoves.AutoSize = true;
            CHK_FixMoves.Location = new System.Drawing.Point(14, 255);
            CHK_FixMoves.Name = "CHK_FixMoves";
            CHK_FixMoves.Size = new System.Drawing.Size(280, 19);
            CHK_FixMoves.TabIndex = 16;
            CHK_FixMoves.Text = "Auto-fix illegal movesets";
            CHK_FixMoves.UseVisualStyleBackColor = true;
            //
            // CHK_AutoLegalize
            //
            CHK_AutoLegalize.AutoSize = true;
            CHK_AutoLegalize.Location = new System.Drawing.Point(14, 280);
            CHK_AutoLegalize.Name = "CHK_AutoLegalize";
            CHK_AutoLegalize.Size = new System.Drawing.Size(280, 19);
            CHK_AutoLegalize.TabIndex = 17;
            CHK_AutoLegalize.Text = "Auto-enforce legality (regenerate illegal Pokémon)";
            CHK_AutoLegalize.UseVisualStyleBackColor = true;
            //
            // L_LegalNotice
            //
            L_LegalNotice.Location = new System.Drawing.Point(12, 305);
            L_LegalNotice.Name = "L_LegalNotice";
            L_LegalNotice.Size = new System.Drawing.Size(360, 60);
            L_LegalNotice.TabIndex = 18;
            L_LegalNotice.Text = "Each checked edit is applied one Pokémon at a time; any Pokémon that would become illegal as a result keeps its original value instead. Optimize IVs can be slow: for encounters with correlated PID/IVs it retries up to 2000 times per Pokémon.";
            //
            // B_Run
            //
            B_Run.Location = new System.Drawing.Point(178, 370);
            B_Run.Name = "B_Run";
            B_Run.Size = new System.Drawing.Size(88, 27);
            B_Run.TabIndex = 19;
            B_Run.Text = "Run";
            B_Run.UseVisualStyleBackColor = true;
            B_Run.Click += B_Run_Click;
            //
            // B_Close
            //
            B_Close.Location = new System.Drawing.Point(272, 370);
            B_Close.Name = "B_Close";
            B_Close.Size = new System.Drawing.Size(88, 27);
            B_Close.TabIndex = 20;
            B_Close.Text = "Close";
            B_Close.UseVisualStyleBackColor = true;
            B_Close.Click += B_Close_Click;
            //
            // SAV_BulkQoL
            //
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            ClientSize = new System.Drawing.Size(384, 409);
            Controls.Add(L_Scope);
            Controls.Add(RB_Boxes);
            Controls.Add(RB_Party);
            Controls.Add(RB_Both);
            Controls.Add(CHK_Ball);
            Controls.Add(CB_Ball);
            Controls.Add(CHK_MetLocation);
            Controls.Add(CB_MetLocation);
            Controls.Add(CHK_Shiny);
            Controls.Add(RB_ShinyOn);
            Controls.Add(RB_ShinyOff);
            Controls.Add(CHK_MaxIVs);
            Controls.Add(CHK_NaturePreset);
            Controls.Add(CB_NaturePreset);
            Controls.Add(CHK_OptimizeIVs);
            Controls.Add(CHK_MaxPP);
            Controls.Add(CHK_FixMoves);
            Controls.Add(CHK_AutoLegalize);
            Controls.Add(L_LegalNotice);
            Controls.Add(B_Run);
            Controls.Add(B_Close);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Icon = Properties.Resources.Icon;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SAV_BulkQoL";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Bulk QoL Editor";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label L_Scope;
        private System.Windows.Forms.RadioButton RB_Boxes;
        private System.Windows.Forms.RadioButton RB_Party;
        private System.Windows.Forms.RadioButton RB_Both;
        private System.Windows.Forms.CheckBox CHK_Ball;
        private System.Windows.Forms.ComboBox CB_Ball;
        private System.Windows.Forms.CheckBox CHK_MetLocation;
        private System.Windows.Forms.ComboBox CB_MetLocation;
        private System.Windows.Forms.CheckBox CHK_Shiny;
        private System.Windows.Forms.RadioButton RB_ShinyOn;
        private System.Windows.Forms.RadioButton RB_ShinyOff;
        private System.Windows.Forms.CheckBox CHK_MaxIVs;
        private System.Windows.Forms.CheckBox CHK_NaturePreset;
        private System.Windows.Forms.ComboBox CB_NaturePreset;
        private System.Windows.Forms.CheckBox CHK_OptimizeIVs;
        private System.Windows.Forms.CheckBox CHK_MaxPP;
        private System.Windows.Forms.CheckBox CHK_FixMoves;
        private System.Windows.Forms.CheckBox CHK_AutoLegalize;
        private System.Windows.Forms.Label L_LegalNotice;
        private System.Windows.Forms.Button B_Run;
        private System.Windows.Forms.Button B_Close;
    }
}
