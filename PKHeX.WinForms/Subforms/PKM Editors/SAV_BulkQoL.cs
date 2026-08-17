using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Core.AutoMod;

namespace PKHeX.WinForms;

/// <summary>
/// Quality-of-life dialog for applying a single common edit (Ball, Met Location, Shiny state) across every
/// Pokémon in the save file's boxes and/or party at once. Each edit is only kept if the Pokémon remains a
/// legal encounter afterward; see <see cref="BulkQoLEditor"/> in PKHeX.Core for the guard logic.
/// </summary>
public partial class SAV_BulkQoL : Form
{
    private readonly SaveFile SAV;

    public SAV_BulkQoL(SaveFile sav)
    {
        InitializeComponent();
        WinFormsUtil.TranslateInterface(this, Main.CurrentLanguage);
        SAV = sav;

        RB_Boxes.Checked = true;
        SetupComboBoxes();
    }

    private void SetupComboBoxes()
    {
        var filtered = GameInfo.FilteredSources;

        CB_Ball.InitializeBinding();
        CB_Ball.DataSource = new BindingSource(filtered.Balls, string.Empty);
        CB_Ball.SelectedValue = (int)Ball.Poke;

        CB_MetLocation.InitializeBinding();
        var metList = GameInfo.GetLocationList(SAV.Version, SAV.Context, egg: false);
        CB_MetLocation.DataSource = new BindingSource(metList, string.Empty);

        RB_ShinyOn.Checked = true;

        CB_NaturePreset.DataSource = Enum.GetValues<BulkQoLEditor.NatureEVPreset>();
        CB_NaturePreset.SelectedIndex = 0;
    }

    /// <summary>
    /// Snapshot of every checkbox/combo value the run needs, captured on the UI thread before work starts --
    /// the actual run happens on a background thread, and WinForms controls aren't safe to touch from there.
    /// </summary>
    private readonly record struct Plan(
        bool Ball, byte BallValue,
        bool MetLocation, ushort MetLocationValue,
        bool Shiny, bool ShinyValue,
        bool MaxIVs,
        bool NaturePreset, BulkQoLEditor.NatureEVPreset NaturePresetValue,
        bool OptimizeIVs,
        bool MaxPP,
        bool FixMoves,
        bool AutoLegalize);

    private Plan CapturePlan() => new(
        CHK_Ball.Checked, (byte)WinFormsUtil.GetIndex(CB_Ball),
        CHK_MetLocation.Checked, (ushort)WinFormsUtil.GetIndex(CB_MetLocation),
        CHK_Shiny.Checked, RB_ShinyOn.Checked,
        CHK_MaxIVs.Checked,
        CHK_NaturePreset.Checked, (BulkQoLEditor.NatureEVPreset)CB_NaturePreset.SelectedItem!,
        CHK_OptimizeIVs.Checked,
        CHK_MaxPP.Checked,
        CHK_FixMoves.Checked,
        CHK_AutoLegalize.Checked);

    private async void B_Run_Click(object sender, EventArgs e)
    {
        var slots = GetScopedSlots();
        var eligible = slots.Where(IsEligible).ToList();
        if (eligible.Count == 0)
        {
            WinFormsUtil.Alert("No editable slots in the selected scope.");
            return;
        }

        var plan = CapturePlan();
        if (!HasAnyEditSelected(plan))
        {
            WinFormsUtil.Alert("Select at least one edit to apply.");
            return;
        }

        // Auto-enforce legality and Optimize IVs can each take a real amount of time across a whole box
        // (correlated-PID entities retry regeneration many times). Run off the UI thread so the window stays
        // responsive instead of appearing to hang, matching how the rest of the app runs long batch edits.
        B_Run.Enabled = false;
        B_Close.Enabled = false;
        UseWaitCursor = true;
        Cursor.Current = Cursors.WaitCursor;
        List<string> lines;
        try
        {
            lines = await Task.Run(() => RunEdits(eligible, SAV, plan)).ConfigureAwait(true);
        }
        finally
        {
            UseWaitCursor = false;
            B_Run.Enabled = true;
            B_Close.Enabled = true;
        }

        foreach (var slot in eligible)
            slot.Source.WriteTo(SAV, slot.Entity, EntityImportSettings.None);

        WinFormsUtil.Alert(lines.ToArray());
    }

    private static bool HasAnyEditSelected(Plan plan) =>
        plan.Ball || plan.MetLocation || plan.Shiny || plan.MaxIVs || plan.NaturePreset
        || plan.OptimizeIVs || plan.MaxPP || plan.FixMoves || plan.AutoLegalize;

