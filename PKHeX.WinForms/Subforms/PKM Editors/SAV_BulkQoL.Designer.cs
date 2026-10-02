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
            GB_Filters = new System.Windows.Forms.GroupBox();
            CHK_FilterIllegalOnly = new System.Windows.Forms.CheckBox();
            CHK_FilterShinyOnly = new System.Windows.Forms.CheckBox();
            CHK_FilterSpecies = new System.Windows.Forms.CheckBox();
            CB_FilterSpecies = new System.Windows.Forms.ComboBox();
            CHK_FilterOrigin = new System.Windows.Forms.CheckBox();
            CB_FilterOrigin = new System.Windows.Forms.ComboBox();
            CHK_FilterGiftOrigin = new System.Windows.Forms.CheckBox();
            CHK_SkipHomeTracked = new System.Windows.Forms.CheckBox();
            GB_SetValues = new System.Windows.Forms.GroupBox();
            CHK_SelectAll_SetValues = new System.Windows.Forms.CheckBox();
            CHK_Ball = new System.Windows.Forms.CheckBox();
            CB_Ball = new System.Windows.Forms.ComboBox();
            CHK_MetLocation = new System.Windows.Forms.CheckBox();
            CB_MetLocation = new System.Windows.Forms.ComboBox();
            CHK_TrainerName = new System.Windows.Forms.CheckBox();
            TB_TrainerName = new System.Windows.Forms.TextBox();
            CHK_Shiny = new System.Windows.Forms.CheckBox();
            RB_ShinyOn = new System.Windows.Forms.RadioButton();
            RB_ShinyOff = new System.Windows.Forms.RadioButton();
            CHK_PreferSquare = new System.Windows.Forms.CheckBox();
            CHK_NaturePreset = new System.Windows.Forms.CheckBox();
            CB_NaturePreset = new System.Windows.Forms.ComboBox();
            GB_StatsSize = new System.Windows.Forms.GroupBox();
            CHK_SelectAll_StatsSize = new System.Windows.Forms.CheckBox();
            CHK_MaxIVs = new System.Windows.Forms.CheckBox();
            CHK_OptimizeIVs = new System.Windows.Forms.CheckBox();
            CHK_MaxPP = new System.Windows.Forms.CheckBox();
            CHK_MaxSize = new System.Windows.Forms.CheckBox();
            CHK_AlignSize = new System.Windows.Forms.CheckBox();
            GB_Repairs = new System.Windows.Forms.GroupBox();
            CHK_SelectAll_Repairs = new System.Windows.Forms.CheckBox();
            CHK_FixMoves = new System.Windows.Forms.CheckBox();
            CHK_FixTrashMemory = new System.Windows.Forms.CheckBox();
            CHK_FixOTMemory = new System.Windows.Forms.CheckBox();
            CHK_FixFishy = new System.Windows.Forms.CheckBox();
            CHK_FixTransferNature = new System.Windows.Forms.CheckBox();
            CHK_FixTransferSideFields = new System.Windows.Forms.CheckBox();
            CHK_SquareVCShiny = new System.Windows.Forms.CheckBox();
            CHK_FixBattleForms = new System.Windows.Forms.CheckBox();
            CHK_FixFakeEvent = new System.Windows.Forms.CheckBox();
            CHK_RepairMet = new System.Windows.Forms.CheckBox();
            CHK_Rehome = new System.Windows.Forms.CheckBox();
            CHK_FixTera = new System.Windows.Forms.CheckBox();
            CHK_AutoLegalize = new System.Windows.Forms.CheckBox();
            GB_Origin = new System.Windows.Forms.GroupBox();
            CHK_SelectAll_Origin = new System.Windows.Forms.CheckBox();
            CHK_NativeEgg = new System.Windows.Forms.CheckBox();
            CHK_SyncDates = new System.Windows.Forms.CheckBox();
            CHK_SquareAll = new System.Windows.Forms.CheckBox();
            CHK_ClearTracker = new System.Windows.Forms.CheckBox();
            CHK_RegenTrackerEC = new System.Windows.Forms.CheckBox();
            L_LegalNotice = new System.Windows.Forms.Label();
            PB_Progress = new System.Windows.Forms.ProgressBar();
            B_Cancel = new System.Windows.Forms.Button();
            B_CheckClones = new System.Windows.Forms.Button();
            B_HomeCheck = new System.Windows.Forms.Button();
            B_SortBoxes = new System.Windows.Forms.Button();
            B_Run = new System.Windows.Forms.Button();
            B_Close = new System.Windows.Forms.Button();
            Panel_Footer = new System.Windows.Forms.Panel();
            Panel_Scroll = new System.Windows.Forms.Panel();
            SuspendLayout();
            //
            // L_Scope
            //
            L_Scope.AutoSize = true;
            L_Scope.Location = new System.Drawing.Point(12, 15);
            L_Scope.Name = "L_Scope";
            L_Scope.Size = new System.Drawing.Size(46, 15);
            L_Scope.Text = "Scope:";
            //
            // RB_Boxes
            //
            RB_Boxes.AutoSize = true;
            RB_Boxes.Location = new System.Drawing.Point(64, 13);
            RB_Boxes.Name = "RB_Boxes";
            RB_Boxes.Size = new System.Drawing.Size(58, 19);
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
            RB_Party.Text = "Party";
            RB_Party.UseVisualStyleBackColor = true;
            //
            // RB_Both
            //
            RB_Both.AutoSize = true;
            RB_Both.Location = new System.Drawing.Point(187, 13);
            RB_Both.Name = "RB_Both";
            RB_Both.Size = new System.Drawing.Size(46, 19);
            RB_Both.Text = "All";
            RB_Both.UseVisualStyleBackColor = true;
            //
            // GB_Filters
            //
            GB_Filters.AutoSize = true;
            GB_Filters.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            GB_Filters.Location = new System.Drawing.Point(12, 45);
            GB_Filters.MinimumSize = new System.Drawing.Size(416, 132);
            GB_Filters.Name = "GB_Filters";
            GB_Filters.Size = new System.Drawing.Size(416, 132);
            GB_Filters.TabStop = false;
            GB_Filters.Text = "Filters (optional -- narrow every edit below to matching Pokemon)";
            //
            // CHK_FilterIllegalOnly
            //
            CHK_FilterIllegalOnly.AutoSize = true;
            CHK_FilterIllegalOnly.Location = new System.Drawing.Point(14, 22);
            CHK_FilterIllegalOnly.Name = "CHK_FilterIllegalOnly";
            CHK_FilterIllegalOnly.Size = new System.Drawing.Size(190, 19);
            CHK_FilterIllegalOnly.Text = "Only currently illegal";
            CHK_FilterIllegalOnly.UseVisualStyleBackColor = true;
            //
            // CHK_FilterShinyOnly
            //
            CHK_FilterShinyOnly.AutoSize = true;
            CHK_FilterShinyOnly.Location = new System.Drawing.Point(210, 22);
            CHK_FilterShinyOnly.Name = "CHK_FilterShinyOnly";
            CHK_FilterShinyOnly.Size = new System.Drawing.Size(190, 19);
            CHK_FilterShinyOnly.Text = "Only currently shiny";
            CHK_FilterShinyOnly.UseVisualStyleBackColor = true;
            //
            // CHK_FilterSpecies
            //
            CHK_FilterSpecies.AutoSize = true;
            CHK_FilterSpecies.Location = new System.Drawing.Point(14, 47);
            CHK_FilterSpecies.Name = "CHK_FilterSpecies";
            CHK_FilterSpecies.Size = new System.Drawing.Size(140, 19);
            CHK_FilterSpecies.Text = "Only species:";
            CHK_FilterSpecies.UseVisualStyleBackColor = true;
            //
            // CB_FilterSpecies
            //
            CB_FilterSpecies.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_FilterSpecies.FormattingEnabled = true;
            CB_FilterSpecies.Location = new System.Drawing.Point(160, 44);
            CB_FilterSpecies.Name = "CB_FilterSpecies";
            CB_FilterSpecies.Size = new System.Drawing.Size(180, 23);
            //
            // CHK_FilterOrigin
            //
            CHK_FilterOrigin.AutoSize = true;
            CHK_FilterOrigin.Location = new System.Drawing.Point(14, 72);
            CHK_FilterOrigin.Name = "CHK_FilterOrigin";
            CHK_FilterOrigin.Size = new System.Drawing.Size(140, 19);
            CHK_FilterOrigin.Text = "Only origin game:";
            CHK_FilterOrigin.UseVisualStyleBackColor = true;
            //
            // CB_FilterOrigin
            //
            CB_FilterOrigin.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_FilterOrigin.FormattingEnabled = true;
            CB_FilterOrigin.Location = new System.Drawing.Point(160, 69);
            CB_FilterOrigin.Name = "CB_FilterOrigin";
            CB_FilterOrigin.Size = new System.Drawing.Size(180, 23);
            //
            // CHK_FilterGiftOrigin
            //
            CHK_FilterGiftOrigin.AutoSize = true;
            CHK_FilterGiftOrigin.Location = new System.Drawing.Point(14, 97);
            CHK_FilterGiftOrigin.Name = "CHK_FilterGiftOrigin";
            CHK_FilterGiftOrigin.Size = new System.Drawing.Size(190, 19);
            CHK_FilterGiftOrigin.Text = "Only Mystery Gift-origin";
            CHK_FilterGiftOrigin.UseVisualStyleBackColor = true;
            //
            // CHK_SkipHomeTracked
            //
            CHK_SkipHomeTracked.AutoSize = true;
            CHK_SkipHomeTracked.Checked = true;
            CHK_SkipHomeTracked.CheckState = System.Windows.Forms.CheckState.Checked;
            CHK_SkipHomeTracked.Location = new System.Drawing.Point(210, 97);
            CHK_SkipHomeTracked.Name = "CHK_SkipHomeTracked";
            CHK_SkipHomeTracked.Size = new System.Drawing.Size(190, 19);
            CHK_SkipHomeTracked.Text = "Skip HOME-registered";
            CHK_SkipHomeTracked.UseVisualStyleBackColor = true;
            //
            // GB_SetValues
            //
            GB_SetValues.AutoSize = true;
            GB_SetValues.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            GB_SetValues.Location = new System.Drawing.Point(12, 189);
            GB_SetValues.MinimumSize = new System.Drawing.Size(416, 207);
            GB_SetValues.Name = "GB_SetValues";
            GB_SetValues.Size = new System.Drawing.Size(416, 207);
            GB_SetValues.TabStop = false;
            GB_SetValues.Text = "Set Values";
            //
            // CHK_SelectAll_SetValues
            //
            CHK_SelectAll_SetValues.AutoSize = true;
            CHK_SelectAll_SetValues.Location = new System.Drawing.Point(10, 22);
            CHK_SelectAll_SetValues.Name = "CHK_SelectAll_SetValues";
            CHK_SelectAll_SetValues.Size = new System.Drawing.Size(140, 19);
            CHK_SelectAll_SetValues.Text = "Select all below";
            CHK_SelectAll_SetValues.Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold);
            CHK_SelectAll_SetValues.UseVisualStyleBackColor = true;
            //
            // CHK_Ball
            //
            CHK_Ball.AutoSize = true;
            CHK_Ball.Location = new System.Drawing.Point(14, 47);
            CHK_Ball.Name = "CHK_Ball";
            CHK_Ball.Size = new System.Drawing.Size(140, 19);
            CHK_Ball.Text = "Set Ball to:";
            CHK_Ball.UseVisualStyleBackColor = true;
            //
            // CB_Ball
            //
            CB_Ball.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_Ball.FormattingEnabled = true;
            CB_Ball.Location = new System.Drawing.Point(160, 44);
            CB_Ball.Name = "CB_Ball";
            CB_Ball.Size = new System.Drawing.Size(180, 23);
            //
            // CHK_MetLocation
            //
            CHK_MetLocation.AutoSize = true;
            CHK_MetLocation.Location = new System.Drawing.Point(14, 72);
            CHK_MetLocation.Name = "CHK_MetLocation";
            CHK_MetLocation.Size = new System.Drawing.Size(140, 19);
            CHK_MetLocation.Text = "Set Met Location to:";
            CHK_MetLocation.UseVisualStyleBackColor = true;
            //
            // CB_MetLocation
            //
            CB_MetLocation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_MetLocation.FormattingEnabled = true;
            CB_MetLocation.Location = new System.Drawing.Point(160, 69);
            CB_MetLocation.Name = "CB_MetLocation";
            CB_MetLocation.Size = new System.Drawing.Size(180, 23);
            //
            // CHK_TrainerName
            //
            CHK_TrainerName.AutoSize = true;
            CHK_TrainerName.Location = new System.Drawing.Point(14, 97);
            CHK_TrainerName.Name = "CHK_TrainerName";
            CHK_TrainerName.Size = new System.Drawing.Size(140, 19);
            CHK_TrainerName.Text = "Set OT name to:";
            CHK_TrainerName.UseVisualStyleBackColor = true;
            //
            // TB_TrainerName
            //
            TB_TrainerName.MaxLength = 12;
            TB_TrainerName.Location = new System.Drawing.Point(160, 94);
            TB_TrainerName.Name = "TB_TrainerName";
            TB_TrainerName.Size = new System.Drawing.Size(180, 23);
            //
            // CHK_Shiny
            //
            CHK_Shiny.AutoSize = true;
            CHK_Shiny.Location = new System.Drawing.Point(14, 122);
            CHK_Shiny.Name = "CHK_Shiny";
            CHK_Shiny.Size = new System.Drawing.Size(140, 19);
            CHK_Shiny.Text = "Set Shiny state:";
            CHK_Shiny.UseVisualStyleBackColor = true;
            //
            // RB_ShinyOn
            //
            RB_ShinyOn.AutoSize = true;
            RB_ShinyOn.TabStop = true;
            RB_ShinyOn.Location = new System.Drawing.Point(160, 119);
            RB_ShinyOn.Name = "RB_ShinyOn";
            RB_ShinyOn.Size = new System.Drawing.Size(58, 19);
            RB_ShinyOn.Text = "Shiny";
            RB_ShinyOn.UseVisualStyleBackColor = true;
            //
            // RB_ShinyOff
            //
            RB_ShinyOff.AutoSize = true;
            RB_ShinyOff.Location = new System.Drawing.Point(224, 119);
            RB_ShinyOff.Name = "RB_ShinyOff";
            RB_ShinyOff.Size = new System.Drawing.Size(89, 19);
            RB_ShinyOff.Text = "Not Shiny";
            RB_ShinyOff.UseVisualStyleBackColor = true;
            //
            // CHK_PreferSquare
            //
            CHK_PreferSquare.AutoSize = true;
            CHK_PreferSquare.Checked = true;
            CHK_PreferSquare.CheckState = System.Windows.Forms.CheckState.Checked;
            CHK_PreferSquare.Location = new System.Drawing.Point(160, 147);
            CHK_PreferSquare.Name = "CHK_PreferSquare";
            CHK_PreferSquare.Size = new System.Drawing.Size(220, 19);
            CHK_PreferSquare.Text = "Prefer Square shiny (Gen8+)";
            CHK_PreferSquare.UseVisualStyleBackColor = true;
            //
            // CHK_NaturePreset
            //
            CHK_NaturePreset.AutoSize = true;
            CHK_NaturePreset.Location = new System.Drawing.Point(14, 172);
            CHK_NaturePreset.Name = "CHK_NaturePreset";
            CHK_NaturePreset.Size = new System.Drawing.Size(220, 19);
            CHK_NaturePreset.Text = "Set Nature + EVs to preset:";
            CHK_NaturePreset.UseVisualStyleBackColor = true;
            //
            // CB_NaturePreset
            //
            CB_NaturePreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            CB_NaturePreset.FormattingEnabled = true;
            CB_NaturePreset.Location = new System.Drawing.Point(240, 169);
            CB_NaturePreset.Name = "CB_NaturePreset";
            CB_NaturePreset.Size = new System.Drawing.Size(100, 23);
            //
            // GB_StatsSize
            //
            GB_StatsSize.AutoSize = true;
            GB_StatsSize.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            GB_StatsSize.Location = new System.Drawing.Point(12, 408);
            GB_StatsSize.MinimumSize = new System.Drawing.Size(416, 182);
            GB_StatsSize.Name = "GB_StatsSize";
            GB_StatsSize.Size = new System.Drawing.Size(416, 182);
            GB_StatsSize.TabStop = false;
            GB_StatsSize.Text = "Stats && Size";
            //
            // CHK_SelectAll_StatsSize
            //
            CHK_SelectAll_StatsSize.AutoSize = true;
            CHK_SelectAll_StatsSize.Location = new System.Drawing.Point(10, 22);
            CHK_SelectAll_StatsSize.Name = "CHK_SelectAll_StatsSize";
            CHK_SelectAll_StatsSize.Size = new System.Drawing.Size(140, 19);
            CHK_SelectAll_StatsSize.Text = "Select all below";
            CHK_SelectAll_StatsSize.Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold);
            CHK_SelectAll_StatsSize.UseVisualStyleBackColor = true;
            //
            // CHK_MaxIVs
            //
            CHK_MaxIVs.AutoSize = true;
            CHK_MaxIVs.Location = new System.Drawing.Point(14, 47);
            CHK_MaxIVs.Name = "CHK_MaxIVs";
            CHK_MaxIVs.Size = new System.Drawing.Size(380, 19);
            CHK_MaxIVs.Text = "Set all IVs to 31";
            CHK_MaxIVs.UseVisualStyleBackColor = true;
            //
            // CHK_OptimizeIVs
            //
            CHK_OptimizeIVs.AutoSize = true;
            CHK_OptimizeIVs.Location = new System.Drawing.Point(14, 72);
            CHK_OptimizeIVs.Name = "CHK_OptimizeIVs";
            CHK_OptimizeIVs.Size = new System.Drawing.Size(380, 19);
            CHK_OptimizeIVs.Text = "Optimize IVs (best legal, prioritizing highest base stats)";
            CHK_OptimizeIVs.UseVisualStyleBackColor = true;
            //
            // CHK_MaxPP
            //
            CHK_MaxPP.AutoSize = true;
            CHK_MaxPP.Location = new System.Drawing.Point(14, 97);
            CHK_MaxPP.Name = "CHK_MaxPP";
            CHK_MaxPP.Size = new System.Drawing.Size(380, 19);
            CHK_MaxPP.Text = "Set PP Ups to max (all moves)";
            CHK_MaxPP.UseVisualStyleBackColor = true;
            //
            // CHK_MaxSize
            //
            CHK_MaxSize.AutoSize = true;
            CHK_MaxSize.Location = new System.Drawing.Point(14, 122);
            CHK_MaxSize.Name = "CHK_MaxSize";
            CHK_MaxSize.Size = new System.Drawing.Size(380, 19);
            CHK_MaxSize.Text = "Max size -- Scale, and Height/Weight independently where Scale is fixed (Gen8+ only)";
            CHK_MaxSize.UseVisualStyleBackColor = true;
            //
            // CHK_AlignSize
            //
            CHK_AlignSize.AutoSize = true;
            CHK_AlignSize.Location = new System.Drawing.Point(14, 147);
            CHK_AlignSize.Name = "CHK_AlignSize";
            CHK_AlignSize.Size = new System.Drawing.Size(380, 19);
            CHK_AlignSize.Text = "Align Height/Weight to Scale (Gen9, HOME does this on import)";
            CHK_AlignSize.UseVisualStyleBackColor = true;
            //
            // GB_Repairs
            //
            GB_Repairs.AutoSize = true;
            GB_Repairs.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            GB_Repairs.Location = new System.Drawing.Point(12, 602);
            GB_Repairs.MinimumSize = new System.Drawing.Size(416, 382);
            GB_Repairs.Name = "GB_Repairs";
            GB_Repairs.Size = new System.Drawing.Size(416, 382);
            GB_Repairs.TabStop = false;
            GB_Repairs.Text = "Legality Repairs";
            //
            // CHK_SelectAll_Repairs
            //
            CHK_SelectAll_Repairs.AutoSize = true;
            CHK_SelectAll_Repairs.Location = new System.Drawing.Point(10, 22);
            CHK_SelectAll_Repairs.Name = "CHK_SelectAll_Repairs";
            CHK_SelectAll_Repairs.Size = new System.Drawing.Size(140, 19);
            CHK_SelectAll_Repairs.Text = "Select all below";
            CHK_SelectAll_Repairs.Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold);
            CHK_SelectAll_Repairs.UseVisualStyleBackColor = true;
            //
            // CHK_FixMoves
            //
            CHK_FixMoves.AutoSize = true;
            CHK_FixMoves.Location = new System.Drawing.Point(14, 47);
            CHK_FixMoves.Name = "CHK_FixMoves";
            CHK_FixMoves.Size = new System.Drawing.Size(380, 19);
            CHK_FixMoves.Text = "Auto-fix illegal movesets";
            CHK_FixMoves.UseVisualStyleBackColor = true;
            //
            // CHK_FixTrashMemory
            //
            CHK_FixTrashMemory.AutoSize = true;
            CHK_FixTrashMemory.Location = new System.Drawing.Point(14, 72);
            CHK_FixTrashMemory.Name = "CHK_FixTrashMemory";
            CHK_FixTrashMemory.Size = new System.Drawing.Size(380, 19);
            CHK_FixTrashMemory.Text = "Fix trash bytes / stale Handling Trainer memory";
            CHK_FixTrashMemory.UseVisualStyleBackColor = true;
            //
            // CHK_FixOTMemory
            //
            CHK_FixOTMemory.AutoSize = true;
            CHK_FixOTMemory.Location = new System.Drawing.Point(14, 97);
            CHK_FixOTMemory.Name = "CHK_FixOTMemory";
            CHK_FixOTMemory.Size = new System.Drawing.Size(380, 19);
            CHK_FixOTMemory.Text = "Fix OT history: memory + event friendship (HOME-safe)";
            CHK_FixOTMemory.UseVisualStyleBackColor = true;
            //
            // CHK_FixFishy
            //
            CHK_FixFishy.AutoSize = true;
            CHK_FixFishy.Location = new System.Drawing.Point(14, 122);
            CHK_FixFishy.Name = "CHK_FixFishy";
            CHK_FixFishy.Size = new System.Drawing.Size(380, 19);
            CHK_FixFishy.Text = "Fix \"Fishy\" warnings (EVs / EXP / nickname flag)";
            CHK_FixFishy.UseVisualStyleBackColor = true;
            //
            // CHK_FixTransferNature
            //
            CHK_FixTransferNature.AutoSize = true;
            CHK_FixTransferNature.Location = new System.Drawing.Point(14, 147);
            CHK_FixTransferNature.Name = "CHK_FixTransferNature";
            CHK_FixTransferNature.Size = new System.Drawing.Size(380, 19);
            CHK_FixTransferNature.Text = "Fix VC transfer Nature (Gen1/2 to Gen7 EXP mismatch)";
            CHK_FixTransferNature.UseVisualStyleBackColor = true;
            //
            // CHK_FixTransferSideFields
            //
            CHK_FixTransferSideFields.AutoSize = true;
            CHK_FixTransferSideFields.Location = new System.Drawing.Point(14, 172);
            CHK_FixTransferSideFields.Name = "CHK_FixTransferSideFields";
            CHK_FixTransferSideFields.Size = new System.Drawing.Size(380, 19);
            CHK_FixTransferSideFields.Text = "Fix legacy transfer EC / Nature / Hidden Ability";
            CHK_FixTransferSideFields.UseVisualStyleBackColor = true;
            //
            // CHK_SquareVCShiny
            //
            CHK_SquareVCShiny.AutoSize = true;
            CHK_SquareVCShiny.Location = new System.Drawing.Point(14, 197);
            CHK_SquareVCShiny.Name = "CHK_SquareVCShiny";
            CHK_SquareVCShiny.Size = new System.Drawing.Size(380, 19);
            CHK_SquareVCShiny.Text = "Make VC transfer shinies Square (clears Star warning)";
            CHK_SquareVCShiny.UseVisualStyleBackColor = true;
            //
            // CHK_FixBattleForms
            //
            CHK_FixBattleForms.AutoSize = true;
            CHK_FixBattleForms.Location = new System.Drawing.Point(14, 222);
            CHK_FixBattleForms.Name = "CHK_FixBattleForms";
            CHK_FixBattleForms.Size = new System.Drawing.Size(380, 19);
            CHK_FixBattleForms.Text = "Revert battle-only forms (Mega / Primal / Pirouette)";
            CHK_FixBattleForms.UseVisualStyleBackColor = true;
            //
            // CHK_FixFakeEvent
            //
            CHK_FixFakeEvent.AutoSize = true;
            CHK_FixFakeEvent.Location = new System.Drawing.Point(14, 247);
            CHK_FixFakeEvent.Name = "CHK_FixFakeEvent";
            CHK_FixFakeEvent.Size = new System.Drawing.Size(380, 19);
            CHK_FixFakeEvent.Text = "Fix fake event data (Fateful flag + invalid ribbons)";
            CHK_FixFakeEvent.UseVisualStyleBackColor = true;
            //
            // CHK_RepairMet
            //
            CHK_RepairMet.AutoSize = true;
            CHK_RepairMet.Location = new System.Drawing.Point(14, 272);
            CHK_RepairMet.Name = "CHK_RepairMet";
            CHK_RepairMet.Size = new System.Drawing.Size(380, 19);
            CHK_RepairMet.Text = "Repair mismatched met level/location to a real encounter";
            CHK_RepairMet.UseVisualStyleBackColor = true;
            //
            // CHK_Rehome
            //
            CHK_Rehome.AutoSize = true;
            CHK_Rehome.Location = new System.Drawing.Point(14, 297);
            CHK_Rehome.Name = "CHK_Rehome";
            CHK_Rehome.Size = new System.Drawing.Size(380, 19);
            CHK_Rehome.Text = "Re-home fake events to a real wild encounter (drops gift identity)";
            CHK_Rehome.UseVisualStyleBackColor = true;
            //
            // CHK_FixTera
            //
            CHK_FixTera.AutoSize = true;
            CHK_FixTera.Location = new System.Drawing.Point(14, 322);
            CHK_FixTera.Name = "CHK_FixTera";
            CHK_FixTera.Size = new System.Drawing.Size(380, 19);
            CHK_FixTera.Text = "Fix mismatched Tera Type (transferred Pokemon)";
            CHK_FixTera.UseVisualStyleBackColor = true;
            //
            // CHK_AutoLegalize
            //
            CHK_AutoLegalize.AutoSize = true;
            CHK_AutoLegalize.Location = new System.Drawing.Point(14, 347);
            CHK_AutoLegalize.Name = "CHK_AutoLegalize";
            CHK_AutoLegalize.Size = new System.Drawing.Size(380, 19);
            CHK_AutoLegalize.Text = "Auto-enforce legality (regenerate illegal Pokemon)";
            CHK_AutoLegalize.UseVisualStyleBackColor = true;
            //
            // GB_Origin
            //
            GB_Origin.AutoSize = true;
            GB_Origin.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            GB_Origin.Location = new System.Drawing.Point(12, 996);
            GB_Origin.MinimumSize = new System.Drawing.Size(416, 182);
            GB_Origin.Name = "GB_Origin";
            GB_Origin.Size = new System.Drawing.Size(416, 182);
            GB_Origin.TabStop = false;
            GB_Origin.Text = "Origin && HOME";
            //
            // CHK_SelectAll_Origin
            //
            CHK_SelectAll_Origin.AutoSize = true;
            CHK_SelectAll_Origin.Location = new System.Drawing.Point(10, 22);
            CHK_SelectAll_Origin.Name = "CHK_SelectAll_Origin";
            CHK_SelectAll_Origin.Size = new System.Drawing.Size(140, 19);
            CHK_SelectAll_Origin.Text = "Select all below";
            CHK_SelectAll_Origin.Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold);
            CHK_SelectAll_Origin.UseVisualStyleBackColor = true;
            //
            // CHK_NativeEgg
            //
            CHK_NativeEgg.AutoSize = true;
            CHK_NativeEgg.Location = new System.Drawing.Point(14, 47);
            CHK_NativeEgg.Name = "CHK_NativeEgg";
            CHK_NativeEgg.Size = new System.Drawing.Size(380, 19);
            CHK_NativeEgg.Text = "Re-origin foreign Pokemon as a native egg (drops HOME tracker need)";
            CHK_NativeEgg.UseVisualStyleBackColor = true;
            //
            // CHK_SyncDates
            //
            CHK_SyncDates.AutoSize = true;
            CHK_SyncDates.Location = new System.Drawing.Point(14, 72);
            CHK_SyncDates.Name = "CHK_SyncDates";
            CHK_SyncDates.Size = new System.Drawing.Size(380, 19);
            CHK_SyncDates.Text = "Match Met Date to Egg Met Date";
            CHK_SyncDates.UseVisualStyleBackColor = true;
            //
            // CHK_SquareAll
            //
            CHK_SquareAll.AutoSize = true;
            CHK_SquareAll.Location = new System.Drawing.Point(14, 97);
            CHK_SquareAll.Name = "CHK_SquareAll";
            CHK_SquareAll.Size = new System.Drawing.Size(380, 19);
            CHK_SquareAll.Text = "Convert Star shinies to Square (only where it stays legal)";
            CHK_SquareAll.UseVisualStyleBackColor = true;
            //
            // CHK_ClearTracker
            //
            CHK_ClearTracker.AutoSize = true;
            CHK_ClearTracker.Location = new System.Drawing.Point(14, 122);
            CHK_ClearTracker.Name = "CHK_ClearTracker";
            CHK_ClearTracker.Size = new System.Drawing.Size(380, 19);
            CHK_ClearTracker.Text = "Clear HOME Tracker (re-register on next upload)";
            CHK_ClearTracker.UseVisualStyleBackColor = true;
            //
            // CHK_RegenTrackerEC
            //
            CHK_RegenTrackerEC.AutoSize = true;
            CHK_RegenTrackerEC.Location = new System.Drawing.Point(14, 147);
            CHK_RegenTrackerEC.Name = "CHK_RegenTrackerEC";
            CHK_RegenTrackerEC.Size = new System.Drawing.Size(380, 19);
            CHK_RegenTrackerEC.Text = "Regenerate PID / Tracker / EC (break clone collisions)";
            CHK_RegenTrackerEC.UseVisualStyleBackColor = true;
            //
            // L_LegalNotice
            //
            L_LegalNotice.Location = new System.Drawing.Point(12, 1190);
            L_LegalNotice.Name = "L_LegalNotice";
            L_LegalNotice.Size = new System.Drawing.Size(416, 60);
            L_LegalNotice.Text = "Each checked edit is applied one Pok\u00e9mon at a time; any Pok\u00e9mon that would become illegal as a result keeps its original value instead. Optimize IVs and the seed-search parts of Max size can be slow: for encounters with correlated PID/IVs they retry many times per Pok\u00e9mon.";
            //
            // PB_Progress
            //
            PB_Progress.Location = new System.Drawing.Point(12, 10);
            PB_Progress.Name = "PB_Progress";
            PB_Progress.Size = new System.Drawing.Size(268, 20);
            PB_Progress.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            PB_Progress.Visible = false;
            //
            // B_Cancel
            //
            B_Cancel.Location = new System.Drawing.Point(286, 8);
            B_Cancel.Name = "B_Cancel";
            B_Cancel.Size = new System.Drawing.Size(74, 24);
            B_Cancel.Text = "Cancel";
            B_Cancel.UseVisualStyleBackColor = true;
            B_Cancel.Visible = false;
            B_Cancel.Click += B_Cancel_Click;
            //
            // B_CheckClones
            //
            B_CheckClones.Location = new System.Drawing.Point(12, 42);
            B_CheckClones.Name = "B_CheckClones";
            B_CheckClones.Size = new System.Drawing.Size(150, 27);
            B_CheckClones.Text = "Check for Clones";
            B_CheckClones.UseVisualStyleBackColor = true;
            B_CheckClones.Click += B_CheckClones_Click;
            //
            // B_HomeCheck
            //
            B_HomeCheck.Location = new System.Drawing.Point(168, 42);
            B_HomeCheck.Name = "B_HomeCheck";
            B_HomeCheck.Size = new System.Drawing.Size(260, 27);
            B_HomeCheck.Text = "Check HOME Transfer Risk";
            B_HomeCheck.UseVisualStyleBackColor = true;
            B_HomeCheck.Click += B_HomeCheck_Click;
            //
            // B_SortBoxes
            //
            B_SortBoxes.Location = new System.Drawing.Point(12, 76);
            B_SortBoxes.Name = "B_SortBoxes";
            B_SortBoxes.Size = new System.Drawing.Size(416, 27);
            B_SortBoxes.Text = "Sort All Boxes by National Dex #";
            B_SortBoxes.UseVisualStyleBackColor = true;
            B_SortBoxes.Click += B_SortBoxes_Click;
            //
            // B_Run
            //
            B_Run.Location = new System.Drawing.Point(252, 110);
            B_Run.Name = "B_Run";
            B_Run.Size = new System.Drawing.Size(88, 27);
            B_Run.Text = "Run";
            B_Run.UseVisualStyleBackColor = true;
            B_Run.Click += B_Run_Click;
            //
            // B_Close
            //
            B_Close.Location = new System.Drawing.Point(340, 110);
            B_Close.Name = "B_Close";
            B_Close.Size = new System.Drawing.Size(88, 27);
            B_Close.Text = "Close";
            B_Close.UseVisualStyleBackColor = true;
            B_Close.Click += B_Close_Click;
            //
            // Panel_Footer
            //
            Panel_Footer.Dock = System.Windows.Forms.DockStyle.Bottom;
            Panel_Footer.Name = "Panel_Footer";
            Panel_Footer.Size = new System.Drawing.Size(460, 147);
            //
            // Panel_Scroll
            //
            Panel_Scroll.AutoScroll = true;
            Panel_Scroll.Dock = System.Windows.Forms.DockStyle.Fill;
            Panel_Scroll.Name = "Panel_Scroll";
            Panel_Scroll.Size = new System.Drawing.Size(460, 1260);

            //
            // group children
            //
            GB_Filters.Controls.Add(CHK_FilterIllegalOnly);
            GB_Filters.Controls.Add(CHK_FilterShinyOnly);
            GB_Filters.Controls.Add(CHK_FilterSpecies);
            GB_Filters.Controls.Add(CB_FilterSpecies);
            GB_Filters.Controls.Add(CHK_FilterOrigin);
            GB_Filters.Controls.Add(CB_FilterOrigin);
            GB_Filters.Controls.Add(CHK_FilterGiftOrigin);
            GB_Filters.Controls.Add(CHK_SkipHomeTracked);
            GB_SetValues.Controls.Add(CHK_SelectAll_SetValues);
            GB_SetValues.Controls.Add(CHK_Ball);
            GB_SetValues.Controls.Add(CB_Ball);
            GB_SetValues.Controls.Add(CHK_MetLocation);
            GB_SetValues.Controls.Add(CB_MetLocation);
            GB_SetValues.Controls.Add(CHK_TrainerName);
            GB_SetValues.Controls.Add(TB_TrainerName);
            GB_SetValues.Controls.Add(CHK_Shiny);
            GB_SetValues.Controls.Add(RB_ShinyOn);
            GB_SetValues.Controls.Add(RB_ShinyOff);
            GB_SetValues.Controls.Add(CHK_PreferSquare);
            GB_SetValues.Controls.Add(CHK_NaturePreset);
            GB_SetValues.Controls.Add(CB_NaturePreset);
            GB_StatsSize.Controls.Add(CHK_SelectAll_StatsSize);
            GB_StatsSize.Controls.Add(CHK_MaxIVs);
            GB_StatsSize.Controls.Add(CHK_OptimizeIVs);
            GB_StatsSize.Controls.Add(CHK_MaxPP);
            GB_StatsSize.Controls.Add(CHK_MaxSize);
            GB_StatsSize.Controls.Add(CHK_AlignSize);
            GB_Repairs.Controls.Add(CHK_SelectAll_Repairs);
            GB_Repairs.Controls.Add(CHK_FixMoves);
            GB_Repairs.Controls.Add(CHK_FixTrashMemory);
            GB_Repairs.Controls.Add(CHK_FixOTMemory);
            GB_Repairs.Controls.Add(CHK_FixFishy);
            GB_Repairs.Controls.Add(CHK_FixTransferNature);
            GB_Repairs.Controls.Add(CHK_FixTransferSideFields);
            GB_Repairs.Controls.Add(CHK_SquareVCShiny);
            GB_Repairs.Controls.Add(CHK_FixBattleForms);
            GB_Repairs.Controls.Add(CHK_FixFakeEvent);
            GB_Repairs.Controls.Add(CHK_RepairMet);
            GB_Repairs.Controls.Add(CHK_Rehome);
            GB_Repairs.Controls.Add(CHK_FixTera);
            GB_Repairs.Controls.Add(CHK_AutoLegalize);
            GB_Origin.Controls.Add(CHK_SelectAll_Origin);
            GB_Origin.Controls.Add(CHK_NativeEgg);
            GB_Origin.Controls.Add(CHK_SyncDates);
            GB_Origin.Controls.Add(CHK_SquareAll);
            GB_Origin.Controls.Add(CHK_ClearTracker);
            GB_Origin.Controls.Add(CHK_RegenTrackerEC);

            //
            // Panel_Footer children
            //
            Panel_Footer.Controls.Add(PB_Progress);
            Panel_Footer.Controls.Add(B_Cancel);
            Panel_Footer.Controls.Add(B_CheckClones);
            Panel_Footer.Controls.Add(B_HomeCheck);
            Panel_Footer.Controls.Add(B_SortBoxes);
            Panel_Footer.Controls.Add(B_Run);
            Panel_Footer.Controls.Add(B_Close);

            //
            // Panel_Scroll children
            //
            Panel_Scroll.Controls.Add(L_Scope);
            Panel_Scroll.Controls.Add(RB_Boxes);
            Panel_Scroll.Controls.Add(RB_Party);
            Panel_Scroll.Controls.Add(RB_Both);
            Panel_Scroll.Controls.Add(GB_Filters);
            Panel_Scroll.Controls.Add(GB_SetValues);
            Panel_Scroll.Controls.Add(GB_StatsSize);
            Panel_Scroll.Controls.Add(GB_Repairs);
            Panel_Scroll.Controls.Add(GB_Origin);
            Panel_Scroll.Controls.Add(L_LegalNotice);

            //
            // SAV_BulkQoL
            //
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            ClientSize = new System.Drawing.Size(460, 620);
            Controls.Add(Panel_Footer);
            Controls.Add(Panel_Scroll);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            Icon = Properties.Resources.Icon;
            MaximizeBox = true;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(400, 300);
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
        private System.Windows.Forms.GroupBox GB_Filters;
        private System.Windows.Forms.CheckBox CHK_FilterIllegalOnly;
        private System.Windows.Forms.CheckBox CHK_FilterShinyOnly;
        private System.Windows.Forms.CheckBox CHK_FilterSpecies;
        private System.Windows.Forms.ComboBox CB_FilterSpecies;
        private System.Windows.Forms.CheckBox CHK_FilterOrigin;
        private System.Windows.Forms.ComboBox CB_FilterOrigin;
        private System.Windows.Forms.CheckBox CHK_FilterGiftOrigin;
        private System.Windows.Forms.CheckBox CHK_SkipHomeTracked;
        private System.Windows.Forms.GroupBox GB_SetValues;
        private System.Windows.Forms.CheckBox CHK_SelectAll_SetValues;
        private System.Windows.Forms.CheckBox CHK_Ball;
        private System.Windows.Forms.ComboBox CB_Ball;
        private System.Windows.Forms.CheckBox CHK_MetLocation;
        private System.Windows.Forms.ComboBox CB_MetLocation;
        private System.Windows.Forms.CheckBox CHK_TrainerName;
        private System.Windows.Forms.TextBox TB_TrainerName;
        private System.Windows.Forms.CheckBox CHK_Shiny;
        private System.Windows.Forms.RadioButton RB_ShinyOn;
        private System.Windows.Forms.RadioButton RB_ShinyOff;
        private System.Windows.Forms.CheckBox CHK_PreferSquare;
        private System.Windows.Forms.CheckBox CHK_NaturePreset;
        private System.Windows.Forms.ComboBox CB_NaturePreset;
        private System.Windows.Forms.GroupBox GB_StatsSize;
        private System.Windows.Forms.CheckBox CHK_SelectAll_StatsSize;
        private System.Windows.Forms.CheckBox CHK_MaxIVs;
        private System.Windows.Forms.CheckBox CHK_OptimizeIVs;
        private System.Windows.Forms.CheckBox CHK_MaxPP;
        private System.Windows.Forms.CheckBox CHK_MaxSize;
        private System.Windows.Forms.CheckBox CHK_AlignSize;
        private System.Windows.Forms.GroupBox GB_Repairs;
        private System.Windows.Forms.CheckBox CHK_SelectAll_Repairs;
        private System.Windows.Forms.CheckBox CHK_FixMoves;
        private System.Windows.Forms.CheckBox CHK_FixTrashMemory;
        private System.Windows.Forms.CheckBox CHK_FixOTMemory;
        private System.Windows.Forms.CheckBox CHK_FixFishy;
        private System.Windows.Forms.CheckBox CHK_FixTransferNature;
        private System.Windows.Forms.CheckBox CHK_FixTransferSideFields;
        private System.Windows.Forms.CheckBox CHK_SquareVCShiny;
        private System.Windows.Forms.CheckBox CHK_FixBattleForms;
        private System.Windows.Forms.CheckBox CHK_FixFakeEvent;
        private System.Windows.Forms.CheckBox CHK_RepairMet;
        private System.Windows.Forms.CheckBox CHK_Rehome;
        private System.Windows.Forms.CheckBox CHK_FixTera;
        private System.Windows.Forms.CheckBox CHK_AutoLegalize;
        private System.Windows.Forms.GroupBox GB_Origin;
        private System.Windows.Forms.CheckBox CHK_SelectAll_Origin;
        private System.Windows.Forms.CheckBox CHK_NativeEgg;
        private System.Windows.Forms.CheckBox CHK_SyncDates;
        private System.Windows.Forms.CheckBox CHK_SquareAll;
        private System.Windows.Forms.CheckBox CHK_ClearTracker;
        private System.Windows.Forms.CheckBox CHK_RegenTrackerEC;
        private System.Windows.Forms.Label L_LegalNotice;
        private System.Windows.Forms.ProgressBar PB_Progress;
        private System.Windows.Forms.Button B_Cancel;
        private System.Windows.Forms.Button B_CheckClones;
        private System.Windows.Forms.Button B_HomeCheck;
        private System.Windows.Forms.Button B_SortBoxes;
        private System.Windows.Forms.Button B_Run;
        private System.Windows.Forms.Button B_Close;
        private System.Windows.Forms.Panel Panel_Footer;
        private System.Windows.Forms.Panel Panel_Scroll;
    }
}
