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
        /// <summary>252 Atk / 252 Spe, +Atk -SpA.</summary>
        Jolly,
        /// <summary>252 SpA / 252 Spe, +SpA -Atk.</summary>
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
    public static BulkEditResult FixTrashAndMemoryForAll(IEnumerable<PKM> mons, SaveFile sav) =>
        ApplyGuardedToAll(mons, pk =>
        {
            if (pk.Format >= 8 || pk.Context == EntityContext.Gen7b)
            {
                pk.SetString(pk.NicknameTrash, pk.Nickname, pk.Nickname.Length, StringConverterOption.ClearZero);
                pk.SetString(pk.OriginalTrainerTrash, pk.OriginalTrainerName, pk.OriginalTrainerName.Length, StringConverterOption.ClearZero);
                if (pk.HandlingTrainerTrash.Length != 0)
                    pk.SetString(pk.HandlingTrainerTrash, pk.HandlingTrainerName, pk.HandlingTrainerName.Length, StringConverterOption.ClearZero);
            }
            FixHandlingTrainerMemory(pk);
            FixHandlingTrainerLanguage(pk, sav);
        });

    private static void FixHandlingTrainerMemory(PKM pk)
    {
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
            case PK9 or PA8 or PB8:
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
    /// Regenerates the HOME Tracker (when one is already present) and/or Encryption Constant (Gen6+ only) to
    /// fresh random values, reverting any entity that becomes illegal as a result. Intended to resolve a HOME
    /// upload rejection caused by a cloned Tracker/PID/EC collision (see <see cref="CloneDetector"/>) without
    /// touching anything else about the entity — species/IVs/moves/etc. are left exactly as they are.
    /// </summary>
    /// <remarks>
    /// Only entities that already have a nonzero <see cref="IHomeTrack.Tracker"/> are touched there — giving a
    /// fake Tracker to an entity that has never been through HOME is itself a legality violation
    /// (<c>TransferTrackerShouldBeZero</c>), and the guard would just revert it anyway.
    /// <para/>
    /// Encryption Constant is only independently regenerated for Format 6+: Generations 3-5 <i>define</i> EC as
    /// equal to PID (see <see cref="CommonEdits.SetRandomEC"/>), so there's nothing to change there without also
    /// changing PID — a much bigger, riskier operation (can cascade into shininess/IVs/gender/nature) that this
    /// method deliberately does not attempt. Entities with neither applicable are reported as skipped rather than
    /// silently counted as "fixed".
    /// </remarks>
    public static BulkEditResult RegenerateTrackerAndECForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNothingToDo = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            var hasTrackerToChange = pk is IHomeTrack { Tracker: not 0 };
            var hasECToChange = pk.Format >= 6;
            if (!hasTrackerToChange && !hasECToChange)
            {
                skippedNothingToDo++;
                continue;
            }

            if (TryApplyGuarded(pk, RegenerateTrackerAndEC))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNothingToDo);

        static void RegenerateTrackerAndEC(PKM pk)
        {
            if (pk is IHomeTrack { Tracker: not 0 } home)
                home.Tracker = GetRandomNonZeroTracker();
            if (pk.Format >= 6)
                pk.SetRandomEC();
        }
    }

    private static ulong GetRandomNonZeroTracker()
    {
        Span<byte> buffer = stackalloc byte[8];
        Random.Shared.NextBytes(buffer);
        var value = BitConverter.ToUInt64(buffer);
        return value == 0 ? 1 : value; // 0 means "no tracker" -- reroll rather than accidentally clear it
    }

    private static void FixHandlingTrainerLanguage(PKM pk, SaveFile sav)
    {
        if (pk is not IHandlerLanguage lang || pk.IsUntraded)
            return;

        // Only meaningful when this save's trainer is the entity's current handler -- that's the scenario the
        // legality checker actually compares against (HistoryVerifier.CheckHandlingTrainerEquals).
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