    /// <summary>
    /// Runs every checked edit over <paramref name="eligible"/>, in a fixed order chosen so each edit sees the
    /// results of the ones before it (moves fixed before auto-legalize gets less to do; auto-legalize runs
    /// last so it isn't immediately undone by an earlier edit). Touches only PKM/SaveFile data -- safe to run
    /// off the UI thread.
    /// </summary>
    private static List<string> RunEdits(List<SlotCache> eligible, SaveFile sav, Plan plan)
    {
        var lines = new List<string>();

        if (plan.Ball)
        {
            var result = BulkQoLEditor.SetBallForAll(eligible.Select(s => s.Entity), plan.BallValue);
            lines.Add(Describe("Ball", result));
        }
        if (plan.MetLocation)
        {
            var result = BulkQoLEditor.SetMetLocationForAll(eligible.Select(s => s.Entity), plan.MetLocationValue);
            lines.Add(Describe("Met Location", result));
        }
        if (plan.Shiny)
        {
            var result = BulkQoLEditor.SetShinyForAll(eligible.Select(s => s.Entity), plan.ShinyValue);
            lines.Add(Describe(plan.ShinyValue ? "Shiny" : "Not Shiny", result));
        }
        if (plan.MaxIVs)
        {
            var result = BulkQoLEditor.SetMaxIVsForAll(eligible.Select(s => s.Entity));
            lines.Add(Describe("All IVs to 31", result));
        }
        if (plan.NaturePreset)
        {
            var result = BulkQoLEditor.SetNatureEVPresetForAll(eligible.Select(s => s.Entity), plan.NaturePresetValue);
            lines.Add($"Nature+EVs ({plan.NaturePresetValue}): {result.Modified} applied, {result.SkippedIllegal} skipped (would be illegal), {result.AlreadyLegal} skipped (Gen 3/4), {result.SkippedInvalid} skipped (empty)");
        }
        if (plan.OptimizeIVs)
        {
            var result = IVOptimizer.OptimizeAll(eligible.Select(s => s.Entity), sav);
            lines.Add($"Optimize IVs: {result.Improved} improved, {result.AlreadyOptimal} already optimal, {result.Failed} no legal spread found, {result.SkippedInvalid} skipped (empty)");
        }
        if (plan.MaxPP)
        {
            var result = BulkQoLEditor.SetMaxPPUpsForAll(eligible.Select(s => s.Entity));
            lines.Add(Describe("PP Ups to max", result));
        }
        if (plan.FixMoves)
        {
            // Run before auto-legalize: fixing moves first gives the legalizer less (or nothing) left to do.
            var result = BulkQoLEditor.SetLegalMovesForAll(eligible.Select(s => s.Entity));
            lines.Add($"Auto-fix illegal movesets: {result.Modified} fixed, {result.SkippedIllegal} could not be fixed, {result.AlreadyLegal} already legal, {result.SkippedInvalid} skipped (empty)");
        }
        if (plan.AutoLegalize)
        {
            // Run this last: it should legalize whatever the prior edits left behind, not get immediately
            // undone by them (e.g. a ball/met-location change applied to an entity it just legalized).
            var result = BulkAutoLegalize.LegalizeAll(eligible.Select(s => s.Entity), sav);
            lines.Add($"Auto-enforce legality: {result.Modified} regenerated, {result.Failed} could not be legalized, {result.AlreadyLegal} already legal, {result.SkippedInvalid} skipped (empty)");
        }

        return lines;
    }

    private static string Describe(string label, BulkQoLEditor.BulkEditResult result) =>
        $"{label}: {result.Modified} modified, {result.SkippedIllegal} skipped (would be illegal), {result.SkippedInvalid} skipped (empty)";

    private List<SlotCache> GetScopedSlots()
    {
        var data = new List<SlotCache>();
        if (RB_Party.Checked || RB_Both.Checked)
            SlotInfoLoader.AddPartyData(SAV, data);
        if (RB_Boxes.Checked || RB_Both.Checked)
            SlotInfoLoader.AddBoxData(SAV, data);
        return data;
    }

    private bool IsEligible(SlotCache slot) =>
        slot.Source is not SlotInfoBox info || !SAV.GetBoxSlotFlags(info.Box, info.Slot).IsOverwriteProtected();

    private void B_Close_Click(object sender, EventArgs e) => Close();
}
