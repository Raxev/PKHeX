using System;
using System.Collections.Generic;

namespace PKHeX.Core;

/// <summary>
/// Surfaces the things that make a Pokémon risky to upload to Pokémon HOME even though
/// <see cref="LegalityAnalysis"/> reports it as Legal. PKHeX validates <i>cartridge</i> legality ("could the
/// game have produced this?"); HOME additionally validates against its own server-side records, which PKHeX
/// has no access to and deliberately does not model.
/// </summary>
/// <remarks>
/// Two distinct blind spots are covered:
/// <list type="number">
/// <item><b>Severity.Fishy findings.</b> <see cref="CheckResult.Valid"/> is
/// <c>Judgement != Severity.Invalid</c>, so a Fishy check still reports as Legal. The headline verdict can
/// therefore be green while the report body contains real warnings.</item>
/// <item><b>HOME-record desync.</b> An entity that already carries a HOME Tracker has a matching record on
/// HOME's servers. Editing any of HOME's documented immutable values (PID, Encryption Constant, IVs, Nature,
/// Ball, Met data, Relearn Moves, Original Tera Type, Height/Weight/Scale, Ribbons, OT, TID/SID) invalidates
/// that Tracker, and HOME then refuses the upload -- while PKHeX still says Legal, because nothing about the
/// entity is illegal on the cartridge.</item>
/// </list>
/// Read-only: this reports, it never edits.
/// </remarks>
public static class HomeTransferPreCheck
{
    /// <summary>How serious a <see cref="Finding"/> is.</summary>
    public enum Risk
    {
        /// <summary>Informational; no action needed unless HOME actually rejects the entity.</summary>
        Note,
        /// <summary>Worth reviewing; a plausible contributor to a HOME rejection.</summary>
        Warning,
    }

    /// <param name="Slot">Where the entity lives.</param>
    /// <param name="Level">How serious the finding is.</param>
    /// <param name="Category">Short grouping key. A whole save can produce the same finding hundreds of times
    /// (every legalizer-generated entity shares the same Fishy checks), so callers group by this rather than
    /// printing one line per entity per check.</param>
    /// <param name="Message">Human-readable explanation, shared by every entity in the category.</param>
    public readonly record struct Finding(SlotCache Slot, Risk Level, string Category, string Message);

    /// <summary>
    /// Scans every entity in <paramref name="sav"/> and reports HOME-transfer risks that
    /// <see cref="LegalityAnalysis.Valid"/> does not surface.
    /// </summary>
    public static IReadOnlyList<Finding> Scan(SaveFile sav)
    {
        var slots = new List<SlotCache>();
        SlotInfoLoader.AddFromSaveFile(sav, slots);

        var findings = new List<Finding>();
        foreach (var slot in slots)
        {
            var pk = slot.Entity;
            if (pk.Species == 0 || !pk.ChecksumValid)
                continue;
            Inspect(slot, pk, findings);
        }
        return findings;
    }

    private static void Inspect(SlotCache slot, PKM pk, List<Finding> findings)
    {
        AddTrackerRisk(slot, pk, findings);
        AddFishyFindings(slot, pk, findings);
        if (pk is PK9 pk9)
            AddGen9Risks(slot, pk9, findings);
    }

    private static void AddTrackerRisk(SlotCache slot, PKM pk, List<Finding> findings)
    {
        if (pk is not IHomeTrack { Tracker: not 0 } home)
            return;

        findings.Add(new Finding(slot, Risk.Warning, "HOME-registered (Tracker set)",
            $"Already registered with HOME (Tracker {home.Tracker:X16}). HOME holds a record of this exact " +
            "Pokémon, so editing any immutable value (PID, EC, IVs, Nature, Ball, Met data, Relearn Moves, " +
            "Original Tera Type, Height/Weight/Scale, Ribbons, OT, TID/SID) invalidates the Tracker and HOME " +
            "will refuse it -- while PKHeX keeps reporting it as Legal. Avoid bulk-editing this one, or clear " +
            "its Tracker so HOME issues a fresh record on the next upload."));
    }

    /// <summary>
    /// Collects checks that were flagged <see cref="Severity.Fishy"/>. These do not make
    /// <see cref="LegalityAnalysis.Valid"/> false, so they are invisible if you only read the headline verdict.
    /// </summary>
    private static void AddFishyFindings(SlotCache slot, PKM pk, List<Finding> findings)
    {
        LegalityAnalysis la;
        try
        {
            la = new LegalityAnalysis(pk);
        }
        catch (Exception)
        {
            findings.Add(new Finding(slot, Risk.Warning, "Analysis error", "Legality analysis threw an exception -- the data is likely corrupted."));
            return;
        }

        // Only worth reporting on entities that otherwise look clean; an already-Invalid entity has a normal
        // legality report to read instead, and would just add noise here.
        if (!la.Valid)
        {
            findings.Add(new Finding(slot, Risk.Note, "Already Illegal", "Reported Illegal by PKHeX -- fix ordinary legality first; see its Legality report."));
            return;
        }

        foreach (var chk in la.Results)
        {
            if (chk.Judgement != Severity.Fishy)
                continue;
            findings.Add(new Finding(slot, Risk.Warning, $"Fishy: {chk.Result}",
                "Reports as Legal, but this check was flagged Fishy. Fishy findings never turn the verdict " +
                "red, so they are invisible if you only read the Legal/Illegal result."));
        }
    }

