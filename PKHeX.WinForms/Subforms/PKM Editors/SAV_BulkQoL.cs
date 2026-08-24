using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private CancellationTokenSource? _cts;

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

        CB_FilterSpecies.InitializeBinding();
        CB_FilterSpecies.DataSource = new BindingSource(filtered.Species, string.Empty);
    }

    /// <summary>
    /// Snapshot of every checkbox/combo value the run needs, captured on the UI thread before work starts --
    /// the actual run happens on a background thread, and WinForms controls aren't safe to touch from there.
    /// </summary>
    private readonly record struct Plan(
        bool FilterIllegalOnly,
        bool FilterShinyOnly,
        bool FilterSpecies, ushort FilterSpeciesValue,
        bool FilterGiftOrigin,
        bool FilterSkipHomeTracked,
        bool Ball, byte BallValue,
        bool MetLocation, ushort MetLocationValue,
        bool Shiny, bool ShinyValue, bool PreferSquare,
        bool MaxIVs,
        bool MaxSize,
        bool AlignSize,
        bool NaturePreset, BulkQoLEditor.NatureEVPreset NaturePresetValue,
        bool OptimizeIVs,
        bool MaxPP,
        bool FixMoves,
        bool FixTrashMemory,
        bool RegenTrackerEC,
        bool AutoLegalize);

    private Plan CapturePlan() => new(
        CHK_FilterIllegalOnly.Checked,
        CHK_FilterShinyOnly.Checked,
        CHK_FilterSpecies.Checked, (ushort)WinFormsUtil.GetIndex(CB_FilterSpecies),
        CHK_FilterGiftOrigin.Checked,
        CHK_SkipHomeTracked.Checked,
        CHK_Ball.Checked, (byte)WinFormsUtil.GetIndex(CB_Ball),
        CHK_MetLocation.Checked, (ushort)WinFormsUtil.GetIndex(CB_MetLocation),
        CHK_Shiny.Checked, RB_ShinyOn.Checked, CHK_PreferSquare.Checked,
        CHK_MaxIVs.Checked,
        CHK_MaxSize.Checked,
        CHK_AlignSize.Checked,
        CHK_NaturePreset.Checked, (BulkQoLEditor.NatureEVPreset)CB_NaturePreset.SelectedItem!,
        CHK_OptimizeIVs.Checked,
        CHK_MaxPP.Checked,
        CHK_FixMoves.Checked,
        CHK_FixTrashMemory.Checked,
        CHK_RegenTrackerEC.Checked,
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
        ShowBusy();
        var ct = _cts!.Token;
        List<string> lines;
        try
        {
            lines = await Task.Run(() => RunEdits(eligible, SAV, plan, ct)).ConfigureAwait(true);
        }
        finally
        {
            HideBusy();
        }

        // Anything a cancelled run already applied and kept legal stays applied -- guarded edits commit as they
        // go, so there's nothing "in progress" to roll back; write back whatever eligible slots hold now.
        foreach (var slot in eligible)
            slot.Source.WriteTo(SAV, slot.Entity, EntityImportSettings.None);

        WinFormsUtil.Alert(lines.ToArray());
    }

    /// <summary>
    /// Scans the whole save (not just the current scope/filters -- a clone can be duplicated across boxes and
    /// party) for signs of duplicated/cloned Pokémon: a shared HOME Tracker GUID, or a shared raw PID/Encryption
    /// Constant. Also reports how many Pokémon in the save are Mystery Gift-origin as an FYI -- those carry a
    /// separate, locally-unfixable HOME rejection risk (see <see cref="HomeRiskAnalyzer"/>). Offers to
    /// auto-regenerate the Tracker/EC of the flagged duplicate copies afterward. See <see cref="CloneDetector"/>.
    /// </summary>
    private async void B_CheckClones_Click(object sender, EventArgs e)
    {
        ShowBusy();
        var ct = _cts!.Token;
        IReadOnlyList<CloneDetector.Finding> findings;
        int giftOriginCount;
        try
        {
            var work = Task.Run(() =>
            {
                var f = CloneDetector.FindLikelyClones(SAV);
                var g = HomeRiskAnalyzer.CountGiftOrigin(GetAllSlots().Select(s => s.Entity));
                return (Findings: f, GiftCount: g);
            });
            var cancelTask = Task.Delay(Timeout.Infinite, ct);
            var completed = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(60)), cancelTask).ConfigureAwait(true);
            if (completed == cancelTask)
            {
                WinFormsUtil.Alert("Clone scan cancelled.",
                    "The scan itself keeps running in the background until it finishes or times out -- this just stops waiting for it.");
                return;
            }
            if (completed != work)
            {
                // Bail out rather than leave the button disabled/cursor spinning forever with no feedback --
                // most likely one specific Pokémon's LegalityAnalysis is pathologically slow to construct
                // (the same class of issue fixed for Auto-enforce Legality/Optimize IVs via search timeouts;
                // BulkAnalysis's own per-slot LegalityAnalysis construction has no such guard).
                WinFormsUtil.Alert("Clone scan timed out after 60 seconds without finishing.",
                    "This usually means one specific Pokémon in the save is pathologically slow to analyze. " +
                    "Try narrowing the scope (Party only, or a single box at a time) to isolate which one.");
                return;
            }
            (findings, giftOriginCount) = await work.ConfigureAwait(true); // already completed; rethrows if faulted
        }
        catch (Exception ex)
        {
            // A single corrupted/edge-case entity can make BulkAnalysis's construction itself throw -- surface
            // that instead of silently doing nothing, which is indistinguishable from a hang to the user.
            WinFormsUtil.Alert("Clone scan failed with an error:", ex.Message);
            return;
        }
        finally
        {
            HideBusy();
        }

        var giftNotice = giftOriginCount > 0
            ? $"FYI: {giftOriginCount} Pokémon in this save are Mystery Gift-origin. If HOME rejects one of those specifically (commonly error 999), that's a gift-authenticity signature check PKHeX can't verify or fix locally -- not a clone/tracker problem."
            : null;

        if (findings.Count == 0)
        {
            WinFormsUtil.Alert("No likely clones found -- no two Pokémon in this save share a HOME Tracker, PID, or Encryption Constant.", giftNotice);
            return;
        }

        var lines = SummarizeFindings(findings);

        var fixable = findings.Where(f => f.DuplicateSlot is not null).Select(f => f.DuplicateSlot!).ToList();
        // DistinctBy: a Pokémon involved in 3+ mutually-identical copies produces multiple findings all pointing
        // back to the same first-seen original, but each finding's *other* side is still a distinct duplicate --
        // this collects every one of those in a single pass rather than fixing only one per round.
        var distinctFixable = fixable.DistinctBy(s => s.Entity).ToList();

        var gap = Environment.NewLine + Environment.NewLine;
        var body = string.Join(gap, lines);
        if (giftNotice is not null)
            body += gap + giftNotice;
        if (distinctFixable.Count != 0)
        {
            body += gap + "Fixing regenerates the PID/HOME Tracker/Encryption Constant of the "
                 + $"{distinctFixable.Count} newly-detected duplicate(s); the first-seen original in each group "
                 + "is left untouched. Species/gender/nature/form/shininess are preserved exactly, and any "
                 + "Pokémon the change would make illegal is reverted individually.";
        }

        // Scrollable viewer rather than WinFormsUtil.Alert: a MessageBox grows past the screen and clips instead
        // of scrolling, so a save with 100+ clones was unreadable. Doubles as the confirm prompt.
        var actionText = distinctFixable.Count == 0 ? null : $"Fix {distinctFixable.Count} Duplicate(s)";
        using var viewer = new ReportViewer("Clone / Duplicate Report",
            $"{findings.Count} likely clone(s)/duplicate(s) found in {lines.Length} group(s):", body, actionText);
        var reply = viewer.ShowDialog(this);

        if (distinctFixable.Count == 0 || reply != DialogResult.Yes)
            return; // e.g. only a duplicate-gift-egg finding, which has no safe automatic fix

        // Per-entity so we learn WHICH ones the cheap path could not handle. Gen9 raid encounters derive
        // PID/EC/IVs from one seed and Encounter9RNG re-derives them exactly, so mutating the PID in place
        // always breaks the correlation -- those need a different valid seed, which only the legalizer finds.
        var stubborn = new List<SlotCache>();
        int fixedCount = 0, wasIllegal = 0, notApplicable = 0;
        foreach (var slot in distinctFixable)
        {
            var one = BulkQoLEditor.RegeneratePIDTrackerAndECForAll([slot.Entity]);
            if (one.Modified == 1) fixedCount++;
            else if (one.AlreadyIllegal == 1) wasIllegal++;
            else if (one.AlreadyLegal == 1) notApplicable++;
            else stubborn.Add(slot);
        }
        foreach (var slot in distinctFixable)
            slot.Source.WriteTo(SAV, slot.Entity, EntityImportSettings.None);

        var fixResult = new BulkQoLEditor.BulkEditResult(fixedCount, stubborn.Count, 0, notApplicable, wasIllegal);

        if (stubborn.Count != 0)
        {
            var offer = WinFormsUtil.Prompt(MessageBoxButtons.YesNo,
                $"{stubborn.Count} could not be fixed by rerolling PID/EC directly. This is expected for Gen9 raid "
                + "Pokémon, whose PID, Encryption Constant and IVs all derive from a single seed -- changing the PID "
                + "breaks that correlation, so the edit is reverted.",
                "Rebuild those via the legalizer instead? It searches for a genuinely different valid seed. This is "
                + "more invasive than the reroll: it regenerates each Pokémon from a template, so incidental details "
                + "can shift. Anything it cannot rebuild legally is left exactly as-is.");
            if (offer == DialogResult.Yes)
            {
                var forced = BulkAutoLegalize.ForceNewIdentity(stubborn.Select(s => s.Entity), SAV);
                foreach (var slot in stubborn)
                    slot.Source.WriteTo(SAV, slot.Entity, EntityImportSettings.None);
                fixResult = fixResult with
                {
                    Modified = fixResult.Modified + forced.Regenerated,
                    SkippedIllegal = forced.Failed,
                };
            }
        }

        // Re-verify immediately rather than making the user click Check for Clones again to find out whether it
        // actually worked -- a leftover count here means those specific entities failed the legality guard
        // (e.g. a genuine fixed-PID event) and need manual attention instead.
        var leftover = CloneDetector.FindLikelyClones(SAV);
        var summary = $"Regenerate PID/Tracker/EC: {fixResult.Modified} regenerated, "
                    + $"{fixResult.AlreadyIllegal} skipped (ALREADY illegal before the fix), "
                    + $"{fixResult.SkippedIllegal} skipped (the change itself would break legality), "
                    + $"{fixResult.AlreadyLegal} skipped (nothing applicable).";

        if (leftover.Count == 0)
        {
            WinFormsUtil.Alert(summary, "Re-verified: no more likely clones/duplicates in this save.");
            return;
        }

        // Anything still listed failed the legality guard (e.g. a genuine fixed-PID event encounter) and
        // needs manual attention -- show it in the scrollable viewer too rather than clipping it.
        var leftoverBody = string.Join(gap, SummarizeFindings(leftover)) + gap + (fixResult.AlreadyIllegal > 0
            ? $"{fixResult.AlreadyIllegal} of these were ALREADY illegal before any fix was attempted. A guarded "
              + "edit can never succeed on those: it only keeps a change if the Pokémon is legal afterward, which "
              + "an already-illegal Pokémon cannot be. Fix their underlying legality first (check the Legality "
              + "report on one, or try Auto-enforce legality), then re-run the clone fix."
            : "These could not be auto-fixed: regenerating their PID/EC would itself have broken legality, so "
              + "each was reverted individually and left as-is.");
        using var leftoverViewer = new ReportViewer("Clone / Duplicate Report -- Remaining",
            $"{summary}  {leftover.Count} finding(s) still remain:", leftoverBody);
        leftoverViewer.ShowDialog(this);
    }

    /// <summary>
    /// Groups pairwise findings into clusters (one line per group of mutually-identical/colliding Pokémon)
    /// instead of one line per pair -- a save with e.g. 8 identical cloned Miraidon produces 7 pairwise findings
    /// (each compared against the same first-seen original), which reads far better as one "8 copies" line.
    /// </summary>
    private static readonly HashSet<LegalityCheckResultCode> IdenticalCopyCodes =
    [
        LegalityCheckResultCode.BulkCloneDetectedTracker,
        LegalityCheckResultCode.BulkCloneDetectedDetails,
    ];

    private static string[] SummarizeFindings(IReadOnlyList<CloneDetector.Finding> findings)
    {
        // Only cluster genuine "identical copy of the same Pokémon" findings -- a PID/EC-sharing finding can
        // pair two entirely different species/individuals (suspicious, but not each other's clone), so those
        // are always reported as their own pair line rather than folded into a same-species cluster.
        var parent = new Dictionary<PKM, PKM>();
        PKM Find(PKM x)
        {
            while (parent.TryGetValue(x, out var p) && p != x)
                x = p;
            return x;
        }
        void Union(PKM a, PKM b)
        {
            parent.TryAdd(a, a);
            parent.TryAdd(b, b);
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb)
                parent[ra] = rb;
        }

        var slotByEntity = new Dictionary<PKM, SlotCache>();
        var otherLines = new List<string>();
        foreach (var f in findings)
        {
            if (IdenticalCopyCodes.Contains(f.Code) && f.Second is { } identical)
            {
                slotByEntity[f.First.Entity] = f.First;
                slotByEntity[identical.Entity] = identical;
                Union(f.First.Entity, identical.Entity);
            }
            else if (f.Second is { } second)
            {
                otherLines.Add($"{f.Description}\n  [{f.First.Identify()}]\n  [{second.Identify()}]");
            }
            else
            {
                otherLines.Add($"{f.Description}\n  [{f.First.Identify()}]");
            }
        }

        var clusters = new Dictionary<PKM, List<PKM>>();
        foreach (var entity in slotByEntity.Keys)
        {
            var root = Find(entity);
            if (!clusters.TryGetValue(root, out var members))
                clusters[root] = members = [];
            members.Add(entity);
        }

        return clusters.Values
            .OrderByDescending(g => g.Count)
            .Select(group =>
            {
                var sample = group[0];
                var shiny = sample.IsShiny ? "★ " : "";
                var name = GameInfo.Strings.specieslist[sample.Species];
                var slots = group.Select(e => "  [" + slotByEntity[e].Identify() + "]").OrderBy(s => s, StringComparer.Ordinal);
                return $"{shiny}{name} (Form {sample.Form}): {group.Count} copies share an identical PID/IVs/form --\n" + string.Join("\n", slots);
            })
            .Concat(otherLines)
            .ToArray();
    }

    /// <summary>
    /// Reports HOME-transfer risks that PKHeX's Legal/Illegal verdict does not surface: entities already
    /// registered with HOME (where any immutable-field edit invalidates their Tracker), Fishy-severity checks
    /// that don't turn the verdict red, and a few Gen9 fields PKHeX doesn't validate. Read-only.
    /// </summary>
    private async void B_HomeCheck_Click(object sender, EventArgs e)
    {
        ShowBusy();
        var ct = _cts!.Token;
        IReadOnlyList<HomeTransferPreCheck.Finding> findings;
        try
        {
            var work = Task.Run(() => HomeTransferPreCheck.Scan(SAV));
            var cancelTask = Task.Delay(Timeout.Infinite, ct);
            var completed = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(120)), cancelTask).ConfigureAwait(true);
            if (completed != work)
            {
                WinFormsUtil.Alert(completed == cancelTask
                    ? "HOME transfer check cancelled."
                    : "HOME transfer check timed out after 120 seconds. Try a smaller scope to isolate a slow entity.");
                return;
            }
            findings = await work.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert("HOME transfer check failed with an error:", ex.Message);
            return;
        }
        finally
        {
            HideBusy();
        }

        if (findings.Count == 0)
        {
            WinFormsUtil.Alert("No HOME transfer risks found.",
                "Note this only covers what can be checked locally. HOME validates against its own server records, "
                + "which PKHeX has no access to.");
            return;
        }

        // Group by category: a whole save routinely produces the same finding for hundreds of Pokémon (every
        // legalizer-generated entity shares the same Fishy checks), and one line each would be unreadable.
        var nl = Environment.NewLine;
        var gap = nl + nl;
        var groups = findings
            .GroupBy(f => (f.Level, f.Category))
            .OrderByDescending(g => g.Key.Level)
            .ThenByDescending(g => g.Count())
            .Select(g =>
            {
                var header = $"[{g.Key.Level}] {g.Key.Category} -- {g.Count()} Pokémon";
                var detail = "    " + g.First().Message;
                var slots = g.Take(MaxSlotsPerGroup).Select(f => "      " + f.Slot.Identify());
                var more = g.Count() > MaxSlotsPerGroup
                    ? $"      ... and {g.Count() - MaxSlotsPerGroup} more"
                    : null;
                var listed = string.Join(nl, more is null ? slots : slots.Append(more));
                return header + nl + detail + nl + listed;
            });

        var warnings = findings.Count(f => f.Level == HomeTransferPreCheck.Risk.Warning);
        var affected = findings.Select(f => f.Slot.Entity).Distinct().Count();
        var body = string.Join(gap, groups)
                 + gap + "This only covers locally-checkable risks. HOME also validates against its own server "
                 + "records, which PKHeX cannot see, so a clean report here is not a guarantee.";

        using var viewer = new ReportViewer("HOME Transfer Pre-Check",
            $"{findings.Count} finding(s) across {affected} Pokémon ({warnings} warning(s)) that the Legal verdict hides:",
            body);
        viewer.ShowDialog(this);
    }


    /// <summary>Cap on slots listed per finding group before collapsing into a "... and N more" line.</summary>
    private const int MaxSlotsPerGroup = 12;

    private void ShowBusy()
    {
        _cts = new CancellationTokenSource();
        PB_Progress.Visible = true;
        B_Cancel.Visible = true;
        B_Cancel.Enabled = true;
        B_Run.Enabled = false;
        B_CheckClones.Enabled = false;
        B_Close.Enabled = false;
        UseWaitCursor = true;
        Cursor.Current = Cursors.WaitCursor;
    }

    private void HideBusy()
    {
        PB_Progress.Visible = false;
        B_Cancel.Visible = false;
        B_Run.Enabled = true;
        B_CheckClones.Enabled = true;
        B_Close.Enabled = true;
        UseWaitCursor = false;
        _cts?.Dispose();
        _cts = null;
    }

    private void B_Cancel_Click(object sender, EventArgs e)
    {
        _cts?.Cancel();
        B_Cancel.Enabled = false; // one cancel request is enough; avoid re-entrant Cancel() calls
    }

    private static bool HasAnyEditSelected(Plan plan) =>
        plan.Ball || plan.MetLocation || plan.Shiny || plan.MaxIVs || plan.MaxSize || plan.NaturePreset
        || plan.AlignSize || plan.OptimizeIVs || plan.MaxPP || plan.FixMoves || plan.FixTrashMemory || plan.RegenTrackerEC
        || plan.AutoLegalize;

    /// <summary>
    /// Narrows <paramref name="eligible"/> down to only the Pokémon matching every checked filter. Runs off
    /// the UI thread (called from <see cref="RunEdits"/>) since the illegal-only filter needs a full legality
    /// pass per entity, which isn't free across a whole box.
    /// </summary>
    private static List<SlotCache> ApplyFilters(List<SlotCache> eligible, Plan plan)
    {
        if (!plan.FilterIllegalOnly && !plan.FilterShinyOnly && !plan.FilterSpecies && !plan.FilterGiftOrigin
            && !plan.FilterSkipHomeTracked)
            return eligible;

        var result = new List<SlotCache>(eligible.Count);
        foreach (var slot in eligible)
        {
            var pk = slot.Entity;
            if (pk.Species == 0)
                continue;
            if (plan.FilterSpecies && pk.Species != plan.FilterSpeciesValue)
                continue;
            if (plan.FilterShinyOnly && !pk.IsShiny)
                continue;
            if (plan.FilterIllegalOnly && new LegalityAnalysis(pk).Valid)
                continue;
            if (plan.FilterGiftOrigin && !HomeRiskAnalyzer.IsGiftOrigin(pk))
                continue;
            // A Pokémon that already has a HOME Tracker has a matching server-side record. Editing any of
            // HOME's immutable values (PID, EC, IVs, Nature, Ball, Met data, size, Ribbons, OT, TID/SID)
            // invalidates that record and HOME then refuses the upload, while PKHeX still reports it Legal.
            // Most edits below touch at least one of those, so these are excluded by default.
            if (plan.FilterSkipHomeTracked && pk is IHomeTrack { Tracker: not 0 })
                continue;
            result.Add(slot);
        }
        return result;
    }

    /// <summary>
    /// Runs every checked edit over <paramref name="eligible"/>, in a fixed order chosen so each edit sees the
    /// results of the ones before it (moves fixed before auto-legalize gets less to do; auto-legalize runs
    /// last so it isn't immediately undone by an earlier edit). Touches only PKM/SaveFile data -- safe to run
    /// off the UI thread.
    /// </summary>
    private static List<string> RunEdits(List<SlotCache> eligible, SaveFile sav, Plan plan, CancellationToken ct)
    {
        var lines = new List<string>();

        var filtered = ApplyFilters(eligible, plan);
        if (filtered.Count == 0)
        {
            lines.Add("No Pokémon matched the selected filters.");
            return lines;
        }
        eligible = filtered;

        // Cancellation is only checked between whole edit steps below, not mid-step -- each step is a tight
        // loop over `eligible` with no natural interruption point. Anything a completed step already applied
        // and kept legal stays applied; only the steps after the cancellation point are skipped.
        if (Cancelled(ct, lines)) return lines;
        if (plan.Ball)
        {
            var result = BulkQoLEditor.SetBallForAll(eligible.Select(s => s.Entity), plan.BallValue);
            lines.Add(Describe("Ball", result));
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.MetLocation)
        {
            var result = BulkQoLEditor.SetMetLocationForAll(eligible.Select(s => s.Entity), plan.MetLocationValue);
            lines.Add(Describe("Met Location", result));
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.Shiny)
        {
            var result = BulkQoLEditor.SetShinyForAll(eligible.Select(s => s.Entity), plan.ShinyValue, plan.PreferSquare);
            var label = plan.ShinyValue ? (plan.PreferSquare ? "Shiny (prefer Square)" : "Shiny") : "Not Shiny";
            lines.Add(Describe(label, result));
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.MaxIVs)
        {
            var result = BulkQoLEditor.SetMaxIVsForAll(eligible.Select(s => s.Entity));
            lines.Add(Describe("All IVs to 31", result));
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.MaxSize)
        {
            var result = BulkQoLEditor.SetMaxSizeForAll(eligible.Select(s => s.Entity));
            lines.Add($"Max size: {result.Modified} modified, {result.SkippedIllegal} skipped (would be illegal), {result.AlreadyLegal} skipped (pre-Gen8, no size scalar), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.AlignSize)
        {
            var result = BulkQoLEditor.AlignSizeToScaleForAll(eligible.Select(s => s.Entity));
            lines.Add($"Align Height/Weight to Scale: {result.Modified} aligned, {result.SkippedIllegal} skipped (would be illegal), {result.AlreadyLegal} skipped (already aligned, non-Gen9, or HOME-registered), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.NaturePreset)
        {
            var result = BulkQoLEditor.SetNatureEVPresetForAll(eligible.Select(s => s.Entity), plan.NaturePresetValue);
            lines.Add($"Nature+EVs ({plan.NaturePresetValue}): {result.Modified} applied, {result.SkippedIllegal} skipped (would be illegal), {result.AlreadyLegal} skipped (Gen 3/4), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.OptimizeIVs)
        {
            var result = IVOptimizer.OptimizeAll(eligible.Select(s => s.Entity), sav);
            lines.Add($"Optimize IVs: {result.Improved} improved, {result.AlreadyOptimal} already optimal, {result.Failed} no legal spread found, {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.MaxPP)
        {
            var result = BulkQoLEditor.SetMaxPPUpsForAll(eligible.Select(s => s.Entity));
            lines.Add(Describe("PP Ups to max", result));
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.FixMoves)
        {
            // Run before auto-legalize: fixing moves first gives the legalizer less (or nothing) left to do.
            var result = BulkQoLEditor.SetLegalMovesForAll(eligible.Select(s => s.Entity));
            lines.Add($"Auto-fix illegal movesets: {result.Modified} fixed, {result.SkippedIllegal} could not be fixed, {result.AlreadyLegal} already legal, {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.FixTrashMemory)
        {
            // Also run before auto-legalize: cleaner starting data, less for the legalizer to fix.
            var result = BulkQoLEditor.FixTrashAndMemoryForAll(eligible.Select(s => s.Entity), sav);
            lines.Add(Describe("Fix trash bytes / HT memory", result));
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.RegenTrackerEC)
        {
            // Also before auto-legalize: a full regeneration already assigns a fresh PID/EC by construction,
            // so this mainly matters for entities that keep their original (non-regenerated) data.
            var result = BulkQoLEditor.RegeneratePIDTrackerAndECForAll(eligible.Select(s => s.Entity));
            lines.Add($"Regenerate PID/Tracker/EC: {result.Modified} regenerated, {result.AlreadyIllegal} skipped (already illegal beforehand), {result.SkippedIllegal} skipped (change would break legality), {result.AlreadyLegal} skipped (nothing applicable), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.AutoLegalize)
        {
            // Run this last: it should legalize whatever the prior edits left behind, not get immediately
            // undone by them (e.g. a ball/met-location change applied to an entity it just legalized).
            var result = BulkAutoLegalize.LegalizeAll(eligible.Select(s => s.Entity), sav, plan.PreferSquare);
            lines.Add($"Auto-enforce legality: {result.Modified} regenerated, {result.Failed} could not be legalized, {result.AlreadyLegal} already legal, {result.SkippedInvalid} skipped (empty)");
        }

        return lines;
    }

    private static bool Cancelled(CancellationToken ct, List<string> lines)
    {
        if (!ct.IsCancellationRequested)
            return false;
        lines.Add("Cancelled -- remaining edits were skipped. Anything applied above was already kept.");
        return true;
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

    /// <summary>
    /// All box + party slots regardless of the Scope radio selection -- used by whole-save checks
    /// (Check for Clones, gift-origin count) where a clone/duplicate can span boxes and party either way.
    /// </summary>
    private List<SlotCache> GetAllSlots()
    {
        var data = new List<SlotCache>();
        SlotInfoLoader.AddPartyData(SAV, data);
        SlotInfoLoader.AddBoxData(SAV, data);
        return data;
    }

    private bool IsEligible(SlotCache slot) =>
        slot.Source is not SlotInfoBox info || !SAV.GetBoxSlotFlags(info.Box, info.Slot).IsOverwriteProtected();

    private void B_Close_Click(object sender, EventArgs e) => Close();
}
