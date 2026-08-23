using System;
using System.Collections.Generic;

namespace PKHeX.Core;

/// <summary>
/// Applies a single common edit (Ball, Met Location, Shiny state, ...) across many <see cref="PKM"/> at once,
/// keeping the change only if the entity remains a legal encounter afterward.
/// </summary>
/// <remarks>
/// Unlike <see cref="EntityBatchProcessor"/>, this does not use the property=value instruction language;
/// it is a small, fixed set of guarded operations meant to be driven by a simple checkbox-style UI.
/// </remarks>
public static class BulkQoLEditor
{
    /// <summary>
    /// Tally of what happened when a guarded edit was applied across a collection of <see cref="PKM"/>.
    /// </summary>
    /// <param name="Modified">Entities that were changed and remained legal.</param>
    /// <param name="SkippedIllegal">Entities where the edit was reverted because it made the entity illegal.</param>
    /// <param name="SkippedInvalid">Entities that were skipped entirely (empty slot).</param>
    /// <param name="AlreadyLegal">Entities that were intentionally left untouched (already didn't need the edit, e.g. moves already legal, or the edit is unsupported for that entity's format).</param>
    public readonly record struct BulkEditResult(int Modified, int SkippedIllegal, int SkippedInvalid, int AlreadyLegal = 0)
    {
        public int Total => Modified + SkippedIllegal + SkippedInvalid + AlreadyLegal;
    }

    /// <summary>
    /// Sets <see cref="PKM.Ball"/> on every entity, reverting any entity that becomes illegal as a result.
    /// </summary>
    public static BulkEditResult SetBallForAll(IEnumerable<PKM> mons, byte ball) =>
        ApplyGuardedToAll(mons, pk => pk.Ball = ball);

    /// <summary>
    /// Sets <see cref="PKM.MetLocation"/> (and optionally <see cref="PKM.MetLevel"/>) on every entity, reverting
    /// any entity that becomes illegal as a result. Entities that would become illegal simply keep their
    /// existing (legal) met location instead of being touched.
    /// </summary>
    public static BulkEditResult SetMetLocationForAll(IEnumerable<PKM> mons, ushort location, byte? metLevel = null) =>
        ApplyGuardedToAll(mons, pk =>
        {
            pk.MetLocation = location;
            if (metLevel is { } lvl)
                pk.MetLevel = lvl;
        });

    /// <summary>
    /// Sets the shiny state on every entity, reverting any entity that becomes illegal as a result
    /// (e.g. a fixed-PID event encounter that cannot legally be shiny).
    /// </summary>
    public static BulkEditResult SetShinyForAll(IEnumerable<PKM> mons, bool shiny) =>
        ApplyGuardedToAll(mons, pk => pk.SetIsShiny(shiny));

    /// <summary>
    /// Maxes out PP Ups (and heals PP to match) for every move slot on every entity, reverting any entity
    /// that becomes illegal as a result (e.g. an egg, or a format with no PP Ups at all).
    /// </summary>
    public static BulkEditResult SetMaxPPUpsForAll(IEnumerable<PKM> mons) =>
        ApplyGuardedToAll(mons, pk =>
        {
            if (Legal.IsPPUpAvailable(pk) && !pk.IsEgg)
                pk.SetMaximumPPUps();
        });

    /// <summary>
    /// For every entity whose current moveset isn't legal, replaces it (and its relearn moves, if those are
    /// also illegal) with a suggested legal moveset. Entities whose moves are already legal are left untouched.
    /// Reverts if no legal moveset could be found.
    /// </summary>
    public static BulkEditResult SetLegalMovesForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, alreadyLegal = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            bool movesAlreadyLegal;
            try
            {
                movesAlreadyLegal = MoveResult.AllValid(new LegalityAnalysis(pk).Info.Moves);
            }
            catch (Exception)
            {
                // Corrupted/out-of-range data can make analysis itself throw; treat as "couldn't fix" rather
                // than letting one bad entity take down the whole bulk run.
                skippedIllegal++;
                continue;
            }

            if (movesAlreadyLegal)
            {
                alreadyLegal++;
                continue;
            }