    /// <summary>
    /// Bulk edits skip HOME-registered entities by design (editing an immutable value invalidates their HOME
    /// record), so a finding on one of those is reported but NOT actionable by the bulk tools. Saying so in the
    /// category itself answers "why didn't my fix apply to these?" without the reader having to cross-reference
    /// two separate groups.
    /// </summary>
    private static string Scope(PKM pk) =>
        pk is IHomeTrack { Tracker: not 0 } ? " [HOME-registered, bulk edits skip these]" : " [fixable]";

    private static void AddGen9Risks(SlotCache slot, PK9 pk, List<Finding> findings)
    {
        // HOME's own PK9 importer sets Obedience_Level from MetLevel. PKHeX only enforces an exact match for
        // untraded entities; for traded ones it accepts the whole MetLevel..CurrentLevel band, so a value no
        // real game would have written still passes.
        if (!pk.IsUntraded && pk.ObedienceLevel != pk.MetLevel)
        {
            findings.Add(new Finding(slot, Risk.Note, "Obedience Level != Met Level",
                $"Obedience Level ({pk.ObedienceLevel}) differs from Met Level ({pk.MetLevel}). PKHeX allows any " +
                "value in the Met..Current band for a traded Pokémon, but the game writes the level at the time " +
                "of trade, and HOME's own importer derives it from Met Level."));
        }

        // PKHeX range-checks TeraTypeOriginal but never TeraTypeOverride for ordinary species
        // (MiscVerifierPK9.VerifyTeraType only special-cases eggs, Terapagos and Ogerpon).
        var over = (byte)pk.TeraTypeOverride;
        if (!TeraTypeUtil.IsValid(over))
        {
            findings.Add(new Finding(slot, Risk.Warning, "Tera Type Override out of range",
                $"Tera Type Override holds an out-of-range value ({over}). PKHeX does not range-check this field " +
                "for ordinary species, but HOME stores it verbatim."));
        }

        // MiscScaleVerifier only requires HeightScalar == Scale once the entity is HOME-tracked
        // (IsHeightScaleMatchRequired => pk is IHomeTrack { HasTracker: true }). Until then a mismatch is
        // completely unflagged -- but HOME's own importer copies Scale over HeightScalar, so after a round trip
        // the entity comes back with the rule active. Verified: adding a Tracker to a mismatched entity
        // immediately turns it Invalid with StatIncorrectScaleValue.
        if (pk.HeightScalar != pk.Scale && pk is IHomeTrack { Tracker: not 0 })
        {
            // This combination should already be an outright legality error, since the match requirement is
            // active whenever a Tracker exists. Seeing it alongside a Legal verdict means something upstream
            // disagrees, and is worth surfacing loudly rather than filing under the ordinary mismatch group.
            findings.Add(new Finding(slot, Risk.Warning, "HeightScalar != Scale WHILE HOME-registered",
                $"HeightScalar ({pk.HeightScalar}) does not match Scale ({pk.Scale}) on a Pokémon that already " +
                "has a HOME Tracker. PKHeX enforces that match once a Tracker exists, so this should be showing " +
                "as an outright legality error. Bulk edits deliberately skip HOME-registered Pokémon, so this " +
                "one needs manual attention."));
        }
        else if (pk.HeightScalar != pk.Scale)
        {
            findings.Add(new Finding(slot, Risk.Warning, "HeightScalar != Scale" + Scope(pk),
                $"HeightScalar ({pk.HeightScalar}) does not match Scale ({pk.Scale}). Harmless right now, but " +
                "PKHeX enforces a match once the Pokémon is HOME-tracked, so it becomes an outright legality " +
                "error after a HOME round trip."));
        }

        // Gen9 grants the Jumbo/Mini mark automatically at capture from Scale, and RibbonVerifierMark9 only
        // checks the mark-without-scale direction -- so scale-without-mark passes despite being unreachable
        // in-game. (BulkQoLEditor.SetMaxSizeForAll now sets the mark; this catches entities edited before that.)
        if (pk is IRibbonSetMark9 marks)
        {
            if (pk.Scale == byte.MaxValue && !marks.RibbonMarkJumbo)
                findings.Add(new Finding(slot, Risk.Warning, "Max Scale without Jumbo Mark" + Scope(pk), "Scale is maxed (255) but the Jumbo Mark is absent -- the game always awards it at that size."));
            else if (pk.Scale == byte.MinValue && !marks.RibbonMarkMini)
                findings.Add(new Finding(slot, Risk.Warning, "Min Scale without Mini Mark" + Scope(pk), "Scale is minimum (0) but the Mini Mark is absent -- the game always awards it at that size."));
        }
    }
}
