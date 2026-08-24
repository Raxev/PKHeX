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
            L_Filter = new System.Windows.Forms.Label();
            CHK_FilterIllegalOnly = new System.Windows.Forms.CheckBox();
            CHK_FilterShinyOnly = new System.Windows.Forms.CheckBox();
            CHK_FilterSpecies = new System.Windows.Forms.CheckBox();
            CB_FilterSpecies = new System.Windows.Forms.ComboBox();
            CHK_FilterGiftOrigin = new System.Windows.Forms.CheckBox();
            CHK_SkipHomeTracked = new System.Windows.Forms.CheckBox();
            CHK_AlignSize = new System.Windows.Forms.CheckBox();
            CHK_Ball = new System.Windows.Forms.CheckBox();
            CB_Ball = new System.Windows.Forms.ComboBox();
            CHK_MetLocation = new System.Windows.Forms.CheckBox();
            CB_MetLocation = new System.Windows.Forms.ComboBox();
            CHK_Shiny = new System.Windows.Forms.CheckBox();
            RB_ShinyOn = new System.Windows.Forms.RadioButton();
            RB_ShinyOff = new System.Windows.Forms.RadioButton();
            CHK_PreferSquare = new System.Windows.Forms.CheckBox();
            CHK_MaxIVs = new System.Windows.Forms.CheckBox();
            CHK_MaxSize = new System.Windows.Forms.CheckBox();
            CHK_NaturePreset = new System.Windows.Forms.CheckBox();
            CB_NaturePreset = new System.Windows.Forms.ComboBox();
            CHK_OptimizeIVs = new System.Windows.Forms.CheckBox();
            CHK_MaxPP = new System.Windows.Forms.CheckBox();
            CHK_FixMoves = new System.Windows.Forms.CheckBox();
            CHK_FixTrashMemory = new System.Windows.Forms.CheckBox();
            CHK_FixOTMemory = new System.Windows.Forms.CheckBox();
            CHK_RegenTrackerEC = new System.Windows.Forms.CheckBox();
            CHK_AutoLegalize = new System.Windows.Forms.CheckBox();
            L_LegalNotice = new System.Windows.Forms.Label();
            PB_Progress = new System.Windows.Forms.ProgressBar();
            B_Cancel = new System.Windows.Forms.Button();
            B_CheckClones = new System.Windows.Forms.Button();
            B_HomeCheck = new System.Windows.Forms.Button();
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
            RB_Both.Size = new System.Drawing.Size(46, 19);
            RB_Both.TabIndex = 3;
            RB_Both.Text = "All";
            RB_Both.UseVisualStyleBackColor = true;
            //
            // L_Filter
            //
            L_Filter.AutoSize = true;
            L_Filter.Location = new System.Drawing.Point(12, 42);
            L_Filter.Name = "L_Filter";
            L_Filter.Size = new System.Drawing.Size(300, 15);
            L_Filter.TabIndex = 4;
            L_Filter.Text = "Filters (optional -- narrow every edit below to matching Pokémon):";
            //
            // CHK_FilterIllegalOnly
            //
            CHK_FilterIllegalOnly.AutoSize = true;
            CHK_FilterIllegalOnly.Location = new System.Drawing.Point(14, 62);
            CHK_FilterIllegalOnly.Name = "CHK_FilterIllegalOnly";
            CHK_FilterIllegalOnly.Size = new System.Drawing.Size(130, 19);
            CHK_FilterIllegalOnly.TabIndex = 5;
            CHK_FilterIllegalOnly.Text = "Only currently illegal";
            CHK_FilterIllegalOnly.UseVisualStyleBackColor = true;
            //
            // CHK_FilterShinyOnly
            //
            CHK_FilterShinyOnly.AutoSize = true;
            CHK_FilterShinyOnly.Location = new System.Drawing.Point(200, 62);
            CHK_FilterShinyOnly.Name = "CHK_FilterShinyOnly";
            CHK_FilterShinyOnly.Size = new System.Drawing.Size(110, 19);
            CHK_FilterShinyOnly.TabIndex = 6;
            CHK_FilterShinyOnly.Text = "Only currently shiny";
            CHK_FilterShinyOnly.UseVisualStyleBackColor = true;
            //
            // CHK_FilterSpecies
            //
            CHK_FilterSpecies.AutoSize = true;
            CHK_FilterSpecies.Location = new System.Drawing.Point(14, 87);
            CHK_FilterSpecies.Name = "CHK_FilterSpecies";
            CHK_FilterSpecies.Size = new System.Drawing.Size(140, 19);
            CHK_FilterSpecies.TabIndex = 7;
            CHK_FilterSpecies.Text = "Only species:";
            CHK_FilterSpecies.UseVisualStyleBackColor = true;
            //
            // CB_FilterSpecies
            //
            CB_FilterSpecies.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            CB_FilterSpecies.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            CB_FilterSpecies.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_FilterSpecies.FormattingEnabled = true;
            CB_FilterSpecies.Location = new System.Drawing.Point(160, 84);
            CB_FilterSpecies.Name = "CB_FilterSpecies";
            CB_FilterSpecies.Size = new System.Drawing.Size(180, 23);
            CB_FilterSpecies.TabIndex = 8;
            //
            // CHK_FilterGiftOrigin
            //
            CHK_FilterGiftOrigin.AutoSize = true;
            CHK_FilterGiftOrigin.Location = new System.Drawing.Point(14, 112);
            CHK_FilterGiftOrigin.Name = "CHK_FilterGiftOrigin";
            CHK_FilterGiftOrigin.Size = new System.Drawing.Size(180, 19);
            CHK_FilterGiftOrigin.TabIndex = 8;
            CHK_FilterGiftOrigin.Text = "Only Mystery Gift-origin";
            CHK_FilterGiftOrigin.UseVisualStyleBackColor = true;
            //
            // CHK_SkipHomeTracked
            //
            CHK_SkipHomeTracked.AutoSize = true;
            CHK_SkipHomeTracked.Checked = true;
            CHK_SkipHomeTracked.CheckState = System.Windows.Forms.CheckState.Checked;
            CHK_SkipHomeTracked.Location = new System.Drawing.Point(200, 112);
            CHK_SkipHomeTracked.Name = "CHK_SkipHomeTracked";
            CHK_SkipHomeTracked.Size = new System.Drawing.Size(180, 19);
            CHK_SkipHomeTracked.TabIndex = 9;
            CHK_SkipHomeTracked.Text = "Skip HOME-registered";
            CHK_SkipHomeTracked.UseVisualStyleBackColor = true;
            //
            // CHK_AlignSize
            //
            CHK_AlignSize.AutoSize = true;
            CHK_AlignSize.Location = new System.Drawing.Point(14, 292);
            CHK_AlignSize.Name = "CHK_AlignSize";
            CHK_AlignSize.Size = new System.Drawing.Size(340, 19);
            CHK_AlignSize.TabIndex = 20;
            CHK_AlignSize.Text = "Align Height/Weight to Scale (Gen9, HOME does this on import)";
            CHK_AlignSize.UseVisualStyleBackColor = true;
            //
            // CHK_Ball
            //
            CHK_Ball.AutoSize = true;
            CHK_Ball.Location = new System.Drawing.Point(14, 142);
            CHK_Ball.Name = "CHK_Ball";
            CHK_Ball.Size = new System.Drawing.Size(89, 19);
            CHK_Ball.TabIndex = 9;
            CHK_Ball.Text = "Set Ball to:";
            CHK_Ball.UseVisualStyleBackColor = true;
            //
            // CB_Ball
            //
            CB_Ball.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_Ball.FormattingEnabled = true;
            CB_Ball.Location = new System.Drawing.Point(160, 139);
            CB_Ball.Name = "CB_Ball";
            CB_Ball.Size = new System.Drawing.Size(180, 23);
            CB_Ball.TabIndex = 10;
            //
            // CHK_MetLocation
            //
            CHK_MetLocation.AutoSize = true;
            CHK_MetLocation.Location = new System.Drawing.Point(14, 167);
            CHK_MetLocation.Name = "CHK_MetLocation";
            CHK_MetLocation.Size = new System.Drawing.Size(140, 19);
            CHK_MetLocation.TabIndex = 11;
            CHK_MetLocation.Text = "Set Met Location to:";
            CHK_MetLocation.UseVisualStyleBackColor = true;
            //
            // CB_MetLocation
            //
            CB_MetLocation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_MetLocation.FormattingEnabled = true;
            CB_MetLocation.Location = new System.Drawing.Point(160, 164);
            CB_MetLocation.Name = "CB_MetLocation";
            CB_MetLocation.Size = new System.Drawing.Size(180, 23);
            CB_MetLocation.TabIndex = 12;
            //
            // CHK_Shiny
            //
            CHK_Shiny.AutoSize = true;
            CHK_Shiny.Location = new System.Drawing.Point(14, 192);
            CHK_Shiny.Name = "CHK_Shiny";
            CHK_Shiny.Size = new System.Drawing.Size(100, 19);
            CHK_Shiny.TabIndex = 13;
            CHK_Shiny.Text = "Set Shiny state:";
            CHK_Shiny.UseVisualStyleBackColor = true;
            //
            // RB_ShinyOn
            //
            RB_ShinyOn.AutoSize = true;
            RB_ShinyOn.Location = new System.Drawing.Point(160, 191);
            RB_ShinyOn.Name = "RB_ShinyOn";
            RB_ShinyOn.Size = new System.Drawing.Size(58, 19);
            RB_ShinyOn.TabIndex = 14;
            RB_ShinyOn.TabStop = true;
            RB_ShinyOn.Text = "Shiny";
            RB_ShinyOn.UseVisualStyleBackColor = true;
            //
            // RB_ShinyOff
            //
            RB_ShinyOff.AutoSize = true;
            RB_ShinyOff.Location = new System.Drawing.Point(230, 191);
            RB_ShinyOff.Name = "RB_ShinyOff";
            RB_ShinyOff.Size = new System.Drawing.Size(89, 19);
            RB_ShinyOff.TabIndex = 15;
            RB_ShinyOff.Text = "Not Shiny";
            RB_ShinyOff.UseVisualStyleBackColor = true;
            //
            // CHK_PreferSquare
            //
            CHK_PreferSquare.AutoSize = true;
            CHK_PreferSquare.Checked = true;
            CHK_PreferSquare.CheckState = System.Windows.Forms.CheckState.Checked;
            CHK_PreferSquare.Location = new System.Drawing.Point(160, 214);
            CHK_PreferSquare.Name = "CHK_PreferSquare";
            CHK_PreferSquare.Size = new System.Drawing.Size(220, 19);
            CHK_PreferSquare.TabIndex = 16;
            CHK_PreferSquare.Text = "Prefer Square shiny (Gen8+)";
            CHK_PreferSquare.UseVisualStyleBackColor = true;
            //
            // CHK_MaxIVs
            //
            CHK_MaxIVs.AutoSize = true;
            CHK_MaxIVs.Location = new System.Drawing.Point(14, 242);
            CHK_MaxIVs.Name = "CHK_MaxIVs";
            CHK_MaxIVs.Size = new System.Drawing.Size(150, 19);
            CHK_MaxIVs.TabIndex = 16;
            CHK_MaxIVs.Text = "Set all IVs to 31";
            CHK_MaxIVs.UseVisualStyleBackColor = true;
            //
            // CHK_MaxSize
            //
            CHK_MaxSize.AutoSize = true;
            CHK_MaxSize.Location = new System.Drawing.Point(14, 267);
            CHK_MaxSize.Name = "CHK_MaxSize";
            CHK_MaxSize.Size = new System.Drawing.Size(280, 19);
            CHK_MaxSize.TabIndex = 17;
            CHK_MaxSize.Text = "Max size (height/weight/scale, Gen8+ only)";
            CHK_MaxSize.UseVisualStyleBackColor = true;
            //
            // CHK_NaturePreset
            //
            CHK_NaturePreset.AutoSize = true;
            CHK_NaturePreset.Location = new System.Drawing.Point(14, 317);
            CHK_NaturePreset.Name = "CHK_NaturePreset";
            CHK_NaturePreset.Size = new System.Drawing.Size(220, 19);
            CHK_NaturePreset.TabIndex = 18;
            CHK_NaturePreset.Text = "Set Nature + EVs to preset:";
            CHK_NaturePreset.UseVisualStyleBackColor = true;
            //
            // CB_NaturePreset
            //
            CB_NaturePreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_NaturePreset.FormattingEnabled = true;
            CB_NaturePreset.Location = new System.Drawing.Point(240, 314);
            CB_NaturePreset.Name = "CB_NaturePreset";
            CB_NaturePreset.Size = new System.Drawing.Size(100, 23);
            CB_NaturePreset.TabIndex = 19;
            //
            // CHK_OptimizeIVs
            //
            CHK_OptimizeIVs.AutoSize = true;
            CHK_OptimizeIVs.Location = new System.Drawing.Point(14, 342);
            CHK_OptimizeIVs.Name = "CHK_OptimizeIVs";
            CHK_OptimizeIVs.Size = new System.Drawing.Size(340, 19);
            CHK_OptimizeIVs.TabIndex = 20;
            CHK_OptimizeIVs.Text = "Optimize IVs (best legal, prioritizing highest base stats)";
            CHK_OptimizeIVs.UseVisualStyleBackColor = true;
            //
            // CHK_MaxPP
            //
            CHK_MaxPP.AutoSize = true;
            CHK_MaxPP.Location = new System.Drawing.Point(14, 367);
            CHK_MaxPP.Name = "CHK_MaxPP";
            CHK_MaxPP.Size = new System.Drawing.Size(180, 19);
            CHK_MaxPP.TabIndex = 21;
            CHK_MaxPP.Text = "Set PP Ups to max (all moves)";
            CHK_MaxPP.UseVisualStyleBackColor = true;
            //
            // CHK_FixMoves
            //
            CHK_FixMoves.AutoSize = true;
            CHK_FixMoves.Location = new System.Drawing.Point(14, 392);
            CHK_FixMoves.Name = "CHK_FixMoves";
            CHK_FixMoves.Size = new System.Drawing.Size(280, 19);
            CHK_FixMoves.TabIndex = 22;
            CHK_FixMoves.Text = "Auto-fix illegal movesets";
            CHK_FixMoves.UseVisualStyleBackColor = true;
            //
            // CHK_FixTrashMemory
            //
            CHK_FixTrashMemory.AutoSize = true;
            CHK_FixTrashMemory.Location = new System.Drawing.Point(14, 417);
            CHK_FixTrashMemory.Name = "CHK_FixTrashMemory";
            CHK_FixTrashMemory.Size = new System.Drawing.Size(340, 19);
            CHK_FixTrashMemory.TabIndex = 23;
            CHK_FixTrashMemory.Text = "Fix trash bytes / stale Handling Trainer memory";
            CHK_FixTrashMemory.UseVisualStyleBackColor = true;
            //
            // CHK_FixOTMemory
            //
            CHK_FixOTMemory.AutoSize = true;
            CHK_FixOTMemory.Location = new System.Drawing.Point(14, 392);
            CHK_FixOTMemory.Name = "CHK_FixOTMemory";
            CHK_FixOTMemory.Size = new System.Drawing.Size(340, 19);
            CHK_FixOTMemory.TabIndex = 24;
            CHK_FixOTMemory.Text = "Fix missing OT memory (safe on HOME-registered)";
            CHK_FixOTMemory.UseVisualStyleBackColor = true;
            //
            // CHK_RegenTrackerEC
            //
            CHK_RegenTrackerEC.AutoSize = true;
            CHK_RegenTrackerEC.Location = new System.Drawing.Point(14, 467);
            CHK_RegenTrackerEC.Name = "CHK_RegenTrackerEC";
            CHK_RegenTrackerEC.Size = new System.Drawing.Size(340, 19);
            CHK_RegenTrackerEC.TabIndex = 24;
            CHK_RegenTrackerEC.Text = "Regenerate PID / HOME Tracker / Encryption Constant (dodge clones)";
            CHK_RegenTrackerEC.UseVisualStyleBackColor = true;
            //
            // CHK_AutoLegalize
            //
            CHK_AutoLegalize.AutoSize = true;
            CHK_AutoLegalize.Location = new System.Drawing.Point(14, 492);
            CHK_AutoLegalize.Name = "CHK_AutoLegalize";
            CHK_AutoLegalize.Size = new System.Drawing.Size(280, 19);
            CHK_AutoLegalize.TabIndex = 25;
            CHK_AutoLegalize.Text = "Auto-enforce legality (regenerate illegal Pokémon)";
            CHK_AutoLegalize.UseVisualStyleBackColor = true;
            //
            // L_LegalNotice
            //
            L_LegalNotice.Location = new System.Drawing.Point(12, 517);
            L_LegalNotice.Name = "L_LegalNotice";
            L_LegalNotice.Size = new System.Drawing.Size(360, 60);
            L_LegalNotice.TabIndex = 26;
            L_LegalNotice.Text = "Each checked edit is applied one Pokémon at a time; any Pokémon that would become illegal as a result keeps its original value instead. Optimize IVs can be slow: for encounters with correlated PID/IVs it retries up to 2000 times per Pokémon.";
            //
            // PB_Progress
            //
            PB_Progress.Location = new System.Drawing.Point(12, 582);
            PB_Progress.Name = "PB_Progress";
            PB_Progress.Size = new System.Drawing.Size(268, 20);
            PB_Progress.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            PB_Progress.TabIndex = 29;
            PB_Progress.Visible = false;
            //
            // B_Cancel
            //
            B_Cancel.Location = new System.Drawing.Point(286, 580);
            B_Cancel.Name = "B_Cancel";
            B_Cancel.Size = new System.Drawing.Size(74, 24);
            B_Cancel.TabIndex = 30;
            B_Cancel.Text = "Cancel";
            B_Cancel.UseVisualStyleBackColor = true;
            B_Cancel.Visible = false;
            B_Cancel.Click += B_Cancel_Click;
            //
            // B_CheckClones
            //
            B_CheckClones.Location = new System.Drawing.Point(12, 612);
            B_CheckClones.Name = "B_CheckClones";
            B_CheckClones.Size = new System.Drawing.Size(150, 27);
            B_CheckClones.TabIndex = 31;
            B_CheckClones.Text = "Check for Clones";
            B_CheckClones.UseVisualStyleBackColor = true;
            B_CheckClones.Click += B_CheckClones_Click;
            //
            // B_HomeCheck
            //
            B_HomeCheck.Location = new System.Drawing.Point(168, 612);
            B_HomeCheck.Name = "B_HomeCheck";
            B_HomeCheck.Size = new System.Drawing.Size(192, 27);
            B_HomeCheck.TabIndex = 34;
            B_HomeCheck.Text = "Check HOME Transfer Risk";
            B_HomeCheck.UseVisualStyleBackColor = true;
            B_HomeCheck.Click += B_HomeCheck_Click;
            //
            // B_Run
            //
            B_Run.Location = new System.Drawing.Point(178, 644);
            B_Run.Name = "B_Run";
            B_Run.Size = new System.Drawing.Size(88, 27);
            B_Run.TabIndex = 32;
            B_Run.Text = "Run";
            B_Run.UseVisualStyleBackColor = true;
            B_Run.Click += B_Run_Click;
            //
            // B_Close
            //
            B_Close.Location = new System.Drawing.Point(272, 644);
            B_Close.Name = "B_Close";
            B_Close.Size = new System.Drawing.Size(88, 27);
            B_Close.TabIndex = 33;
            B_Close.Text = "Close";
            B_Close.UseVisualStyleBackColor = true;
            B_Close.Click += B_Close_Click;
            //
            // SAV_BulkQoL
            //
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            ClientSize = new System.Drawing.Size(384, 684);
            Controls.Add(L_Scope);
            Controls.Add(RB_Boxes);
            Controls.Add(RB_Party);
            Controls.Add(RB_Both);
            Controls.Add(L_Filter);
            Controls.Add(CHK_FilterIllegalOnly);
            Controls.Add(CHK_FilterShinyOnly);
            Controls.Add(CHK_FilterSpecies);
            Controls.Add(CB_FilterSpecies);
            Controls.Add(CHK_FilterGiftOrigin);
            Controls.Add(CHK_SkipHomeTracked);
            Controls.Add(CHK_AlignSize);
            Controls.Add(CHK_Ball);
            Controls.Add(CB_Ball);
            Controls.Add(CHK_MetLocation);
            Controls.Add(CB_MetLocation);
            Controls.Add(CHK_Shiny);
            Controls.Add(RB_ShinyOn);
            Controls.Add(RB_ShinyOff);
            Controls.Add(CHK_PreferSquare);
            Controls.Add(CHK_MaxIVs);
            Controls.Add(CHK_MaxSize);
            Controls.Add(CHK_NaturePreset);
            Controls.Add(CB_NaturePreset);
            Controls.Add(CHK_OptimizeIVs);
            Controls.Add(CHK_MaxPP);
            Controls.Add(CHK_FixMoves);
            Controls.Add(CHK_FixTrashMemory);
            Controls.Add(CHK_FixOTMemory);
            Controls.Add(CHK_RegenTrackerEC);
            Controls.Add(CHK_AutoLegalize);
            Controls.Add(L_LegalNotice);
            Controls.Add(PB_Progress);
            Controls.Add(B_Cancel);
            Controls.Add(B_CheckClones);
            Controls.Add(B_HomeCheck);
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
        private System.Windows.Forms.Label L_Filter;
        private System.Windows.Forms.CheckBox CHK_FilterIllegalOnly;
        private System.Windows.Forms.CheckBox CHK_FilterShinyOnly;
        private System.Windows.Forms.CheckBox CHK_FilterSpecies;
        private System.Windows.Forms.ComboBox CB_FilterSpecies;
        private System.Windows.Forms.CheckBox CHK_FilterGiftOrigin;
        private System.Windows.Forms.CheckBox CHK_SkipHomeTracked;
        private System.Windows.Forms.CheckBox CHK_AlignSize;
        private System.Windows.Forms.CheckBox CHK_Ball;
        private System.Windows.Forms.ComboBox CB_Ball;
        private System.Windows.Forms.CheckBox CHK_MetLocation;
        private System.Windows.Forms.ComboBox CB_MetLocation;
        private System.Windows.Forms.CheckBox CHK_Shiny;
        private System.Windows.Forms.RadioButton RB_ShinyOn;
        private System.Windows.Forms.RadioButton RB_ShinyOff;
        private System.Windows.Forms.CheckBox CHK_PreferSquare;
        private System.Windows.Forms.CheckBox CHK_MaxIVs;
        private System.Windows.Forms.CheckBox CHK_MaxSize;
        private System.Windows.Forms.CheckBox CHK_NaturePreset;
        private System.Windows.Forms.ComboBox CB_NaturePreset;
        private System.Windows.Forms.CheckBox CHK_OptimizeIVs;
        private System.Windows.Forms.CheckBox CHK_MaxPP;
        private System.Windows.Forms.CheckBox CHK_FixMoves;
        private System.Windows.Forms.CheckBox CHK_FixTrashMemory;
        private System.Windows.Forms.CheckBox CHK_FixOTMemory;
        private System.Windows.Forms.CheckBox CHK_RegenTrackerEC;
        private System.Windows.Forms.CheckBox CHK_AutoLegalize;
        private System.Windows.Forms.Label L_LegalNotice;
        private System.Windows.Forms.ProgressBar PB_Progress;
        private System.Windows.Forms.Button B_Cancel;
        private System.Windows.Forms.Button B_CheckClones;
        private System.Windows.Forms.Button B_HomeCheck;
        private System.Windows.Forms.Button B_Run;
        private System.Windows.Forms.Button B_Close;
    }
}