            if (TryApplyGuarded(pk, FixMoves))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, alreadyLegal);

        static void FixMoves(PKM pk)
        {
            pk.SetMoveset();
            var la = new LegalityAnalysis(pk);
            if (la.Parsed && !MoveResult.AllValid(la.Info.Relearn))
                pk.SetRelearnMoves(la);
        }
    }

    /// <summary>
    /// Sets all six IVs to 31 on every entity, reverting any entity that becomes illegal as a result
    /// (e.g. an event gift with fixed/preset IVs).
    /// </summary>
    public static BulkEditResult SetMaxIVsForAll(IEnumerable<PKM> mons) =>
        ApplyGuardedToAll(mons, pk => pk.SetIVs(MaxIVs));

    // PKM.SetIVs/SetEVs order is HP, ATK, DEF, SPE, SPA, SPD (Speed before the special stats).
    private static readonly int[] MaxIVs = [31, 31, 31, 31, 31, 31];
    private static readonly int[] PhysicalSpread = [0, 252, 0, 252, 0, 0]; // 252 Atk / 252 Spe
    private static readonly int[] SpecialSpread = [0, 0, 0, 252, 252, 0]; // 252 SpA / 252 Spe

    /// <summary>
    /// A competitive Nature + EV spread preset for <see cref="SetNatureEVPresetForAll"/>.
    /// </summary>
    public enum NatureEVPreset
    {
        /// <summary>252 Atk / 252 Spe, +Spe -SpA.</summary>
        Jolly,
        /// <summary>252 SpA / 252 Spe, +Spe -Atk.</summary>
        Timid,
        /// <summary>252 Atk / 252 Spe, +Atk -SpA.</summary>
        Adamant,
        /// <summary>252 SpA / 252 Spe, +SpA -Atk.</summary>
        Modest,
    }

    /// <summary>
    /// Sets Nature and EVs to a competitive preset (252/252, remaining EVs untouched at 0) on every entity,
    /// reverting any entity that becomes illegal as a result (e.g. a nature-locked event encounter).
    /// </summary>
    /// <remarks>
    /// Generation 3/4 entities are skipped entirely: their Nature is derived from PID, so forcing it re-rolls
    /// the PID via <see cref="CommonEdits.SetNature"/>-&gt;<see cref="PKM.SetPIDNature"/>, which silently clears
    /// shininess and can flip the entity's ability — legal, but not a side effect this bulk edit should cause
    /// without the player asking for it directly.
    /// </remarks>
    public static BulkEditResult SetNatureEVPresetForAll(IEnumerable<PKM> mons, NatureEVPreset preset)
    {
        var (nature, evs) = preset switch
        {
            NatureEVPreset.Jolly => (Nature.Jolly, PhysicalSpread),
            NatureEVPreset.Adamant => (Nature.Adamant, PhysicalSpread),
            NatureEVPreset.Timid => (Nature.Timid, SpecialSpread),
            NatureEVPreset.Modest => (Nature.Modest, SpecialSpread),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
        };

        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedGen34 = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            if (pk.Format is 3 or 4)
            {
                skippedGen34++;
                continue;
            }

            if (TryApplyGuarded(pk, p =>
            {
                p.SetNature(nature);
                p.SetEVs(evs);
            }))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedGen34);
    }

    /// <summary>
    /// Maxes out height/weight/scale (whichever the entity's format supports) on every entity, reverting any
    /// entity that becomes illegal as a result (e.g. an Alpha with a fixed non-max scale, or a mon carrying
    /// a "Mini" size mark). Entities from a pre-Gen8 format, which has no size-scalar concept at all, are
    /// skipped rather than counted as modified.
    /// </summary>
    public static BulkEditResult SetMaxSizeForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedUnsupported = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            if (pk is not (IScaledSize or IScaledSize3))
            {
                skippedUnsupported++;
                continue;
            }

            if (TryApplyGuarded(pk, MaxSize))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedUnsupported);

        static void MaxSize(PKM pk)
        {
            // SV's Scale is the authoritative size byte; HeightScalar/WeightScalar only matter once HOME-tracked,
            // at which point they must match Scale exactly -- so always set all three that apply.
            if (pk is IScaledSize3 s3)
                s3.Scale = byte.MaxValue;
            if (pk is IScaledSize s2)
            {
                s2.HeightScalar = byte.MaxValue;
                s2.WeightScalar = byte.MaxValue;
            }
            // Gen9 awards the Jumbo Mark automatically at capture when Scale is maxed, so "max Scale without the
            // mark" is a state the game itself never produces. PKHeX won't flag it -- RibbonVerifierMark9 only
            // checks mark-without-scale, not scale-without-mark -- but it leaves an inconsistency visible to
            // anything that cross-checks the pair. The guard reverts if this encounter can't legally hold the
            // mark (see MarkRules.IsEncounterMarkLost, e.g. Nincada -> Shedinja), so it's safe to just try.
            if (pk is IRibbonSetMark9 { RibbonMarkJumbo: false } mark9 && pk is IScaledSize3 { Scale: byte.MaxValue })
                mark9.RibbonMarkJumbo = true;
            // Resync the derived displayed height/weight (meters/kg) where the format tracks it separately;
            // stale Absolute values aren't a legality problem here, but leaving them stale would be a visible bug.
            if (pk is IScaledSizeValue sv)
            {
                sv.ResetHeight();
                sv.ResetWeight();
            }
        }
    }

    /// <summary>
    /// Clears leftover Nickname/OT/HT "trash" bytes and fixes Handling Trainer memory that the legality
    /// checker flags as "Trash Bytes should be cleared" / "Memory: Not cleared properly" / "Memory: Handling
    /// Trainer Memory missing" — edits made outside the normal name-entry UI (or an HT that got added/removed
    /// by hand) can leave these dirty without the entity's own displayed data looking wrong. Reverts if the
    /// cleanup somehow makes the entity illegal.
    /// </summary>
    /// <remarks>
    /// Trash-byte clearing is only meaningful for Format 8+ and LGPE (Gen7b) — earlier/other formats aren't
    /// checked for it at all.
    /// <para/>
    /// Handling Trainer memory: cleared when the entity has no real Handling Trainer
    /// (<see cref="PKM.IsUntraded"/>); when it does, this fills in the generic "Link Trade" memory PK6-PK8
    /// require (mirroring AutoLegalityMod's own <c>SetSuggestedMemories</c> logic) since PK9/PA8/PB8 dropped
    /// the requirement entirely and are always cleared instead. Also fixes the Handling Trainer's recorded
    /// language to match <paramref name="sav"/>'s own language when it's the entity's current handler (a
    /// mismatch there is flagged Fishy — e.g. shows as "(None)" in the OT/Misc tab when it was never set).
    /// This can't fix Original Trainer memory: unlike HT memory, the correct OT memory value is either
    /// historically fixed per-encounter/Mystery Gift or must be zero, with no generic safe guess — that needs
    /// the full regeneration <see cref="BulkAutoLegalize"/> (Auto-enforce legality) already does by re-deriving
    /// the matched encounter.
    /// </remarks>
    public static BulkEditResult FixTrashAndMemoryForAll(IEnumerable<PKM> mons, SaveFile sav)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            // Resolve the matched encounter BEFORE mutating: a handful of Gen9 gift encounters legitimately ship
            // with a Handling Trainer language even while untraded, and must keep it (MemoryVerifier lines
            // 102-110). Everything else untraded must have it zeroed. Deciding this mid-mutation would mean
            // matching an encounter against half-edited data.
            bool giftKeepsLanguage;
            try
            {
                giftKeepsLanguage = new LegalityAnalysis(pk).EncounterMatch is EncounterStatic9 { GiftWithLanguage: true };
            }
            catch (Exception)
            {
                giftKeepsLanguage = false;
            }

            if (TryApplyGuarded(pk, Fix))
                modified++;
            else
                skippedIllegal++;

            void Fix(PKM entity)
            {
                if (entity.Format >= 8 || entity.Context == EntityContext.Gen7b)
                {
                    entity.SetString(entity.NicknameTrash, entity.Nickname, entity.Nickname.Length, StringConverterOption.ClearZero);
                    entity.SetString(entity.OriginalTrainerTrash, entity.OriginalTrainerName, entity.OriginalTrainerName.Length, StringConverterOption.ClearZero);
                    if (entity.HandlingTrainerTrash.Length != 0)
                        entity.SetString(entity.HandlingTrainerTrash, entity.HandlingTrainerName, entity.HandlingTrainerName.Length, StringConverterOption.ClearZero);
                }
                FixHandlingTrainerMemory(entity);
                FixHandlingTrainerLanguage(entity, sav, giftKeepsLanguage);
            }
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid);
    }

    private static void FixHandlingTrainerMemory(PKM pk)
    {
        // PK9 has its own canonical routine that clears more than the four memory fields -- for an untraded
        // entity it also zeroes HandlingTrainerGender/Friendship and the HT name trash (deliberately leaving
        // HandlingTrainerLanguage alone, which gifts require). Clearing only the memories left
        // HandlingTrainerFriendship populated, which HistoryVerifier flags Invalid -- so the guard reverted the
        // whole edit and the entity never actually got fixed.
        if (pk is PK9 pk9)
        {
            pk9.FixMemories();
            return;
        }

        if (pk is not IMemoryHT ht)
            return;

        if (pk.IsUntraded)
        {
            ht.ClearMemoriesHT();
            return;
        }

        // Traded: needs *some* valid HT memory (or, for the formats that dropped the requirement, none at all).
        switch (pk)
        {
            case PA8 or PB8:
                ht.ClearMemoriesHT();
                break;
            case PK8:
                ht.SetTradeMemoryHT8();
                break;
            case PK7 or PK6:
                ht.SetTradeMemoryHT6(bank: true);
                break;
            // Other traded formats (PB7, PA9, ...) aren't covered by a known-safe generic suggestion;
            // leave them untouched here rather than guess -- Auto-enforce legality is the fallback for those.
        }
    }

    /// <summary>
    /// Regenerates the PID (Gen3+), HOME Tracker (when one is already present), and Encryption Constant (Gen6+)
    /// to fresh random values -- while preserving species/gender/nature/form/shininess exactly -- reverting any
    /// entity that becomes illegal as a result. Intended to resolve a HOME upload rejection caused by a cloned
    /// PID/Tracker/EC collision (see <see cref="CloneDetector"/>).
    /// </summary>
    /// <remarks>
    /// PID regeneration is the important part here, not an afterthought: <see cref="CloneDetector"/>'s most
    /// common finding (<c>BulkCloneDetectedDetails</c>, and the raw-PID-sharing findings) is keyed on
    /// Species+PID+IVs+Form -- an <i>earlier version of this method only touched Tracker/EC, which does nothing
    /// to break that particular collision</i> (PID is untouched by an EC change). Reuses <see cref="PKM.SetShiny"/>
    /// and <see cref="PKM.SetPIDGender"/> -- both already-correct, already-tested PKHeX primitives -- to reroll
    /// PID while looping until the entity's current shininess is preserved, rather than hand-rolling PID math.
    /// <para/>
    /// An existing nonzero HOME Tracker is <b>cleared to zero</b>, never replaced with an invented value. A
    /// Tracker is a GUID issued by HOME's own servers; PKHeX's <see cref="TransferVerifier"/> says so explicitly
    /// ("Transfer a 0-Tracker pk to HOME to get assigned a valid Tracker via the game it originated from.
    /// Don't make one up."). Fabricating one claims an ID HOME never issued, which HOME can check against its
    /// own records; zero instead means "never uploaded", so HOME assigns a fresh Tracker on the next transfer.
    /// Zeroing is also self-guarding: for entities where a Tracker is genuinely required (GO transfers, HOME
    /// gifts, cross-generation transfers -- see <see cref="HomeTrackerUtil.IsRequired"/>) clearing it produces
    /// <c>TransferTrackerMissing</c> and the guard reverts the whole edit, leaving those untouched.
    /// <para/>
    /// Encryption Constant is independently regenerated only for Format 6+: Generations 3-5 <i>define</i> EC as
    /// equal to PID (see <see cref="CommonEdits.SetRandomEC"/>), which the PID reroll above already keeps in
    /// sync for those entities. Entities with nothing applicable (a Gen1/2 entity with no PID and no Tracker)
    /// are reported as skipped rather than silently counted as "fixed".
    /// </remarks>
    public static BulkEditResult RegeneratePIDTrackerAndECForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNothingToDo = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            var hasPIDToChange = pk.Format >= 3; // Gen1/2 have no PID -- DVs instead, not touched here
            var hasTrackerToClear = pk is IHomeTrack { Tracker: not 0 };
            if (!hasPIDToChange && !hasTrackerToClear)
            {
                skippedNothingToDo++;
                continue;
            }

            if (TryApplyGuarded(pk, RegenerateIdentity))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNothingToDo);

        static void RegenerateIdentity(PKM pk)
        {
            // Clear, never fabricate -- see the remarks above.
            if (pk is IHomeTrack { Tracker: not 0 } home)
                home.Tracker = 0;
            if (pk.Format >= 3)
            {
                // Reroll PID keeping species/gender/version/nature/form and current shininess identical --
                // these two existing primitives also keep EncryptionConstant in sync for Gen3-5 (EC == PID there).
                if (pk.IsShiny)
                    pk.SetShiny();
                else
                    pk.SetPIDGender(pk.Gender);
            }
            if (pk.Format >= 6)
                pk.SetRandomEC(); // Gen6+ EC is independent of PID; the reroll above only synced it for Gen3-5 origin
        }
    }

    private static void FixHandlingTrainerLanguage(PKM pk, SaveFile sav, bool giftKeepsLanguage)
    {
        if (pk is not IHandlerLanguage lang)
            return;

        if (pk.IsUntraded)
        {
            // MemoryVerifier.GetIsHTLanguageValid: an untraded entity must have HT language 0, unless it's one of
            // the Gen9 gift encounters that ships with one -- those must instead match the entity's own language.
            // PK9.FixMemories deliberately won't touch this field (it can't tell the two cases apart), so a stale
            // language left behind by hand-removing a Handling Trainer would otherwise make the whole guarded
            // edit revert, silently failing to fix anything.
            lang.HandlingTrainerLanguage = giftKeepsLanguage ? (byte)pk.Language : (byte)0;
            return;
        }

        // Traded: only meaningful when this save's trainer is the entity's current handler -- that's the scenario
        // the legality checker actually compares against (HistoryVerifier.CheckHandlingTrainerEquals).
        if (pk.CurrentHandler == 1 && lang.HandlingTrainerLanguage != sav.Language)
            lang.HandlingTrainerLanguage = (byte)sav.Language;
    }

    private static BulkEditResult ApplyGuardedToAll(IEnumerable<PKM> mons, Action<PKM> mutate)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            if (TryApplyGuarded(pk, mutate))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid);
    }

    /// <summary>
    /// Applies <paramref name="mutate"/> to <paramref name="pk"/>, keeping the change only if the entity
    /// is still a legal encounter afterward; otherwise the entity's original data is restored untouched.
    /// </summary>
    /// <returns>True if the mutation was kept, false if it was reverted.</returns>
    public static bool TryApplyGuarded(PKM pk, Action<PKM> mutate)
    {
        Span<byte> backup = stackalloc byte[pk.Data.Length];
        pk.Data.CopyTo(backup);

        try
        {
            mutate(pk);
            pk.RefreshChecksum();

            if (new LegalityAnalysis(pk).Valid)
                return true;
        }
        catch (Exception)
        {
            // Corrupted/out-of-range data (e.g. a garbage species byte) can make the mutation or the legality
            // check itself throw. A guarded edit should never be able to crash the caller -- fall through and
            // revert, same as an ordinary "this made it illegal" result.
        }

        backup.CopyTo(pk.Data);
        pk.RefreshChecksum();
        return false;
    }
}
