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
