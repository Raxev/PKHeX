using System;
using System.Collections.Generic;

namespace PKHeX.Core.AutoMod;

/// <summary>
/// Applies AutoLegalityMod's legalization engine across many <see cref="PKM"/> at once, for the "auto-enforce
/// legality" bulk edit. Already-legal entities are left untouched; illegal entities are regenerated from a
/// <see cref="RegenTemplate"/> built off their current data and only replaced if the regenerated result is
/// itself confirmed legal (and converts cleanly back to the entity's original format).
/// </summary>
public static class BulkAutoLegalize
{
    /// <param name="Modified">Entities that were illegal and were successfully replaced with a legal equivalent.</param>
    /// <param name="Failed">Entities that were illegal and could not be legalized (or the result didn't convert back to the original format).</param>
    /// <param name="AlreadyLegal">Entities that were already legal and were left untouched.</param>
    /// <param name="SkippedInvalid">Entities that were skipped entirely (empty slot).</param>
    public readonly record struct Result(int Modified, int Failed, int AlreadyLegal, int SkippedInvalid)
    {
        public int Total => Modified + Failed + AlreadyLegal + SkippedInvalid;
    }

    /// <param name="preferSquare">
    /// When the entity being regenerated is shiny, ask the legalizer for a Square shiny
    /// (<see cref="PKM.ShinyXor"/> == 0) rather than whatever type it currently has. Only meaningful for
    /// Format 8+, where the two are visually differentiated. Falls back to the entity's original shiny type if
    /// no Square-capable encounter can be found, so this can never turn a legalizable entity into a failure.
    /// </param>
    public static Result LegalizeAll(IEnumerable<PKM> mons, SaveFile sav, bool preferSquare = true)
    {
        int modified = 0, failed = 0, alreadyLegal = 0, invalid = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0 || pk.Species > sav.MaxSpeciesID)
            {
                invalid++;
                continue;
            }

            bool alreadyValid;
            try
            {
                alreadyValid = new LegalityAnalysis(pk).Valid;
            }
            catch (Exception)
            {
                alreadyValid = false; // corrupted data; fall through and let TryLegalize's own guard handle it
            }
            if (alreadyValid)
            {
                alreadyLegal++;
                continue;
            }

