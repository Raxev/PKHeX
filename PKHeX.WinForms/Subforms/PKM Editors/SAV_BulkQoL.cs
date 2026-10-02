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
    private readonly SlotChangelog? _changelog;
    private CancellationTokenSource? _cts;

    /// <param name="sav">Save file to edit.</param>
    /// <param name="changelog">
    /// Undo history to record this dialog's writes into, so a bulk run can be reversed with Ctrl+Z like any
    /// other slot edit. Optional -- passing null simply means the run is not undoable.
    /// </param>
    public SAV_BulkQoL(SaveFile sav, SlotChangelog? changelog = null)
    {
        InitializeComponent();
        WinFormsUtil.TranslateInterface(this, Main.CurrentLanguage);
        SAV = sav;
        _changelog = changelog;

        RB_Boxes.Checked = true;
        SetupComboBoxes();
        WireSelectAllHeaders();
        RelayoutGroupsVertically();
        FitToScreen();
        EnsureScrollExtent();
    }

    /// <summary>
    /// Re-stacks the category GroupBoxes (and the legal notice below them) using their actual measured
    /// <see cref="Control.Bottom"/>, instead of the fixed Y offsets the Designer computed at generation time.
    /// </summary>
    /// <remarks>
    /// Each GroupBox is <c>AutoSize</c> now, specifically because a fixed row-height constant does not survive
    /// DPI/font scaling -- at a scale other than the one the layout was authored at, a real CheckBox row renders
    /// taller or shorter than assumed, so the box's true height differs from what the Designer's Y math for
    /// every box <i>below</i> it was computed from. Left alone that drift means either an overlap (a later box's
    /// hardcoded top is above where the grown one actually ends) or a gap. Re-deriving each box's position from
    /// the previous one's real Bottom removes the assumption entirely: this holds at any DPI or font scale, not
    /// just the one this was tested at.
    /// </remarks>
    private void RelayoutGroupsVertically()
    {
        const int gap = 12;
        Control[] ordered = [GB_Filters, GB_SetValues, GB_StatsSize, GB_Repairs, GB_Origin, L_LegalNotice];
        var y = ordered[0].Top;
        foreach (var c in ordered)
        {
            c.Top = y;
            y = c.Bottom + gap;
        }
    }

    /// <summary>
    /// Recomputes <see cref="Panel_Scroll"/>'s scrollable range from its children's actual current bounds.
    /// </summary>
    /// <remarks>
    /// AutoScroll alone tracks its extent automatically, but only as of whenever it last observed the child
    /// controls' bounds -- and DPI auto-scaling resizes/repositions every control on the form as a batch
    /// operation sometime around <c>InitializeComponent</c>, which can happen after AutoScroll already cached a
    /// smaller extent from the pre-scaled layout. The visible symptom is a scrollbar that looks like it reaches
    /// the bottom but stops short of the true last controls (here, the last two rows of the Origin &amp; HOME
    /// group were unreachable). Measuring <c>Control.Bottom</c> directly, after layout has settled, sidesteps
    /// the stale cache entirely -- it reads where the controls actually are right now, not where AutoScroll last
    /// thought they were.
    /// </remarks>
    private void EnsureScrollExtent()
    {
        var maxBottom = 0;
        foreach (Control c in Panel_Scroll.Controls)
            maxBottom = Math.Max(maxBottom, c.Bottom);
        Panel_Scroll.AutoScrollMinSize = new System.Drawing.Size(0, maxBottom + 16);
    }

    /// <summary>
    /// Caps the dialog's initial height to the screen's actual working area (the monitor's resolution minus the
    /// taskbar), so the pinned footer -- Run/Close and the utility buttons -- is guaranteed reachable without
    /// the user needing to manually maximize the window first.
    /// </summary>
    /// <remarks>
    /// The Designer's default height (620) was picked for "a typical laptop screen", which is exactly the kind
    /// of guess that breaks on any display shorter than that plus the title bar and taskbar -- the footer was
    /// then genuinely below the visible screen area until the window was maximized. Sizing off the real working
    /// area removes the guess entirely and holds on every monitor, not just the one this was tested on.
    /// </remarks>
    private void FitToScreen()
    {
        var workArea = Screen.FromControl(this).WorkingArea;
        var maxHeight = Math.Max(MinimumSize.Height, workArea.Height - 60); // leave room for the title bar/margins
        if (Height > maxHeight)
            Height = maxHeight;
    }

    /// <summary>
    /// Wires each category's "Select all below" header checkbox to push its own checked state onto every
    /// CheckBox in the same GroupBox (skipping itself).
    /// </summary>
    /// <remarks>
    /// One-directional on purpose: the header sets its children, but unchecking one child afterward does not
    /// un-check the header. A header that tried to reflect "are all children checked" would need to listen to
    /// every child's CheckedChanged too, and the two would fight during the bulk-set itself (each child toggle
    /// re-evaluating the header while the header is still mid-update). "Click to apply this state to everything
    /// below" is a simpler contract and does what the user actually reaches for it to do.
    /// </remarks>
    private void WireSelectAllHeaders()
    {
        foreach (var header in new[] { CHK_SelectAll_SetValues, CHK_SelectAll_StatsSize, CHK_SelectAll_Repairs, CHK_SelectAll_Origin })
        {
            var group = header.Parent;
            header.CheckedChanged += (_, _) =>
            {
                foreach (var c in group!.Controls)
                {
                    if (c is CheckBox chk && chk != header)
                        chk.Checked = header.Checked;
                }
            };
        }
    }

    /// <summary>
    /// The met location to preselect for a given game, for use with the re-origin step.
    /// </summary>
    /// <remarks>
    /// One entry per generation rather than per game, because met location IDs are generation-scoped and do not
    /// carry across: Crown Shrine is Gen8 location 220 and simply does not exist in Gen9, where 220 is past the
    /// end of the table entirely. Picking a location by name from the wrong generation is the easy mistake here,
    /// and it produces an ID that either means something unrelated or nothing at all.
    /// <para/>
    /// Returns 0 for generations with no chosen default, which the caller treats as "leave the list alone".
    /// </remarks>
    private static ushort GetPreferredMetLocation(GameVersion version) => version switch
    {
        GameVersion.SN or GameVersion.MN or GameVersion.US or GameVersion.UM => 188, // Aether Paradise
        GameVersion.SW or GameVersion.SH => 220,  // Crown Shrine (Crown Tundra)
        GameVersion.SL or GameVersion.VL => 124,  // Area Zero (5)
        _ => 0,
    };

    // The IV pass folded into de-cloning runs once per regenerated Pokemon, so the optimizer's on-demand
    // defaults (2000 attempts / 20s) would cost minutes per entity and hours per box. These bound it to
    // something a bulk run can absorb; the standalone "Optimize IVs" step still uses the full budget.
    private const int DeCloneIVAttempts = 150;
    private static readonly TimeSpan DeCloneIVBudget = TimeSpan.FromSeconds(3);

    // "Check for Clones" only ever regenerates the small, explicit list the scan actually flagged -- a handful
    // of entities, not a whole box -- so it can afford the same generous seed-search budget the on-demand
    // per-entity repair menu uses, rather than the cheap bulk default (300 attempts / 1.5s) that exists so the
    // "Regenerate PID/Tracker/EC" checkbox doesn't take minutes across hundreds of entities. Without this, a
    // seed-correlated raid clone (Tera/Mighty/Distribution) with a narrow legal-seed space can exhaust the cheap
    // budget and get reported as unfixable when a longer search would have found a result.
    private const int CloneFixSeedSearchAttempts = 8000;
    private static readonly TimeSpan CloneFixSeedSearchBudget = TimeSpan.FromSeconds(4);

    private void SetupComboBoxes()
    {
        var filtered = GameInfo.FilteredSources;

        CB_Ball.InitializeBinding();
        CB_Ball.DataSource = new BindingSource(filtered.Balls, string.Empty);

        CB_MetLocation.InitializeBinding();
        var metList = GameInfo.GetLocationList(SAV.Version, SAV.Context, egg: false);
        CB_MetLocation.DataSource = new BindingSource(metList, string.Empty);

        // Preselect the values the re-origin step is almost always run with, so the same cleanup on a different
        // save is one checkbox rather than two lookups. Both remain freely overridable.
        CB_Ball.SelectedValue = (int)Ball.Beast;
        var preferred = GetPreferredMetLocation(SAV.Version);
        if (preferred != 0 && metList.Any(z => z.Value == preferred))
            CB_MetLocation.SelectedValue = (int)preferred;

        RB_ShinyOn.Checked = true;

        CB_NaturePreset.DataSource = Enum.GetValues<BulkQoLEditor.NatureEVPreset>();
        CB_NaturePreset.SelectedIndex = 0;

        CB_FilterSpecies.InitializeBinding();
        CB_FilterSpecies.DataSource = new BindingSource(filtered.Species, string.Empty);

        // Every version that can appear as an entity's origin, so a save holding transfers from many games can
        // be narrowed to one of them. Sorted by the enum's own order, which groups games by generation.
        CB_FilterOrigin.InitializeBinding();
        var versions = GameUtil.GameVersions
            .Select(z => new ComboItem(GameInfo.GetVersionName(z), (int)z))
            .ToList();
        CB_FilterOrigin.DataSource = new BindingSource(versions, string.Empty);
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
        bool FilterOrigin, GameVersion FilterOriginValue,
        bool FilterSkipHomeTracked,
        bool Ball, byte BallValue,
        bool MetLocation, ushort MetLocationValue,
        bool TrainerName, string OTName,
        bool Shiny, bool ShinyValue, bool PreferSquare,
        bool MaxIVs,
        bool MaxSize,
        bool AlignSize,
        bool NaturePreset, BulkQoLEditor.NatureEVPreset NaturePresetValue,
        bool OptimizeIVs,
        bool MaxPP,
        bool FixMoves,
        bool FixTrashMemory,
        bool FixOTMemory,
        bool FixFishy,
        bool FixTransferNature,
        bool FixTransferSideFields,
        bool SquareVCShiny,
        bool FixBattleForms,
        bool FixFakeEvent,
        bool RepairMet,
        bool Rehome,
        bool NativeEgg,
        bool SyncDates,
        bool FixTera,
        bool SquareAll,
        bool ClearTracker,
        bool RegenTrackerEC,
        bool AutoLegalize);

    private Plan CapturePlan() => new(
        CHK_FilterIllegalOnly.Checked,
        CHK_FilterShinyOnly.Checked,
        CHK_FilterSpecies.Checked, (ushort)WinFormsUtil.GetIndex(CB_FilterSpecies),
        CHK_FilterGiftOrigin.Checked,
        CHK_FilterOrigin.Checked, (GameVersion)WinFormsUtil.GetIndex(CB_FilterOrigin),
        CHK_SkipHomeTracked.Checked,
        CHK_Ball.Checked, (byte)WinFormsUtil.GetIndex(CB_Ball),
        CHK_MetLocation.Checked, (ushort)WinFormsUtil.GetIndex(CB_MetLocation),
        CHK_TrainerName.Checked, TB_TrainerName.Text,
        CHK_Shiny.Checked, RB_ShinyOn.Checked, CHK_PreferSquare.Checked,
        CHK_MaxIVs.Checked,
        CHK_MaxSize.Checked,
        CHK_AlignSize.Checked,
        CHK_NaturePreset.Checked, (BulkQoLEditor.NatureEVPreset)CB_NaturePreset.SelectedItem!,
        CHK_OptimizeIVs.Checked,
        CHK_MaxPP.Checked,
        CHK_FixMoves.Checked,
        CHK_FixTrashMemory.Checked,
        CHK_FixOTMemory.Checked,
        CHK_FixFishy.Checked,
        CHK_FixTransferNature.Checked,
        CHK_FixTransferSideFields.Checked,
        CHK_SquareVCShiny.Checked,
        CHK_FixBattleForms.Checked,
        CHK_FixFakeEvent.Checked,
        CHK_RepairMet.Checked,
        CHK_Rehome.Checked,
        CHK_NativeEgg.Checked,
        CHK_SyncDates.Checked,
        CHK_FixTera.Checked,
        CHK_SquareAll.Checked,
        CHK_ClearTracker.Checked,
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
        WriteBackUndoable(eligible);

        WinFormsUtil.Alert(lines.ToArray());
    }

    /// <summary>
    /// Writes the edited entities back into the save as a single undoable change.
    /// </summary>
    /// <remarks>
    /// Timing is the whole trick. <see cref="SlotChangelog.Begin(System.Collections.Generic.IEnumerable{ISlotInfo})"/>
    /// snapshots each slot by reading it out of the save, so it must run <b>before</b> the write-back and after
    /// the edits -- which works because the bulk steps mutate detached <c>SlotCache</c> entities and nothing
    /// reaches the save until <c>WriteTo</c>. Beginning it when the dialog opened would snapshot the same state
    /// but hold every clone alive for the whole session for nothing.
    /// <para/>
    /// All slots go into one reversion, so a run is one Ctrl+Z rather than several hundred. Committed even when
    /// a run changed nothing: an undo entry that restores identical bytes is harmless, and "undo my last bulk
    /// run" staying a reliable gesture is worth more than keeping the stack tidy.
    /// </remarks>
    private void WriteBackUndoable(IEnumerable<SlotCache> slots)
    {
        var list = slots as IList<SlotCache> ?? [.. slots];
        if (list.Count == 0)
            return;

        using var change = _changelog?.Begin(list.Select(s => s.Source));
        foreach (var slot in list)
            slot.Source.WriteTo(SAV, slot.Entity, EntityImportSettings.None);
        change?.Commit();
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
        // Drop three categories that regeneration provably cannot touch: Mystery Gift copies (the card pins
        // identity), entities already registered with HOME (a nonzero Tracker is a real server-side record --
        // see BuildUnfixableNote), and anything already illegal independently of the duplicate match (the
        // problem was never about the PID). Including any of these only inflated the "N duplicates" count with
        // entities a fix run was never going to reach, and for the latter two specifically, re-ran the clone
        // scan and reported the same stuck result forever with no indication why.
        static bool StartsLegal(PKM pk) { try { return new LegalityAnalysis(pk).Valid; } catch { return false; } }
        static bool IsHomeTracked(PKM pk) => pk is IHomeTrack { Tracker: not 0 };
        var distinctFixable = fixable.DistinctBy(s => s.Entity)
            .Where(s => !HomeRiskAnalyzer.IsGiftOrigin(s.Entity) && !IsHomeTracked(s.Entity) && StartsLegal(s.Entity))
            .ToList();
        var allDistinct = fixable.DistinctBy(s => s.Entity).ToList();
        var giftClones = allDistinct.Count(s => HomeRiskAnalyzer.IsGiftOrigin(s.Entity));
        var homeTrackedClones = allDistinct.Count(s => !HomeRiskAnalyzer.IsGiftOrigin(s.Entity) && IsHomeTracked(s.Entity));
        var alreadyIllegalClones = allDistinct.Count - giftClones - homeTrackedClones - distinctFixable.Count;

        var gap = Environment.NewLine + Environment.NewLine;
        var body = string.Join(gap, lines);
        if (giftNotice is not null)
            body += gap + giftNotice;
        if (distinctFixable.Count != 0 || giftClones != 0 || homeTrackedClones != 0 || alreadyIllegalClones != 0)
        {
            if (giftClones > 0)
            {
                body += gap + $"{giftClones} duplicate(s) are Mystery Gift copies and are excluded from the fix "
                     + "entirely -- the card pins their identity, so no reroll or regeneration can separate them. "
                     + "Delete the extras manually instead.";
            }
            if (homeTrackedClones > 0)
            {
                body += gap + $"{homeTrackedClones} duplicate(s) already carry a real HOME Tracker and are "
                     + "excluded from the fix entirely -- that Tracker is a server-side record HOME issued for "
                     + "this exact PID, so at most one copy in each group is the entity it actually describes. "
                     + "Keep the one you uploaded and delete the rest manually.";
            }
            if (alreadyIllegalClones > 0)
            {
                body += gap + $"{alreadyIllegalClones} duplicate(s) are already illegal for a reason unrelated to "
                     + "being a clone and are excluded from the fix entirely -- see the specific finding listed "
                     + "next to each affected group above. Re-home the entity to a different valid encounter "
                     + "first; the duplicate will remain reported until then regardless of how many times this "
                     + "fix is run.";
            }
        }
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
            var one = BulkQoLEditor.RegeneratePIDTrackerAndECForAll([slot.Entity], SAV, p => IVOptimizer.TryOptimize(p, SAV, DeCloneIVAttempts, DeCloneIVBudget),
                CloneFixSeedSearchAttempts, CloneFixSeedSearchBudget);
            if (one.Modified == 1) fixedCount++;
            else if (one.AlreadyIllegal == 1) wasIllegal++;
            else if (one.AlreadyLegal == 1) notApplicable++;
            else stubborn.Add(slot);
        }
        WriteBackUndoable(distinctFixable);

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
                WriteBackUndoable(stubborn);
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

    /// <summary>
    /// Builds the "NOT FIXABLE BY REGENERATION" annotation for one duplicate group's report line, or an empty
    /// string when the group is expected to resolve normally.
    /// </summary>
    /// <remarks>
    /// Two genuinely distinct reasons a group can resist every fix attempt, checked in order:
    /// <list type="number">
    /// <item>Mystery Gift-origin. The card pins PID (or a shiny type demanding an exact ShinyXor), so no reroll
    /// or seed search can separate copies of one redemption. This was already surfaced before this method
    /// existed.</item>
    /// <item><b>Already illegal independently of being a duplicate.</b> This is the one that was missing, and it
    /// is the more common cause in practice: <see cref="BulkQoLEditor.RegeneratePIDTrackerAndECForAll"/> only
    /// ever touches an entity that starts out <see cref="LegalityAnalysis.Valid"/> -- "distinguish our edit
    /// broke it from it was already broken" is the method's own stated design. An entity that is illegal for an
    /// unrelated reason (most commonly here: it is shiny but its matched encounter is <c>Shiny.Never</c>) is
    /// therefore skipped before any reroll is even attempted, and will report as stuck on every single run
    /// regardless of how good the regeneration logic is -- regenerating a PID cannot fix a problem that was
    /// never about the PID. Surfacing the actual invalid finding codes here, rather than a generic "could not
    /// be auto-fixed", is what lets the user act on it directly: re-home the entity to a different encounter
    /// that actually permits the trait in question (an Alpha encounter for a shiny PLA-origin entity, for
    /// example), rather than re-running a fix that can never have touched the real problem.
    /// </list>
    /// Deliberately does <b>not</b> flag a group just because its matched encounter lacks
    /// <see cref="IGenerateSeed32"/> (Mass Outbreaks, most Fixed encounters): the naive direct-field reroll in
    /// <c>RegenerateIdentity</c> works fine for those as long as the entity starts legal, since their PID is not
    /// seed-correlated in the first place -- only seed-correlated raids need the seed search at all. Labeling
    /// those "unfixable" would be wrong, not just unhelpful.
    /// </remarks>
    private static string BuildUnfixableNote(PKM sample)
    {
        if (HomeRiskAnalyzer.IsGiftOrigin(sample))
        {
            return Environment.NewLine + "  NOT FIXABLE BY REGENERATION: copies of a single Mystery Gift redemption. "
                 + "The card pins the identity, so they cannot be given distinct PIDs and stay legal. "
                 + "Keep one and delete the rest.";
        }

        // A nonzero HOME Tracker means this exact PID has a real server-side record right now -- at most one
        // copy in the group can be the entity that record actually describes. Regenerating the rest would not
        // recover anything; it would just make them stop matching a real record instead of an invented one.
        if (sample is IHomeTrack { Tracker: not 0 })
        {
            return Environment.NewLine + "  NOT FIXABLE BY REGENERATION: already registered with Pokémon HOME. "
                 + "The Tracker is a GUID HOME itself issued for this exact PID -- at most one copy here is the "
                 + "entity that record describes, and regenerating a PID for a Tracker HOME never issued makes a "
                 + "local mismatch, not a fix. Keep one (the one you actually uploaded) and delete the rest.";
        }

        LegalityAnalysis la;
        try { la = new LegalityAnalysis(sample); }
        catch (Exception)
        {
            return Environment.NewLine + "  NOT FIXABLE BY REGENERATION: legality could not be determined for this entity "
                 + "(corrupted or out-of-range data). Inspect it directly in the editor.";
        }
        if (la.Valid)
            return string.Empty; // Starts legal -- the fix should be able to reach it.

        var codes = la.Results.Where(r => r.Judgement == Severity.Invalid)
                               .Select(r => r.Result.ToString()).Distinct().Take(4).ToList();
        var why = codes.Count == 0 ? "(no specific finding -- check the full legality report)" : string.Join(", ", codes);
        return Environment.NewLine + "  NOT FIXABLE BY REGENERATION: this entity is already illegal for a reason "
             + $"unrelated to the duplicate match -- {why}. Regenerating the PID cannot fix a problem that was "
             + "never about the PID; the group will report as stuck on every run until the underlying issue is "
             + "resolved, typically by re-homing this entity to a different encounter that actually permits "
             + "whatever trait is in conflict (e.g. an Alpha encounter for a shiny PLA-origin Pokemon).";
    }

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
                var note = BuildUnfixableNote(sample);
                return $"{shiny}{name} (Form {sample.Form}): {group.Count} copies share an identical PID/IVs/form --{note}"
                     + Environment.NewLine + string.Join(Environment.NewLine, slots);
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
        plan.Ball || plan.MetLocation || plan.TrainerName || plan.Shiny || plan.MaxIVs || plan.MaxSize || plan.NaturePreset
        || plan.AlignSize || plan.OptimizeIVs || plan.MaxPP || plan.FixMoves || plan.FixTrashMemory || plan.FixOTMemory || plan.FixFishy || plan.FixTransferNature || plan.FixTransferSideFields || plan.SquareVCShiny || plan.FixBattleForms || plan.FixFakeEvent || plan.RepairMet || plan.Rehome || plan.FixTera || plan.NativeEgg || plan.SyncDates || plan.SquareAll || plan.ClearTracker || plan.RegenTrackerEC
        || plan.AutoLegalize;

    /// <summary>
    /// Narrows <paramref name="eligible"/> down to only the Pokémon matching every checked filter. Runs off
    /// the UI thread (called from <see cref="RunEdits"/>) since the illegal-only filter needs a full legality
    /// pass per entity, which isn't free across a whole box.
    /// </summary>
    private static List<SlotCache> ApplyFilters(List<SlotCache> eligible, Plan plan)
    {
        if (!plan.FilterIllegalOnly && !plan.FilterShinyOnly && !plan.FilterSpecies && !plan.FilterGiftOrigin
            && !plan.FilterSkipHomeTracked && !plan.FilterOrigin)
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
            // Origin game as the entity itself records it, not the encounter's -- that is what the user picks
            // from and what the PKM editor displays, so filtering on anything else would surprise them.
            if (plan.FilterOrigin && pk.Version != plan.FilterOriginValue)
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

        // Memory fields are NOT on HOME's immutable list, so fixing them cannot invalidate a HOME Tracker.
        // That makes the OT-memory fix the one edit here that is safe to apply to HOME-registered entities,
        // and most entities missing an OT memory are exactly the transferred ones the filter excludes -- so it
        // gets its own scope with that particular filter turned off.
        var memoryScope = plan.FilterSkipHomeTracked
            ? ApplyFilters(eligible, plan with { FilterSkipHomeTracked = false })
            : null;

        // Re-origin exists specifically to remove a Pokemon's dependence on a HOME tracker, so "skip
        // HOME-registered" excludes every entity it is meant to act on and silently reduces the step to a no-op.
        // The two options are contradictory rather than complementary, so this step ignores that one filter --
        // the alternative is a run that reports 0 converted with no indication of why.
        var reoriginScope = plan.FilterSkipHomeTracked
            ? ApplyFilters(eligible, plan with { FilterSkipHomeTracked = false })
            : null;

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
            lines.Add($"Ball: {result.Changed} set, {result.ChangedWhileIllegal} set on already-illegal Pokemon (no new problems introduced), {result.NotLegalForEncounter} not legal for that encounter, {result.AlreadySet} already that ball, {result.Unsupported} skipped (Gen1/2 have no ball), {result.Empty} skipped (empty), {result.ProtectedEvent} protected (Cherish Ball event - never touched)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.MetLocation)
        {
            var result = BulkQoLEditor.SetMetLocationForAll(eligible.Select(s => s.Entity), plan.MetLocationValue);
            lines.Add($"Met Location: {result.Modified} set, {result.SkippedIllegal} would become illegal, {result.AlreadyLegal} protected (Cherish Ball event - never touched), {result.SkippedInvalid} skipped (empty)");
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
        if (plan.FixBattleForms)
        {
            // Runs before everything else: a battle-only form makes the entity Invalid, and an already-illegal
            // entity fails every legality-guarded edit that follows, including the clone de-duplicator.
            var result = BulkQoLEditor.FixBattleOnlyFormsForAll(eligible.Select(s => s.Entity));
            lines.Add($"Revert battle-only forms: {result.Modified} reverted, {result.SkippedIllegal} could not be reverted, {result.AlreadyLegal} skipped (not a battle-only form), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.FixFakeEvent)
        {
            // Before the move fix: clearing the Fateful flag changes which encounter is matched, and the
            // matched encounter is what decides which moves are legal.
            var result = BulkQoLEditor.FixFakeEventDataForAll(eligible.Select(s => s.Entity));
            lines.Add($"Fix fake event data: {result.Modified} cleaned up, {result.SkippedIllegal} could not be cleared, {result.AlreadyLegal} skipped (no Fateful/ribbon problem), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.RepairMet)
        {
            // Deliberately ahead of re-home: both target EncInvalid, but this one only touches met level (then
            // met location) and commits only on full legality, so it repairs the near-misses without spending
            // the ball and moveset that re-home would. Whatever it cannot fix falls through to re-home.
            var result = BulkQoLEditor.RepairMetDataForAll(eligible.Select(s => s.Entity));
            lines.Add($"Repair met level/location: {result.Modified} repaired, {result.SkippedIllegal} no matching encounter found, {result.AlreadyLegal} skipped (encounter already matches or egg), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.Rehome)
        {
            // After the Fateful/ribbon cleanup and before the move fix: this assigns the encounter that every
            // later step judges the entity against.
            var result = BulkQoLEditor.RehomeToOrdinaryEncounterForAll(eligible.Select(s => s.Entity));
            lines.Add($"Re-home to real encounter: {result.Modified} re-homed, {result.SkippedIllegal} no ordinary encounter found, {result.AlreadyLegal} skipped (encounter already matches), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.TrainerName)
        {
            var result = BulkQoLEditor.SetOriginalTrainerNameForAll(eligible.Select(s => s.Entity), plan.OTName);
            lines.Add($"Set OT name: {result.Changed} renamed, {result.ChangedWhileIllegal} renamed (already illegal), {result.NotLegalForEncounter} refused (encounter pins the OT), {result.AlreadySet} already named, {result.TooLong} name too long for format, {result.ProtectedEvent} protected (Cherish Ball event - never touched), {result.Empty} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.NativeEgg)
        {
            // Before the Tera/move repairs: this reassigns the encounter, and everything downstream is judged
            // against whatever encounter is matched at the time it runs.
            var scope = reoriginScope ?? eligible;
            var result = BulkQoLEditor.ConvertToNativeEggForAll(
                scope.Select(s => s.Entity), sav, plan.BallValue, plan.MetLocationValue);
            lines.Add($"Re-origin as native egg: {result.Converted} converted, {result.NotConvertible} could not be made legal as an egg, {result.NotEggCapable} refused (legendary/mythical or cannot hatch - never touched), {result.NotApplicable} skipped (already native), {result.ProtectedEvent} protected (event - never touched), {result.Empty} skipped (empty)"
);
            if (result.FirstFailure is not null)
                lines.Add($"   (skipped example: {result.FirstFailure})");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.SyncDates)
        {
            var result = BulkQoLEditor.SyncEncounterDatesForAll(eligible.Select(s => s.Entity));
            lines.Add($"Match Met/Egg dates: {result.Synced} synced, {result.AlreadyConsistent} already consistent, {result.Reverted} reverted (would have broken legality), {result.Empty} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.FixTera)
        {
            // After the met/encounter repairs: which Tera Types are legal is decided by the matched encounter's
            // evolution chain, so running this first would score every candidate against the wrong template.
            var result = BulkQoLEditor.FixTeraTypeForAll(eligible.Select(s => s.Entity));
            lines.Add($"Fix Tera Type: {result.Modified} fixed, {result.SkippedIllegal} no legal type found, {result.AlreadyLegal} skipped (not Gen9 or already correct), {result.SkippedInvalid} skipped (empty)");
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
        if (plan.FixOTMemory)
        {
            var scope = memoryScope ?? eligible;
            var result = BulkQoLEditor.FixOriginalTrainerMemoryForAll(scope.Select(s => s.Entity));
            lines.Add($"Fix missing OT memory: {result.Modified} filled in, {result.SkippedIllegal} no valid memory found, {result.AlreadyLegal} skipped (not missing one), {result.SkippedInvalid} skipped (empty)");
            var friendship = BulkQoLEditor.FixOriginalTrainerFriendshipForAll(scope.Select(s => s.Entity));
            lines.Add($"Fix event OT friendship: {friendship.Modified} restored to base friendship, {friendship.SkippedIllegal} could not be fixed, {friendship.AlreadyLegal} skipped (no such warning), {friendship.SkippedInvalid} skipped (empty)");
            foreach (var slot in scope)
                slot.Source.WriteTo(sav, slot.Entity, EntityImportSettings.None);
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.FixFishy)
        {
            // Same HOME exemption as the OT-memory fix: EVs, EXP and nickname are not immutable to HOME.
            var scope = memoryScope ?? eligible;
            var result = BulkQoLEditor.FixFishyWarningsForAll(scope.Select(s => s.Entity));
            lines.Add($"Fix Fishy warnings: {result.Modified} cleaned up, {result.SkippedIllegal} could not be cleared, {result.AlreadyLegal} skipped (no such warning), {result.SkippedInvalid} skipped (empty)");
            foreach (var slot in scope)
                slot.Source.WriteTo(sav, slot.Entity, EntityImportSettings.None);
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.FixTransferNature)
        {
            // Nature IS on HOME's immutable list, so this uses the ordinary scope and honours "Skip
            // HOME-registered" -- unlike the OT-memory and Fishy repairs above.
            var result = BulkQoLEditor.FixTransferNatureForAll(eligible.Select(s => s.Entity));
            lines.Add($"Fix VC transfer Nature: {result.Modified} resynced to Experience, {result.SkippedIllegal} could not be fixed, {result.AlreadyLegal} skipped (no such warning), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.FixTransferSideFields)
        {
            // EC and Nature are HOME-immutable, so this honours "Skip HOME-registered" like the VC nature fix.
            var result = BulkQoLEditor.FixTransferSideFieldsForAll(eligible.Select(s => s.Entity));
            lines.Add($"Fix legacy transfer side fields: {result.Modified} improved, {result.SkippedIllegal} left alone (PID itself is wrong for the encounter -- needs Auto-enforce legality), {result.AlreadyLegal} skipped (no such warning), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.SquareVCShiny)
        {
            // Rewrites the PID, which is HOME-immutable, so this honours "Skip HOME-registered".
            var result = BulkQoLEditor.MakeVirtualConsoleShinySquareForAll(eligible.Select(s => s.Entity));
            lines.Add($"Square VC transfer shinies: {result.Modified} converted from Star to Square, {result.SkippedIllegal} could not be converted, {result.AlreadyLegal} skipped (not a Star VC transfer), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.SquareAll)
        {
            // Passes the save so the save-wide set of in-use PIDs is known: Square PIDs are a tiny subspace,
            // so converting in bulk can otherwise manufacture fresh PID collisions.
            var result = BulkQoLEditor.MakeShiniesSquareForAll(eligible.Select(s => s.Entity), sav);
            lines.Add($"Convert Star shinies to Square: {result.Modified} converted, {result.SkippedIllegal} skipped (PID is pinned by the encounter), {result.AlreadyIllegal} skipped (already illegal beforehand), {result.AlreadyLegal} skipped (not shiny, or already Square), {result.SkippedInvalid} skipped (empty)");
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.ClearTracker)
        {
            // Targets HOME-registered entities by definition, so it must see them: use the scope that has the
            // "Skip HOME-registered" filter disabled, or it would have nothing to act on.
            var scope = memoryScope ?? eligible;
            var result = BulkQoLEditor.ClearHomeTrackerForAll(scope.Select(s => s.Entity));
            lines.Add($"Clear HOME Tracker: {result.Modified} cleared, {result.SkippedIllegal} skipped (a Tracker is required for that encounter), {result.AlreadyLegal} skipped (no Tracker set), {result.SkippedInvalid} skipped (empty)");
            foreach (var slot in scope)
                slot.Source.WriteTo(sav, slot.Entity, EntityImportSettings.None);
        }
        if (Cancelled(ct, lines)) return lines;
        if (plan.RegenTrackerEC)
        {
            // Also before auto-legalize: a full regeneration already assigns a fresh PID/EC by construction,
            // so this mainly matters for entities that keep their original (non-regenerated) data.
            var result = BulkQoLEditor.RegeneratePIDTrackerAndECForAll(eligible.Select(s => s.Entity), sav, p => IVOptimizer.TryOptimize(p, sav, DeCloneIVAttempts, DeCloneIVBudget));
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
        // "Both" means the whole save, not just boxes + party. AddFromSaveFile also pulls the misc slots
        // (Surprise Trade, Daycare, Fused), which the HOME pre-check already scans -- without this, findings
        // could be reported in slots no bulk edit was able to reach, and re-running never cleared them.
        if (RB_Both.Checked)
        {
            SlotInfoLoader.AddFromSaveFile(SAV, data);
            return data;
        }
        if (RB_Party.Checked)
            SlotInfoLoader.AddPartyData(SAV, data);
        if (RB_Boxes.Checked)
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

    /// <summary>
    /// Reorders every box in the save into National Dex order.
    /// </summary>
    /// <remarks>
    /// <see cref="PKM.Species"/> is already the National Dex ID in PKHeX regardless of the entity's format
    /// (the format-specific internal indices are converted on read), so the default
    /// <see cref="EntitySorting.OrderBySpecies"/> comparer is National Dex order and needs no custom sorter.
    /// <para/>
    /// This is a slot reordering, not an edit: nothing about any entity's data changes, so the guarded
    /// legality pattern the rest of this dialog uses does not apply. <see cref="SaveFile.SortBoxes"/> skips
    /// overwrite-protected slots (locked / battle-box) and repoints slot pointers on its own.
    /// <para/>
    /// PKHeX already exposes this via the box right-click menu (Sort -> SortSpecies, holding Shift to apply to
    /// all boxes rather than the current one); this button is the discoverable "all boxes" entry point.
    /// </remarks>
    private void B_SortBoxes_Click(object sender, EventArgs e)
    {
        var moved = SAV.SortBoxes();
        WinFormsUtil.Alert(moved == 0
            ? "No slots were repositioned -- the boxes are already in National Dex order (or every slot is overwrite-protected)."
            : $"Sorted all boxes by National Dex #: {moved} Pokemon repositioned.");
    }

    private void B_Close_Click(object sender, EventArgs e) => Close();
}