            // Ask for Square first, then retry with the entity's own shiny type. Two attempts rather than one so
            // an encounter that can only produce a Star shiny (or a fixed-PID gift) still gets legalized instead
            // of being counted as a failure just because the Square preference couldn't be honoured.
            var wantSquare = preferSquare && pk.Format >= 8 && pk.IsShiny && pk.ShinyXor != 0;
            if ((wantSquare && TryLegalize(pk, sav, Shiny.AlwaysSquare)) || TryLegalize(pk, sav, null))
            {
                // The vendored legalizer only honours AlwaysSquare on some encounter paths -- its Gen9 raid
                // seed search does (APILegality.cs:1202), but the ordinary wild path ignores it and reproduces
                // whatever type the source had. Make the preference stick with a guarded post-pass. Reverts
                // cleanly for seed-correlated encounters where rewriting the PID would break the match, leaving
                // the legalized Star result intact rather than failing the whole entity.
                if (wantSquare && pk is { IsShiny: true, ShinyXor: not 0 })
                    BulkQoLEditor.TryApplyGuarded(pk, static p => p.SetShiny(Shiny.AlwaysSquare));
                modified++;
            }
            else
            {
                failed++;
            }
        }
        return new Result(modified, failed, alreadyLegal, invalid);
    }

    /// <param name="Regenerated">Entities given a genuinely new identity (PID changed) while staying legal.</param>
    /// <param name="Failed">Entities the legalizer could not rebuild, or rebuilt without changing identity.</param>
    public readonly record struct IdentityResult(int Regenerated, int Failed);

    /// <summary>
    /// Gives entities a fresh identity by re-running the legalizer, for cases where directly rerolling the PID
    /// cannot work.
    /// </summary>
    /// <remarks>
    /// Gen9 raid encounters (<c>EncounterTera9</c>/<c>Dist9</c>/<c>Might9</c>) derive PID, Encryption Constant
    /// and IVs from a single seed, and <c>Encounter9RNG</c> re-derives and compares them exactly. Mutating the
    /// PID in place therefore always breaks the seed correlation, which is why
    /// <see cref="BulkQoLEditor.RegeneratePIDTrackerAndECForAll"/> reverts on those entities. The only way to
    /// change identity legally is to find a <i>different valid seed</i>, which is what the legalizer's search
    /// does.
    /// <para/>
    /// This is deliberately more invasive than the guarded reroll: it rebuilds the entity from a
    /// <see cref="RegenTemplate"/>, so incidental details can shift. Offer it as a fallback, not a default.
    /// </remarks>
    public static IdentityResult ForceNewIdentity(IEnumerable<PKM> mons, SaveFile sav)
    {
        // Seed the "taken" set with every PID already in the save. Without this, regenerating a group of
        // identical clones hands them all the SAME new PID: the legalizer is fed an identical RegenTemplate
        // each time and, for a fixed encounter, converges on the same value. Each entity then differs from its
        // own previous PID (so a naive per-entity check passes) while still colliding with its siblings --
        // trading one shared PID for another and leaving the clone report unchanged.
        var taken = new HashSet<uint>();
        var all = new List<SlotCache>();
        SlotInfoLoader.AddFromSaveFile(sav, all);
        foreach (var slot in all)
        {
            if (slot.Entity.Species != 0)
                taken.Add(slot.Entity.PID);
        }

        int regenerated = 0, failed = 0;
        foreach (var pk in mons)
        {
            taken.Remove(pk.PID); // its own current value must not block it
            if (TryForceNewIdentity(pk, sav, taken))
            {
                taken.Add(pk.PID);
                regenerated++;
            }
            else
            {
                taken.Add(pk.PID);
                failed++;
            }
        }
        return new IdentityResult(regenerated, failed);
    }

    /// <summary>How many distinct seeds to try before giving up on finding an unused identity.</summary>
    private const int IdentityAttempts = 12;

    private static bool TryForceNewIdentity(PKM pk, SaveFile sav, HashSet<uint> taken)
    {
        UseSaveAsFallbackTrainer(sav);
        var identity = TrainerIdentity.From(pk);
        var oldPid = pk.PID;
        var oldEc = pk.EncryptionConstant;
        for (int attempt = 0; attempt < IdentityAttempts; attempt++)
        {
            try
            {
                var regen = new RegenTemplate(pk);
                var blank = EntityBlank.GetBlank(sav);
                var async = sav.GetLegalFromTemplateTimeout(blank, regen);
                if (async.Status != LegalizationResult.Regenerated)
                    return false;

                var converted = EntityConverter.ConvertToType(async.Created, pk.GetType(), out _);
                if (converted is null || converted.Data.Length != pk.Data.Length)
                    return false;
                // Reject a rebuild that kept the old identity, or that landed on one already in use elsewhere
                // in the save -- either way the collision the caller is trying to break would survive.
                if (converted.PID == oldPid && converted.EncryptionConstant == oldEc)
                    continue;
                if (taken.Contains(converted.PID))
                    continue;
                if (!new LegalityAnalysis(converted).Valid)
                    continue;

                converted.Data.CopyTo(pk.Data);
                pk.RefreshChecksum();
                // This routine rerolls PID/EC on purpose; the owner is not part of what it is trying to change.
                PreserveTrainerIdentity(pk, identity);
                OptimizeIVsWithoutCollision(pk, sav, taken);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        return false;
    }

    /// <param name="forceShiny">
    /// Overrides the shiny type the <see cref="RegenTemplate"/> would otherwise inherit from the entity
    /// (<c>RegenSet</c>'s constructor maps an existing Star shiny to <see cref="Shiny.AlwaysStar"/>, which would
    /// keep reproducing Star). Null leaves the inherited value alone.
    /// </param>
    private static bool TryLegalize(PKM pk, SaveFile sav, Shiny? forceShiny)
    {
        UseSaveAsFallbackTrainer(sav);
        var identity = TrainerIdentity.From(pk);
        try
        {
            var regen = new RegenTemplate(pk);
            if (forceShiny is { } shiny)
                regen.Regen.Extra.ShinyType = shiny;
            var blank = EntityBlank.GetBlank(sav);
            // Timeout-wrapped: some entities (e.g. a very old/unusual origin with few or no matching legal
            // encounters) can make the legalizer's internal search run far longer than expected, or effectively
            // never finish. This bounds a single entity's legalization instead of hanging the whole bulk run.
            var async = sav.GetLegalFromTemplateTimeout(blank, regen);
            var (result, status) = (async.Created, async.Status);
            if (status != LegalizationResult.Regenerated)
                return false;

            // Convert back to the entity's original storage format (the legalizer may have produced a different one).
            var converted = EntityConverter.ConvertToType(result, pk.GetType(), out _);
            if (converted is null || converted.Data.Length != pk.Data.Length)
                return false;
            if (!new LegalityAnalysis(converted).Valid)
                return false;

            converted.Data.CopyTo(pk.Data);
            pk.RefreshChecksum();
            PreserveTrainerIdentity(pk, identity);
            return true;
        }
        catch (Exception)
        {
            // The legalizer is a large, format-spanning engine (Gen1-9+) with edge cases it doesn't handle
            // cleanly for every entity (e.g. corrupted/out-of-range data). One bad entity in a bulk run
            // should count as a failure, not take down the whole run.
            return false;
        }
    }

    /// <summary>
    /// Improves the entity's IVs after a forced identity change, keeping the result only if it stays unique and
    /// legal.
    /// </summary>
    /// <remarks>
    /// These are the seed-correlated encounters -- Tera raids and the like -- where PID, Encryption Constant and
    /// all six IVs derive from a single seed. Forcing a new identity therefore re-rolls the IVs as a side
    /// effect, and whichever spread the first legal seed produced is what the Pokemon is left with. That can be
    /// a very poor base spread; Hyper Training then masks it in the displayed stats while the underlying values
    /// stay low. Searching for a better seed is the only way to influence them, which is exactly what
    /// <see cref="IVOptimizer"/> does for this encounter class.
    /// <para/>
    /// Reverted if the optimizer's own regeneration lands on a PID already in use: de-cloning is the point of
    /// this routine, and a better IV spread is not worth reintroducing the collision it just removed.
    /// </remarks>
    private static void OptimizeIVsWithoutCollision(PKM pk, SaveFile sav, HashSet<uint> taken)
    {
        var snapshot = pk.Data.ToArray();
        try
        {
            if (!IVOptimizer.TryOptimize(pk, sav))
                return;
            pk.RefreshChecksum();
            if (!taken.Contains(pk.PID) && new LegalityAnalysis(pk).Valid)
                return;
        }
        catch (Exception)
        {
            // Fall through and revert.
        }
        snapshot.CopyTo(pk.Data);
        pk.RefreshChecksum();
    }

    /// <summary>
    /// Points AutoMod's fallback trainer at the currently loaded save, so anything it regenerates is stamped
    /// with the player's own OT/TID/SID rather than a hardcoded placeholder.
    /// </summary>
    /// <remarks>
    /// Works around a real defect in the vendored engine.
    /// <c>TrainerSettings.GetSavedTrainerData(GameVersion, byte, ITrainerInfo?, LanguageID?)</c> takes a
    /// <c>fallback</c> trainer parameter and then never reads it -- when its trainer database has no match it
    /// returns <c>DefaultFallback(version, lang)</c>, which is built from the static <c>DefaultOT</c>. So the
    /// save file that <see cref="TryLegalize"/> hands the legalizer cannot influence the OT at all, and with no
    /// <c>trainers</c> folder next to the executable that database is always empty. Every regenerated Pokemon
    /// therefore inherits <c>DefaultOT</c>, which upstream ships as "ALM".
    /// <para/>
    /// Setting the defaults from the save is preferable to hardcoding a name: it needs no per-user
    /// configuration, it keeps TID/SID consistent with the OT (a matching name over a mismatched ID pair is
    /// still wrong), and it survives loading a different save. Patching the vendored method directly would be
    /// the tidier fix but makes every upstream merge a conflict, which is why this stays fork-side.
    /// </remarks>
    private static void UseSaveAsFallbackTrainer(SaveFile sav)
    {
        if (sav.OT.Length == 0)
            return;
        TrainerSettings.DefaultOT = sav.OT;
        TrainerSettings.DefaultTID16 = sav.TID16;
        TrainerSettings.DefaultSID16 = sav.SID16;
    }

    /// <summary>
    /// The trainer fields that identify who owns a Pokemon, captured before legalization so they can be put back.
    /// </summary>
    private readonly record struct TrainerIdentity(string Name, ushort TID16, ushort SID16, byte Gender, int Language)
    {
        public static TrainerIdentity From(PKM pk) =>
            new(pk.OriginalTrainerName, pk.TID16, pk.SID16, pk.OriginalTrainerGender, pk.Language);
    }

    /// <summary>
    /// Restores the pre-legalization trainer identity onto <paramref name="pk"/>, keeping it only if the entity
    /// stays legal.
    /// </summary>
    /// <remarks>
    /// The legalizer rebuilds an entity from a template and stamps whatever trainer it resolved, which discards
    /// the original owner even when nothing about the original owner was wrong. That matters most for event
    /// Pokemon: a Mystery Gift whose card has a variable OT legitimately carries the receiving player's name, so
    /// overwriting it with a fallback both loses real provenance and is unrecoverable once saved.
    /// <para/>
    /// Guarded rather than unconditional, because for some encounters the OT is not free -- a gift with a fixed
    /// OT must carry the card's trainer, and an in-game trade must carry the NPC's. In those cases restoring the
    /// old name would re-break the entity the legalizer just fixed, so the guard reverts and the legalizer's
    /// choice stands. "Keep the original where keeping it is legal" is exactly the intended behaviour.
    /// </remarks>
    private static void PreserveTrainerIdentity(PKM pk, TrainerIdentity identity)
    {
        if (identity.Name.Length == 0)
            return; // Nothing meaningful to put back.
        if (pk.OriginalTrainerName == identity.Name && pk.TID16 == identity.TID16 && pk.SID16 == identity.SID16)
            return; // Legalizer already kept it.

        BulkQoLEditor.TryApplyGuarded(pk, p =>
        {
            p.OriginalTrainerName = identity.Name;
            p.TID16 = identity.TID16;
            p.SID16 = identity.SID16;
            p.OriginalTrainerGender = identity.Gender;
            p.Language = identity.Language;
        });
    }
}
