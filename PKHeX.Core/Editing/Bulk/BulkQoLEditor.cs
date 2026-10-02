using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
    /// <param name="AlreadyIllegal">Entities that were illegal <i>before</i> the edit was attempted. A guarded
    /// edit can never succeed on these -- the guard requires the entity to be legal afterward, which an
    /// already-illegal entity cannot be -- so reporting them as "the edit would have made it illegal" is
    /// actively misleading. They need their underlying legality fixed first.</param>
    public readonly record struct BulkEditResult(int Modified, int SkippedIllegal, int SkippedInvalid, int AlreadyLegal = 0, int AlreadyIllegal = 0)
    {
        public int Total => Modified + SkippedIllegal + SkippedInvalid + AlreadyLegal + AlreadyIllegal;
    }

    /// <summary>
    /// Sets <see cref="PKM.Ball"/> on every entity, reverting any entity that becomes illegal as a result.
    /// </summary>
    /// <summary>
    /// Outcome of a bulk Ball assignment. Separated from <see cref="BulkEditResult"/> because "the ball is
    /// wrong for this encounter" and "this Pokemon was already broken for unrelated reasons" are different
    /// answers that demand different follow-up, and collapsing them into one count is what made this step look
    /// unreliable.
    /// </summary>
    /// <param name="Changed">Ball applied; the entity was legal before and stayed legal.</param>
    /// <param name="ChangedWhileIllegal">Ball applied to an entity that was already illegal, without adding any
    /// new problem. Its legality still needs fixing, but the ball is now what was asked for.</param>
    /// <param name="NotLegalForEncounter">Reverted: this encounter cannot have this ball.</param>
    /// <param name="AlreadySet">Already in the requested ball; nothing to do.</param>
    /// <param name="Unsupported">Gen1/Gen2 entities, which store no ball at all.</param>
    /// <param name="Empty">Empty slots.</param>
    /// <param name="ProtectedEvent">Cherish Ball entities, refused outright -- see <see cref="IsEventDistribution"/>.</param>
    public readonly record struct BallEditResult(
        int Changed,
        int ChangedWhileIllegal,
        int NotLegalForEncounter,
        int AlreadySet,
        int Unsupported,
        int Empty,
        int ProtectedEvent);

    /// <summary>
    /// Sets <see cref="PKM.Ball"/> on every entity that can legally hold it, leaving the rest untouched.
    /// </summary>
    /// <remarks>
    /// Designed to be run repeatedly with progressively rarer balls: each pass only changes the entities the
    /// requested ball is actually legal for, so a wide ball can be laid down as a baseline and narrower ones
    /// layered over it.
    /// <para/>
    /// <b>Later passes overwrite earlier ones.</b> An entity legal in both Luxury and Beast Balls ends up in
    /// whichever was applied last, so run the ball you care most about last, not first.
    /// <para/>
    /// Entities that are already illegal for unrelated reasons are no longer lumped in with "this ball is not
    /// legal here". A legality-guarded edit can never succeed on them -- the guard demands legality afterward,
    /// which they cannot reach -- so under the old strict-only guard every one of them was reported as a ball
    /// rejection and never re-balled, which on a save with many illegal entities looks like the step simply not
    /// working. They now go through a non-worsening guard instead: the ball is applied as long as it introduces
    /// no new legality finding, and is counted separately so the distinction stays visible.
    /// </remarks>
    public static BallEditResult SetBallForAll(IEnumerable<PKM> mons, byte ball)
    {
        int changed = 0, changedWhileIllegal = 0, notLegal = 0, alreadySet = 0, unsupported = 0, empty = 0, protectedEvent = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                empty++;
                continue;
            }
            if (IsEventDistribution(pk))
            {
                protectedEvent++;
                continue;
            }
            if (pk.Format < 3)
            {
                unsupported++; // Gen1/2 have no Ball field to set.
                continue;
            }
            if (pk.Ball == ball)
            {
                alreadySet++;
                continue;
            }

            bool legalBefore;
            try
            {
                legalBefore = new LegalityAnalysis(pk).Valid;
            }
            catch (Exception)
            {
                legalBefore = false;
            }

            var applied = ball;
            if (legalBefore)
            {
                if (TryApplyGuarded(pk, p => p.Ball = applied))
                    changed++;
                else
                    notLegal++;
            }
            else if (TryApplyNonWorsening(pk, p => p.Ball = applied))
            {
                changedWhileIllegal++;
            }
            else
            {
                notLegal++;
            }
        }
        return new BallEditResult(changed, changedWhileIllegal, notLegal, alreadySet, unsupported, empty, protectedEvent);
    }

    /// <param name="Changed">Entities renamed while staying legal.</param>
    /// <param name="ChangedWhileIllegal">Entities that were already illegal and were renamed without adding a new finding.</param>
    /// <param name="NotLegalForEncounter">Entities whose encounter pins the OT name, so renaming was refused.</param>
    /// <param name="AlreadySet">Entities that already carried the requested name.</param>
    /// <param name="TooLong">Entities whose format cannot hold a name this long.</param>
    /// <param name="Empty">Empty slots.</param>
    /// <param name="ProtectedEvent">Cherish Ball event entities, never touched.</param>
    public readonly record struct TrainerNameEditResult(
        int Changed, int ChangedWhileIllegal, int NotLegalForEncounter,
        int AlreadySet, int TooLong, int Empty, int ProtectedEvent);

    /// <summary>
    /// Renames the Original Trainer on every entity the change is legal for, leaving the rest untouched.
    /// </summary>
    /// <remarks>
    /// Only the OT <b>name</b> is written. Trainer ID, secret ID, gender and language are deliberately left
    /// alone: they are a matched set with the name on a real trainer, but rewriting them is a much larger claim
    /// than a rename and would silently re-own every Pokemon in the box. Note the consequence -- after this the
    /// name and the ID pair describe different trainers, which is fine for the legality checker (it does not
    /// tie the two together for ordinary encounters) but means these entities no longer look "yours" to anything
    /// that keys on ID, including the untraded/handler logic.
    /// <para/>
    /// Plenty of encounters pin the OT name and will simply refuse: in-game trades carry the NPC's trainer, and
    /// Mystery Gifts with a fixed OT carry the card's. Those are counted under
    /// <see cref="TrainerNameEditResult.NotLegalForEncounter"/> rather than being forced.
    /// <para/>
    /// Two guards, matching <see cref="SetBallForAll"/>. An entity that is legal beforehand must still be legal
    /// afterward. One that is already illegal for unrelated reasons can never satisfy that, so it goes through
    /// the non-worsening guard instead and is counted separately -- otherwise every illegal entity would be
    /// reported as a naming rejection and the step would look broken on a save full of them.
    /// <para/>
    /// Cherish Ball entities are skipped outright rather than left to the guard, for the same reason the ball and
    /// met-location setters skip them: an already-illegal event Pokemon would pass the non-worsening guard (the
    /// rename adds no new finding when the encounter already failed to match) and lose the distribution OT
    /// permanently. See <see cref="IsEventDistribution"/>.
    /// <para/>
    /// The OT name is on HOME's immutable list, so this honours the "skip HOME-registered" filter.
    /// </remarks>
    public static TrainerNameEditResult SetOriginalTrainerNameForAll(IEnumerable<PKM> mons, string name)
    {
        int changed = 0, changedWhileIllegal = 0, notLegal = 0, alreadySet = 0, tooLong = 0, empty = 0, protectedEvent = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                empty++;
                continue;
            }
            if (IsEventDistribution(pk))
            {
                protectedEvent++;
                continue;
            }
            // Length caps differ per format (Gen1/2 are far shorter than Gen6+), so this is per entity rather
            // than a single check up front.
            if (name.Length > pk.MaxStringLengthTrainer)
            {
                tooLong++;
                continue;
            }
            if (pk.OriginalTrainerName == name)
            {
                alreadySet++;
                continue;
            }

            bool legalBefore;
            try
            {
                legalBefore = new LegalityAnalysis(pk).Valid;
            }
            catch (Exception)
            {
                legalBefore = false;
            }

            if (legalBefore)
            {
                if (TryApplyGuarded(pk, p => p.OriginalTrainerName = name))
                    changed++;
                else
                    notLegal++;
            }
            else if (TryApplyNonWorsening(pk, p => p.OriginalTrainerName = name))
            {
                changedWhileIllegal++;
            }
            else
            {
                notLegal++;
            }
        }
        return new TrainerNameEditResult(changed, changedWhileIllegal, notLegal, alreadySet, tooLong, empty, protectedEvent);
    }

    /// <summary>
    /// Applies <paramref name="mutate"/> and keeps it as long as it introduces no new Invalid finding.
    /// </summary>
    /// <remarks>
    /// Weaker than <see cref="TryApplyReducingInvalid"/>, which additionally demands that the problem count go
    /// <i>down</i>. That is the right bar for a repair, but the wrong one for a preference like Ball: painting
    /// a ball onto an entity that is illegal for some unrelated reason will never reduce the finding count, so
    /// requiring it to would reject every such entity forever.
    /// <para/>
    /// Requiring the resulting finding set to be a subset of the original still makes it impossible for this to
    /// introduce a problem the entity did not already have.
    /// </remarks>
    private static bool TryApplyNonWorsening(PKM pk, Action<PKM> mutate)
    {
        var before = GetInvalidCodes(pk);
        Span<byte> backup = stackalloc byte[pk.Data.Length];
        pk.Data.CopyTo(backup);
        try
        {
            mutate(pk);
            pk.RefreshChecksum();
            if (GetInvalidCodes(pk).IsSubsetOf(before))
                return true;
        }
        catch (Exception)
        {
            // Fall through and revert, same as an ordinary "this didn't work" result.
        }
        backup.CopyTo(pk.Data);
        pk.RefreshChecksum();
        return false;
    }

    /// <summary>
    /// Sets <see cref="PKM.MetLocation"/> (and optionally <see cref="PKM.MetLevel"/>) on every entity, reverting
    /// any entity that becomes illegal as a result. Entities that would become illegal simply keep their
    /// existing (legal) met location instead of being touched.
    /// </summary>
    public static BulkEditResult SetMetLocationForAll(IEnumerable<PKM> mons, ushort location, byte? metLevel = null)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, protectedEvent = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            // Met data is pinned by the distribution record; overwriting it is unrecoverable.
            if (IsEventDistribution(pk))
            {
                protectedEvent++;
                continue;
            }

            if (TryApplyGuarded(pk, Apply))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, protectedEvent);

        void Apply(PKM pk)
        {
            pk.MetLocation = location;
            if (metLevel is { } lvl)
                pk.MetLevel = lvl;
        }
    }

    /// <summary>
    /// Sets the shiny state on every entity, reverting any entity that becomes illegal as a result
    /// (e.g. a fixed-PID event encounter that cannot legally be shiny).
    /// </summary>
    /// <param name="preferSquare">
    /// When making entities shiny, aim for Square (<see cref="PKM.ShinyXor"/> == 0) rather than Star.
    /// Only meaningful for Format 8+, which is where the two are visually differentiated
    /// (see <see cref="ShinyExtensions.IsSquareShinyExist"/>). Entities that cannot legally be Square
    /// fall back to an ordinary shiny rather than being left unchanged.
    /// </param>
    public static BulkEditResult SetShinyForAll(IEnumerable<PKM> mons, bool shiny, bool preferSquare = true)
    {
        if (!shiny)
            return ApplyGuardedToAll(mons, pk => pk.SetIsShiny(false));

        int modified = 0, skippedIllegal = 0, skippedInvalid = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            // Try Square first, then settle for any shiny. Two guarded attempts rather than one, so an
            // encounter that is locked to Star (or a fixed-PID gift) still ends up shiny instead of being
            // reverted entirely just because the Square preference couldn't be honoured.
            if (CanPreferSquare(pk, preferSquare) && TryApplyGuarded(pk, static p => p.SetShiny(Shiny.AlwaysSquare)))
                modified++;
            else if (TryApplyGuarded(pk, static p => p.SetIsShiny(true)))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid);
    }

    /// <summary>
    /// Square vs. Star is only a distinct thing from Gen8 onward. <see cref="CommonEdits.SetShiny"/> also
    /// refuses to honour a specific shiny type for fateful/GO entities (it falls back to a plain reroll), so
    /// there's no point paying for the attempt on those.
    /// </summary>
    private static bool CanPreferSquare(PKM pk, bool preferSquare) =>
        preferSquare && pk.Format >= 8 && !pk.FatefulEncounter && pk.Version != GameVersion.GO;

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

            if (TryApplyProgressive(pk, FixMoves))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, alreadyLegal);

    }

    /// <summary>
    /// Re-derives the moveset (and relearn moves, when those are what is wrong) from the entity's currently
    /// matched encounter.
    /// </summary>
    private static void FixMoves(PKM pk)
    {
        pk.SetMoveset();
        var la = new LegalityAnalysis(pk);
        if (la.Parsed && !MoveResult.AllValid(la.Info.Relearn))
            pk.SetRelearnMoves(la);
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
    /// <param name="scaleSearchLimit">
    /// How many values below maximum to try when the maximum itself cannot be applied legally. Each attempt
    /// costs a full legality analysis, so this is deliberately bounded rather than scanning all 255.
    /// </param>
    public static BulkEditResult SetMaxSizeForAll(IEnumerable<PKM> mons, int scaleSearchLimit = 64) =>
        SetSizeExtremeForAll(mons, SizeGoal.Maximize, scaleSearchLimit);

    /// <summary>
    /// Shrinks every entity's height/weight/scale to the smallest legal value, the "Mini" counterpart to
    /// <see cref="SetMaxSizeForAll"/>. Same three-tier plus seed-search escalation, just walking down instead
    /// of up (and never awards the Jumbo mark, which only exists at maximum size).
    /// </summary>
    public static BulkEditResult SetMinSizeForAll(IEnumerable<PKM> mons, int scaleSearchLimit = 64) =>
        SetSizeExtremeForAll(mons, SizeGoal.Minimize, scaleSearchLimit);

    /// <summary>
    /// Direction for <see cref="SetSizeExtremeForAll"/> and <see cref="TrySetSizeExtreme"/>.
    /// </summary>
    public enum SizeGoal { Maximize, Minimize }

    /// <summary>
    /// Pushes every entity's height/weight/scale to the legal extreme in the requested direction (255/max or
    /// 0/min).
    /// </summary>
    /// <remarks>
    /// Entities from a pre-Gen8 format, which has no size-scalar concept at all, are skipped rather than
    /// counted as modified.
    /// <para/>
    /// Four escalating tiers, each individually guarded, so a failure at one step still leaves the entity
    /// untouched and lets the next step try:
    /// <list type="number">
    /// <item>Scale, Height and Weight together to the extreme, with the Jumbo Mark (maximize only -- the game
    /// awards it automatically at max size).</item>
    /// <item>The same, without the mark. Required because <c>MarkRules.IsMarkAllowedJumbo</c> also demands
    /// <c>EvolutionHistory.HasVisitedGen9</c> and a mark-capable encounter (<c>IsEncounterMarkLost</c> excludes
    /// e.g. Nincada -> Shedinja); without this fallback an entity that simply cannot hold the mark would get no
    /// size change at all.</item>
    /// <item>Height and Weight alone, leaving Scale untouched. Covers encounters whose Scale is pinned to one
    /// exact value by <c>ScaleType.VALUE</c> (every 7-Star Tera Raid, among others) -- Height/Weight only have
    /// to match Scale once the entity is HOME-tracked (<c>MiscScaleVerifier.IsHeightScaleMatchRequired</c> is
    /// <c>pk is IHomeTrack {{ HasTracker: true }}</c>), so on a fresh catch they are free to move independently
    /// even when Scale itself cannot.</item>
    /// <item>The largest/smallest legal value short of the extreme, for encounters that constrain scale to a
    /// range (<c>EncounterOutbreak9</c>'s ScaleMin/ScaleMax, <c>EncounterFixed9</c>'s MinScaleStrongTera floor).</item>
    /// </list>
    /// A fifth tier, the seed search, is <b>not</b> tried here for every entity -- see
    /// <see cref="TrySetSizeExtreme"/> for why, and use the per-entity repair menu entries when an individual
    /// seed-correlated raid catch needs it.
    /// </remarks>
    /// <param name="scaleSearchLimit">
    /// How many values short of the extreme to try when the extreme itself cannot be applied legally. Each
    /// attempt costs a full legality analysis, so this is deliberately bounded rather than scanning all 255.
    /// </param>
    public static BulkEditResult SetSizeExtremeForAll(IEnumerable<PKM> mons, SizeGoal goal, int scaleSearchLimit = 64)
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

            if (TrySetSizeExtremeFieldsOnly(pk, goal, scaleSearchLimit)
                || TrySeedSearchSize(pk, goal, BulkSeedSearchAttempts, BulkSeedSearchBudget))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedUnsupported);
    }

    /// <summary>
    /// Pushes a single entity's height/weight/scale to the legal extreme, trying every tier including the
    /// seed search with a generous (on-demand) budget. Used by the per-entity repair menu.
    /// </summary>
    /// <remarks>
    /// Not folded into the bulk path at this budget on purpose: the seed search runs a full
    /// <see cref="LegalityAnalysis"/> per candidate, so a generous per-entity budget here (thousands of
    /// attempts, several seconds) would multiply out to minutes across a whole box. The bulk path uses a much
    /// smaller budget instead -- see <see cref="BulkSeedSearchAttempts"/>.
    /// </remarks>
    public static bool TrySetSizeExtreme(PKM pk, SizeGoal goal, int scaleSearchLimit = 64, int seedSearchAttempts = OnDemandSeedSearchAttempts, TimeSpan? seedSearchBudget = null)
    {
        if (pk.Species == 0 || pk is not (IScaledSize or IScaledSize3))
            return false;
        return TrySetSizeExtremeFieldsOnly(pk, goal, scaleSearchLimit)
            || TrySeedSearchSize(pk, goal, seedSearchAttempts, seedSearchBudget ?? OnDemandSeedSearchBudget);
    }

    /// <summary>
    /// The first three (non-seed-search) tiers of <see cref="TrySetSizeExtreme"/> -- direct field edits only,
    /// cheap enough to run unconditionally before ever considering a seed search.
    /// </summary>
    private static bool TrySetSizeExtremeFieldsOnly(PKM pk, SizeGoal goal, int scaleSearchLimit)
    {
        var target = goal == SizeGoal.Maximize ? byte.MaxValue : byte.MinValue;
        if (goal == SizeGoal.Maximize && TrySetSize(pk, target, withJumbo: true))
            return true;
        if (TrySetSize(pk, target, withJumbo: false))
            return true;
        if (TrySetHeightWeightOnly(pk, target))
            return true;
        return TryWalkToExtremeLegal(pk, goal, scaleSearchLimit);
    }

    private static bool TrySetSize(PKM pk, byte value, bool withJumbo) =>
        TryApplyGuarded(pk, p => ApplySize(p, value, withJumbo));

    private static void ApplySize(PKM pk, byte value, bool withJumbo)
    {
        // SV's Scale is the authoritative size byte; HeightScalar/WeightScalar only matter once HOME-tracked,
        // at which point they must match Scale exactly -- so always set all three that apply.
        if (pk is IScaledSize3 s3)
            s3.Scale = value;
        if (pk is IScaledSize s2)
        {
            s2.HeightScalar = value;
            s2.WeightScalar = value;
        }
        // Only meaningful at maximum size; RibbonVerifierMark9 checks the mark one-directionally, so
        // scale-without-mark is never flagged despite being a state the game cannot produce.
        if (withJumbo && value == byte.MaxValue && pk is IRibbonSetMark9 { RibbonMarkJumbo: false } mark9)
            mark9.RibbonMarkJumbo = true;
        ResyncSizeValue(pk);
    }

    /// <summary>
    /// Sets Height and Weight to <paramref name="value"/> while leaving Scale exactly as it already is.
    /// </summary>
    /// <remarks>
    /// The tier that actually answers "the height/weight CAN exceed Scale" -- for an entity whose Scale is
    /// pinned to one value by <c>ScaleType.VALUE</c> (every 7-Star Tera Raid among others), the strict guard on
    /// <see cref="ApplySize"/> reverts every attempt because Scale itself cannot reach the extreme, even though
    /// Height and Weight are two entirely separate bytes that never have to equal it until the entity is
    /// HOME-tracked. Trying this narrower edit after the wider one fails recovers exactly that case.
    /// </remarks>
    private static bool TrySetHeightWeightOnly(PKM pk, byte value) =>
        pk is IScaledSize && TryApplyGuarded(pk, p =>
        {
            var s2 = (IScaledSize)p;
            s2.HeightScalar = value;
            s2.WeightScalar = value;
            ResyncSizeValue(p);
        });

    private static void ResyncSizeValue(PKM pk)
    {
        // Resync the derived displayed height/weight (meters/kg) where the format tracks it separately;
        // stale Absolute values aren't a legality problem here, but leaving them stale would be a visible bug.
        if (pk is IScaledSizeValue sv)
        {
            sv.ResetHeight();
            sv.ResetWeight();
        }
    }

    /// <summary>
    /// Walks from the extreme back toward the entity's current value looking for the most extreme size this
    /// entity can legally hold, for encounters that constrain scale to a range rather than pinning or freeing it.
    /// Never moves past the entity's current value in the opposite direction of <paramref name="goal"/>.
    /// </summary>
    private static bool TryWalkToExtremeLegal(PKM pk, SizeGoal goal, int limit)
    {
        var current = pk switch
        {
            IScaledSize3 s3 => s3.Scale,
            IScaledSize s2 => s2.HeightScalar,
            _ => goal == SizeGoal.Maximize ? byte.MaxValue : byte.MinValue,
        };

        for (int i = 1; i <= limit; i++)
        {
            int candidate = goal == SizeGoal.Maximize ? byte.MaxValue - i : byte.MinValue + i;
            if (goal == SizeGoal.Maximize ? candidate <= current : candidate >= current)
                break; // anything further would move the wrong direction relative to what it already has
            if (TrySetSize(pk, (byte)candidate, withJumbo: false))
                return true;
        }
        return false;
    }

    // The seed search runs a full LegalityAnalysis per candidate, so these budgets are calibrated the same way
    // the de-clone IV pass is: small enough that a whole-box bulk run stays reasonable, generous enough that a
    // single on-demand click (the per-entity repair menu) actually finds a good result.
    private const int BulkSeedSearchAttempts = 300;
    private static readonly TimeSpan BulkSeedSearchBudget = TimeSpan.FromSeconds(1.5);
    private const int OnDemandSeedSearchAttempts = 8000;
    private static readonly TimeSpan OnDemandSeedSearchBudget = TimeSpan.FromSeconds(4);

    /// <summary>
    /// For entities whose height/weight are part of a seed-correlated encounter (every Tera Crystal raid type,
    /// among others) and therefore cannot be set by direct field edit at all, regenerates many candidate seeds
    /// via the matched encounter's own <see cref="IGenerateSeed32"/> and keeps the best-scoring legal one.
    /// </summary>
    /// <remarks>
    /// This is the tier that makes "maximize/minimize size" actually work on a Tera raid catch: for these
    /// encounters, <c>Encounter9RNG.GenerateData</c> derives PID, Encryption Constant, IVs (where not fixed),
    /// Ability, Gender, Nature (where not fixed) and Height/Weight/Scale all from the same 32-bit seed, so there
    /// is no direct field edit that can move Height/Weight independently -- every attempt at
    /// <see cref="TrySetHeightWeightOnly"/> fails with <c>EncInvalid</c> because the stored bytes no longer
    /// reproduce from any seed. The only way to influence the roll is to land on a different one, exactly like
    /// <see cref="RegeneratePIDTrackerAndECForAll"/> does for identity and <c>IVOptimizer</c> does for IVs.
    /// <para/>
    /// <b>Identity-preserving by construction, not just by legality.</b> A seed the encounter's own
    /// <see cref="IGenerateSeed32.GenerateSeed32"/> accepts can also roll a different Ability, Nature, Gender or
    /// non-fixed IV alongside a better Height/Weight -- all of those would still come back fully legal, since
    /// they are all valid outcomes of the same encounter. But that is not what "maximize size" means: a
    /// candidate is only accepted if every one of those fields matches what the entity already had, so this can
    /// change size and nothing else about who the Pokemon is. IVs are compared in full (not just the total)
    /// because a raid with fewer than 6 guaranteed-flawless slots can roll the free ones anywhere 0-31, and
    /// those are exactly the kind of quiet identity drift this guard exists to catch.
    /// </remarks>
    private static bool TrySeedSearchSize(PKM pk, SizeGoal goal, int attempts, TimeSpan budget)
    {
        if (pk is not IScaledSize)
            return false;

        LegalityAnalysis before;
        try { before = new LegalityAnalysis(pk); }
        catch (Exception) { return false; }
        if (!before.Valid || before.Info.EncounterMatch is not IGenerateSeed32 gen)
            return false;

        var snapshot = pk.Data.ToArray();
        var origAbility = pk.Ability;
        var origNature = pk.Nature;
        var origGender = pk.Gender;
        var origShiny = pk.IsShiny;
        var origShinyXor = pk.ShinyXor;
        Span<int> origIVs = stackalloc int[6];
        pk.GetIVs(origIVs);

        byte[]? bestBytes = null;
        var bestScore = goal == SizeGoal.Maximize ? int.MinValue : int.MaxValue;
        var sw = Stopwatch.StartNew();
        Span<int> ivs = stackalloc int[6];

        for (int i = 0; i < attempts && sw.Elapsed < budget; i++)
        {
            // Random.Shared, not a per-entity Random(seed) -- seeding from anything derived off the entity's own
            // starting bytes (its EC, its PID) means two entities that start identical, which is exactly the
            // case this method is often run on, would explore the *same* candidate sequence in lockstep.
            var seed = (uint)Random.Shared.NextInt64(uint.MinValue, uint.MaxValue);
            try
            {
                if (!gen.GenerateSeed32(pk, seed))
                {
                    snapshot.CopyTo(pk.Data);
                    continue;
                }
                pk.RefreshChecksum();

                pk.GetIVs(ivs);
                var sameIdentity = pk.Ability == origAbility && pk.Nature == origNature && pk.Gender == origGender
                    && pk.IsShiny == origShiny && (!origShiny || pk.ShinyXor == origShinyXor)
                    && ivs.SequenceEqual(origIVs);
                if (!sameIdentity || !new LegalityAnalysis(pk).Valid)
                {
                    snapshot.CopyTo(pk.Data);
                    continue;
                }

                var s2 = (IScaledSize)pk;
                var score = s2.HeightScalar + s2.WeightScalar;
                var better = goal == SizeGoal.Maximize ? score > bestScore : score < bestScore;
                if (better)
                {
                    bestScore = score;
                    bestBytes = pk.Data.ToArray();
                }
            }
            catch (Exception)
            {
                // One bad seed (an encounter's GenerateSeed32 throwing on an edge case) should not end the
                // search; fall through and restore like any other rejected candidate.
            }
            snapshot.CopyTo(pk.Data);
        }

        if (bestBytes is null)
        {
            pk.RefreshChecksum();
            return false;
        }

        bestBytes.CopyTo(pk.Data);
        pk.RefreshChecksum();
        return true;
    }

    /// <summary>
    /// Sets <see cref="IScaledSize.HeightScalar"/> and <see cref="IScaledSize.WeightScalar"/> equal to
    /// <see cref="IScaledSize3.Scale"/> on Gen9 entities, matching what Pokémon HOME's own importer does on
    /// arrival. Reverts any entity that becomes illegal as a result.
    /// </summary>
    /// <remarks>
    /// SV rolls Scale and the Height/Weight scalars independently, so a mismatch is unflagged and harmless
    /// while the entity has never been to HOME. But <c>MiscScaleVerifier.IsHeightScaleMatchRequired</c> is
    /// <c>pk is IHomeTrack { HasTracker: true }</c>, and HOME copies Scale over both scalars on import -- so a
    /// mismatched entity that visits HOME comes back with the rule active and reports an outright
    /// <c>StatIncorrectScaleValue</c> error. Aligning up front removes that latent trap.
    /// <para/>
    /// Entities that <b>already</b> have a Tracker are deliberately skipped, not aligned: Height/Weight/Scale
    /// are on HOME's documented immutable list, so editing them on an entity HOME already has a record of
    /// invalidates that record. For those, HOME's stored values are authoritative and there is nothing to fix
    /// locally.
    /// </remarks>
    public static BulkEditResult AlignSizeToScaleForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            // Not Gen9-shaped, already aligned, or HOME-registered (where changing size breaks the record).
            if (pk is not (IScaledSize3 and IScaledSize) || pk is IHomeTrack { Tracker: not 0 })
            {
                skippedNotApplicable++;
                continue;
            }

            var s3 = (IScaledSize3)pk;
            var s2 = (IScaledSize)pk;
            var needsAlign = s2.HeightScalar != s3.Scale || s2.WeightScalar != s3.Scale;
            var needsMark = pk is IRibbonSetMark9 m
                            && ((s3.Scale == byte.MaxValue && !m.RibbonMarkJumbo)
                                || (s3.Scale == byte.MinValue && !m.RibbonMarkMini));
            if (!needsAlign && !needsMark)
            {
                skippedNotApplicable++;
                continue;
            }

            // Two independent guarded steps. A Mystery Gift pins HeightScalar/WeightScalar to the card's own
            // values while separately pinning Scale, so aligning is illegal for those -- but the size MARK may
            // still be fixable. Keeping the steps separate means one failing never blocks the other.
            var aligned = TryApplyGuarded(pk, Align);
            var marked = TryApplyGuarded(pk, FixSizeMark);
            if (aligned || marked)
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);

        static void Align(PKM pk)
        {
            var scale = ((IScaledSize3)pk).Scale;
            var size = (IScaledSize)pk;
            size.HeightScalar = scale;
            size.WeightScalar = scale;
        }

        // Gen9 awards Jumbo/Mini automatically at the size extremes, and RibbonVerifierMark9 only checks the
        // mark-without-scale direction -- so scale-without-mark is never flagged despite being unreachable
        // in-game.
        static void FixSizeMark(PKM pk)
        {
            if (pk is not IRibbonSetMark9 marks || pk is not IScaledSize3 s3)
                return;
            if (s3.Scale == byte.MaxValue && !marks.RibbonMarkJumbo)
                marks.RibbonMarkJumbo = true;
            else if (s3.Scale == byte.MinValue && !marks.RibbonMarkMini)
                marks.RibbonMarkMini = true;
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

    private static HashSet<uint>? BuildTakenPidSet(SaveFile? sav)
    {
        if (sav is null)
            return null;
        var slots = new List<SlotCache>();
        SlotInfoLoader.AddFromSaveFile(sav, slots);
        var taken = new HashSet<uint>(slots.Count);
        foreach (var slot in slots)
        {
            if (slot.Entity.Species != 0)
                taken.Add(slot.Entity.PID);
        }
        return taken;
    }

    /// <summary>
    /// Clears an existing HOME Tracker to zero so HOME issues a fresh record on the next upload. Touches
    /// nothing else -- PID, Encryption Constant, IVs and every other field are left exactly as they are.
    /// </summary>
    /// <remarks>
    /// A Tracker is a GUID issued by HOME's own servers, so it can only ever be <b>cleared</b>, never invented.
    /// <see cref="TransferVerifier"/> states this outright: "Transfer a 0-Tracker pk to HOME to get assigned a
    /// valid Tracker via the game it originated from. Don't make one up." A fabricated value claims an ID HOME
    /// never issued, which HOME can check against its own records; zero simply means "never uploaded".
    /// <para/>
    /// Self-limiting: for entities where a Tracker is genuinely required -- GO transfers, HOME gifts, and
    /// anything that crossed generations, per <see cref="HomeTrackerUtil.IsRequired"/> -- clearing it produces
    /// <c>TransferTrackerMissing</c> and the guard reverts, leaving those untouched.
    /// <para/>
    /// This does <b>not</b> guarantee HOME will accept the entity. HOME still holds a record of the original
    /// upload and may recognise it by other means; and if the original copy is still in HOME, a successful
    /// re-upload produces a second copy there rather than replacing the first. It removes the local
    /// tracker-mismatch obstacle, nothing more.
    /// </remarks>
    public static BulkEditResult ClearHomeTrackerForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNoTracker = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            if (pk is not IHomeTrack { Tracker: not 0 })
            {
                skippedNoTracker++;
                continue;
            }

            if (TryApplyGuarded(pk, static p => ((IHomeTrack)p).Tracker = 0))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNoTracker);
    }

    /// <summary>
    /// Clears the "this looks generated rather than played" warnings that <see cref="LegalityAnalysis"/> raises
    /// at <see cref="Severity.Fishy"/>, which never turn the verdict red and so are easy to miss entirely.
    /// </summary>
    /// <remarks>
    /// Each warning has a precise trigger, so each gets a targeted, individually guarded edit rather than a
    /// blanket rewrite:
    /// <list type="bullet">
    /// <item><c>Effort2Remaining</c> -- the EV total is exactly 508 (<see cref="EffortValues.MaxEffective"/>),
    /// the "two 252s" shape a tool produces. Spend the remaining 2 to reach 510.</item>
    /// <item><c>EffortEXPIncreased</c> -- the entity has levelled beyond its encounter level with zero EVs,
    /// which normal play cannot produce. Give it a small amount.</item>
    /// <item><c>LevelEXPThreshold</c> -- EXP sits exactly on a level boundary. Nudge it just above, staying
    /// inside the same level bracket so the level itself never changes.</item>
    /// <item><c>NickMatchLanguageFlag</c> -- the nickname flag is set while the nickname equals the species
    /// name. Clear the flag via <see cref="CommonEdits.SetDefaultNickname"/>.</item>
    /// </list>
    /// Every edit is kept only if the entity stays legal <b>and</b> the specific warning it targeted is
    /// actually gone -- the ordinary guard proves legality, which is not the same thing.
    /// <para/>
    /// EVs, EXP and nickname are <b>not</b> on HOME's documented immutable list, so this is safe to apply to a
    /// HOME-registered entity. See the caller for how that exemption is applied.
    /// </remarks>
    public static BulkEditResult FixFishyWarningsForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            var codes = GetFindingCodes(pk);
            var targeted = false;
            var fixedAny = false;

            if (codes.Contains(LegalityCheckResultCode.Effort2Remaining))
            {
                targeted = true;
                fixedAny |= TryClearFinding(pk, LegalityCheckResultCode.Effort2Remaining, SpendRemainingEVs);
            }
            if (codes.Contains(LegalityCheckResultCode.EffortEXPIncreased))
            {
                targeted = true;
                fixedAny |= TryClearFinding(pk, LegalityCheckResultCode.EffortEXPIncreased, GiveStarterEVs);
            }
            if (codes.Contains(LegalityCheckResultCode.LevelEXPThreshold))
            {
                targeted = true;
                fixedAny |= TryClearFinding(pk, LegalityCheckResultCode.LevelEXPThreshold, NudgeExperience);
            }
            if (codes.Contains(LegalityCheckResultCode.NickMatchLanguageFlag))
            {
                targeted = true;
                fixedAny |= TryClearFinding(pk, LegalityCheckResultCode.NickMatchLanguageFlag, static p => p.SetDefaultNickname());
            }

            if (!targeted)
                skippedNotApplicable++;
            else if (fixedAny)
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static HashSet<LegalityCheckResultCode> GetFindingCodes(PKM pk)
    {
        var set = new HashSet<LegalityCheckResultCode>();
        try
        {
            foreach (var chk in new LegalityAnalysis(pk).Results)
                set.Add(chk.Result);
        }
        catch (Exception)
        {
            // Corrupted data; leave the set empty so the entity is reported as "nothing applicable".
        }
        return set;
    }

    /// <summary>
    /// Applies <paramref name="mutate"/> and keeps it only if the entity is still legal AND
    /// <paramref name="code"/> is no longer reported. Reverts otherwise.
    /// </summary>
    private static bool TryClearFinding(PKM pk, LegalityCheckResultCode code, Action<PKM> mutate)
    {
        Span<byte> backup = stackalloc byte[pk.Data.Length];
        pk.Data.CopyTo(backup);
        try
        {
            mutate(pk);
            pk.RefreshChecksum();
            if (new LegalityAnalysis(pk).Valid && !GetFindingCodes(pk).Contains(code))
                return true;
        }
        catch (Exception)
        {
            // Fall through and revert, same as an ordinary "this didn't work" result.
        }
        backup.CopyTo(pk.Data);
        pk.RefreshChecksum();
        return false;
    }

    private static void SpendRemainingEVs(PKM pk)
    {
        var evs = new int[6];
        pk.GetEVs(evs);
        var spare = EffortValues.Max510 - Sum(evs);
        for (int i = 0; i < evs.Length && spare > 0; i++)
        {
            var room = Math.Min(spare, EffortValues.Max252 - evs[i]);
            evs[i] += room;
            spare -= room;
        }
        pk.SetEVs(evs);
    }

    private static void GiveStarterEVs(PKM pk)
    {
        var evs = new int[6];
        pk.GetEVs(evs);
        evs[0] = Math.Min(EffortValues.Max252, evs[0] + 4); // a few HP EVs is the least invasive nonzero value
        pk.SetEVs(evs);
    }

    private static void NudgeExperience(PKM pk)
    {
        // Move off the exact level boundary without crossing into the next level.
        var growth = pk.PersonalInfo.EXPGrowth;
        var level = pk.CurrentLevel;
        if (level >= Experience.MaxLevel)
            return;
        var here = Experience.GetEXP(level, growth);
        var next = Experience.GetEXP((byte)(level + 1), growth);
        if (next > here + 1)
            pk.EXP = here + 1;
    }

    private static int Sum(ReadOnlySpan<int> values)
    {
        int total = 0;
        foreach (var v in values)
            total += v;
        return total;
    }

    /// <summary>
    /// Fills in a missing Original Trainer memory on entities the legality checker flags with
    /// <see cref="LegalityCheckResultCode.MemoryMissingOT"/>, by searching the memory values the entity's game
    /// can actually produce and keeping the first that clears the finding while leaving the entity legal.
    /// Also handles the opposite fault -- an entity carrying an OT memory its origin may never have, which the
    /// checker reports as "Should be index 0" -- by clearing the four fields. See <see cref="MustHaveNoOTMemory"/>.
    /// </summary>
    /// <remarks>
    /// Unlike Handling Trainer memory there is no single canonical OT memory to apply, so this searches rather
    /// than guessing: several values are typically acceptable for a given encounter (a Dynamax Adventure catch
    /// accepts memories 8, 9, 11, 12, 13 and 15 among others), and which ones depends on the encounter.
    /// Intensity comes from <see cref="MemoryContext.GetMinimumIntensity"/> and feeling from the table matching
    /// the origin context, so the applied memory is internally consistent.
    /// <para/>
    /// Applies to every memory-bearing context, not just Gen8: XY/ORAS (Gen6) and SM/USUM (Gen7) entities use
    /// <see cref="MemoryContext6"/>, later ones <see cref="MemoryContext8"/>. Note the severity differs -- the
    /// verifier reports a missing OT memory as Invalid for Gen6/7 origin but only Fishy for Gen8, because
    /// SW/SH trades legitimately leave it unset for a while.
    /// <para/>
    /// Entities that legitimately must have no OT memory are untouched: the verifier only raises
    /// <c>MemoryMissingOT</c> when <c>CanHaveMemoryForOT</c> is true, so Mystery Gift entities (which require
    /// memory 0) never enter the search.
    /// <para/>
    /// Memory fields are <b>not</b> on HOME's documented immutable list, so unlike almost every other bulk edit
    /// this one is safe to apply to a HOME-registered entity. See the caller for how that exemption is applied.
    /// </remarks>
    public static BulkEditResult FixOriginalTrainerMemoryForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            // MemoryVerifier keys the whole check on the ENCOUNTER's context, not the entity's format -- a
            // Gen6-origin mon sitting in a Gen7 save is judged by Gen6 memory rules. Gate on the same thing.
            if (pk is not IMemoryOT)
            {
                skippedNotApplicable++;
                continue;
            }

            // Three faults share this step, and one entity can hit more than one in sequence:
            //  - the memory is populated but wrong (a bad move/item argument, or forbidden outright),
            //  - the memory is absent when the encounter allows one.
            // Clearing runs first because index 0 is the universal safe baseline: it is exactly what the
            // checker demands when no memory is allowed, and where a memory IS allowed it downgrades the
            // finding to the missing-memory one that the search below then fills in properly.
            var touched = false;
            var failed = false;

            if (HasInvalidOTMemory(pk))
            {
                if (TryClearOTMemory(pk))
                    touched = true;
                else
                    failed = true;
            }

            if (IsMissingOTMemory(pk, out var origin))
            {
                if (TrySearchOTMemory(pk, origin))
                    touched = true;
                else
                    failed = true;
            }

            if (touched)
                modified++;
            else if (failed)
                skippedIllegal++;
            else
                skippedNotApplicable++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    /// <summary>
    /// Reports whether <paramref name="pk"/> is missing its OT memory, and if so which
    /// <see cref="EntityContext"/> the memory rules must be drawn from.
    /// </summary>
    /// <remarks>
    /// The context comes from the matched <b>encounter</b>, not from <see cref="PKM.Context"/>: a Gen6-origin
    /// entity transferred into a Gen7 save is still judged against Gen6 memory tables. This mirrors
    /// <c>MemoryVerifier.VerifyOTMemory</c>, which opens with <c>var context = enc.Context;</c> -- keying off
    /// the entity's own format instead would look up the wrong memory table and search a candidate list the
    /// verifier will reject every time.
    /// </remarks>
    private static bool IsMissingOTMemory(PKM pk, out EntityContext origin)
    {
        origin = pk.Context;
        try
        {
            var la = new LegalityAnalysis(pk);
            foreach (var chk in la.Results)
            {
                if (chk.Result != LegalityCheckResultCode.MemoryMissingOT)
                    continue;
                origin = la.Info.EncounterMatch.Context;
                return true;
            }
        }
        catch (Exception)
        {
            // Corrupted data; treat as "nothing to do" rather than letting one entity break the run.
        }
        return false;
    }

    private static bool IsMissingOTMemory(PKM pk) => IsMissingOTMemory(pk, out _);

    private static bool TrySearchOTMemory(PKM pk, EntityContext origin)
    {
        var context = Memories.GetContext(origin);
        for (byte memory = 1; memory < 100; memory++)
        {
            if (!context.CanObtainMemoryOT(pk.Version, memory))
                continue;

            var applied = memory;
            if (!TryApplyProgressive(pk, p => ApplyOTMemory(p, origin, context, applied)))
                continue;
            // The guard only proves legality; confirm the finding it was raised for is actually gone.
            if (!IsMissingOTMemory(pk))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reports whether the legality checker raised any Invalid finding against <paramref name="pk"/>'s
    /// <b>Original Trainer</b> memory -- forbidden outright, or populated with content the species could never
    /// produce.
    /// </summary>
    /// <remarks>
    /// Broader than checking for one specific code on purpose, because a single wrong memory surfaces under
    /// several different ones: "Should be index 0" when the origin permits no memory at all
    /// (<c>MemoryVerifier.CanHaveMemoryForOT</c> allows one only for Gen6, the Bank memory for Gen1/2/7, and
    /// Gen8 when the entity did not arrive via HOME or GO), "Species can't learn {move}" for a move-flavoured
    /// memory whose argument the species cannot learn, and further codes for bad items, locations and feelings.
    /// All of them have the same remedy, so all of them should trigger it.
    /// <para/>
    /// Two filters keep this precise. <see cref="CheckIdentifier.Memory"/> restricts it to memory findings, and
    /// <see cref="CheckResult.Argument"/> restricts it to the OT handler -- every memory code packs the handler
    /// (0 = OT, 1 = HT) as its first hint argument, so without that second test the identically-coded Handling
    /// Trainer findings would be mistaken for OT ones and repaired in the wrong place.
    /// </remarks>
    private static bool HasInvalidOTMemory(PKM pk)
    {
        try
        {
            var la = new LegalityAnalysis(pk);
            foreach (var chk in la.Results)
            {
                if (chk.Judgement != Severity.Invalid || chk.Identifier != CheckIdentifier.Memory)
                    continue;
                if (chk.Argument == 0)
                    return true;
            }
        }
        catch (Exception)
        {
            // Corrupted data; treat as "nothing to do" rather than letting one entity break the run.
        }
        return false;
    }

    /// <summary>
    /// Zeroes all four Original Trainer memory fields on <paramref name="pk"/>, keeping the change only if the
    /// entity ends up legal or at least no worse than it started.
    /// </summary>
    /// <remarks>
    /// Uses the shrink-only guard rather than the strict one on purpose. These four findings are very often a
    /// <i>cascade</i> from an unmatched encounter -- when no encounter template matches, the verifier falls back
    /// to a context whose rules forbid OT memories, so the memory is condemned for a reason that has nothing to
    /// do with the memory bytes. Demanding full legality would revert a change that genuinely removes four real
    /// findings; requiring only that the finding set shrink and stay a subset lets the cleanup land while still
    /// refusing anything that introduces a new problem.
    /// <para/>
    /// Consequence worth knowing: on such an entity this silences the memory noise but leaves the underlying
    /// encounter mismatch, which is the finding that actually has to be fixed (see
    /// <see cref="RehomeToOrdinaryEncounterForAll"/>). Memory fields are not on HOME's immutable list, so
    /// clearing them is safe for a HOME-registered entity.
    /// </remarks>
    private static bool TryClearOTMemory(PKM pk)
    {
        var before = GetInvalidCodes(pk);
        var snapshot = pk.Data.ToArray();
        ClearOTMemory(pk);
        pk.RefreshChecksum();

        // Accept only if the memory complaint is actually gone and nothing new was introduced. Full legality is
        // not required: these findings are often a cascade from an unmatched encounter, where the memory is
        // condemned for a reason that has nothing to do with the memory bytes, and demanding legality would
        // revert a change that genuinely removes real findings.
        if (!HasInvalidOTMemory(pk) && GetInvalidCodes(pk).IsSubsetOf(before))
            return true;

        snapshot.CopyTo(pk.Data);
        return false;
    }

    private static void ClearOTMemory(PKM pk)
    {
        if (pk is not IMemoryOT ot)
            return;
        ot.OriginalTrainerMemory = 0;
        ot.OriginalTrainerMemoryIntensity = 0;
        ot.OriginalTrainerMemoryVariable = 0;
        ot.OriginalTrainerMemoryFeeling = 0;
    }

    private static void ApplyOTMemory(PKM pk, EntityContext origin, MemoryContext context, byte memory)
    {
        if (pk is not IMemoryOT ot)
            return;
        ot.OriginalTrainerMemory = memory;
        ot.OriginalTrainerMemoryIntensity = context.GetMinimumIntensity(memory);
        // The two feeling tables differ per context; picking the wrong one yields a memory the verifier
        // rejects, which the guard would silently revert as "unfixable".
        ot.OriginalTrainerMemoryFeeling = origin is EntityContext.Gen6 or EntityContext.Gen7
            ? MemoryContext6.GetRandomFeeling6(memory)
            : MemoryContext8.GetRandomFeeling8(memory);
        // Memories that need a real argument (met location, captured species, ...) will fail the guard with a
        // zero variable and simply fall through to the next candidate, so no per-memory special-casing here.
        ot.OriginalTrainerMemoryVariable = 0;
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
    /// <param name="sav">
    /// Optional. When supplied, every PID already present in the save is treated as taken and a reroll that
    /// lands on one is retried. This matters far more than the raw 32-bit PID space suggests: forcing a Square
    /// shiny pins ShinyXor to 0, which collapses the candidate space to roughly 65,536 values, so regenerating
    /// a few dozen shiny entities makes a birthday collision genuinely likely -- the fix would then create
    /// brand-new duplicates while removing old ones.
    /// </param>
    /// <param name="mons">Entities to regenerate.</param>
    /// <param name="sav">Save supplying the set of PIDs already in use, so a reroll does not land on another clone.</param>
    /// <param name="optimizeIVs">
    /// Optional IV improvement pass, run after a successful reroll. Supplied as a callback because the optimizer
    /// lives in <c>PKHeX.Core.AutoMod</c>, which references this assembly rather than the other way around --
    /// calling it directly here would invert the dependency.
    /// <para/>
    /// Worth running for a reason that is easy to miss: on seed-correlated encounters the PID and all six IVs
    /// derive from one value, so rerolling the PID silently rerolls the IVs too. Without this the de-clone can
    /// leave a Pokemon on whatever spread the first legal seed happened to give -- an 8-IV base is entirely
    /// possible -- and Hyper Training hides that in the displayed stats without fixing the underlying values.
    /// </param>
    /// <param name="seedSearchAttempts">
    /// Attempt budget for <see cref="TrySeedSearchUniquePID"/>, the fallback used when a seed-correlated
    /// encounter (Tera/Mighty/Distribution raid) makes the naive reroll above impossible. Defaults to the cheap
    /// bulk budget, because the default caller is the whole-box "Regenerate PID/Tracker/EC" checkbox where
    /// hundreds of entities might pass through this in one run. The "Check for Clones" fix flow calls this with
    /// a much larger on-demand budget instead, since it only ever touches the small, explicit list of entities
    /// the clone scan actually flagged -- a handful of raid clones each getting several thousand attempts is a
    /// few seconds total, not the minutes it would cost to give every entity in a full box that budget.
    /// </param>
    /// <param name="seedSearchBudget">Wall-clock cap per entity for the same search. Defaults to the cheap bulk budget.</param>
    public static BulkEditResult RegeneratePIDTrackerAndECForAll(IEnumerable<PKM> mons, SaveFile? sav = null, Func<PKM, bool>? optimizeIVs = null,
        int seedSearchAttempts = BulkSeedSearchAttempts, TimeSpan? seedSearchBudget = null)
    {
        var budget = seedSearchBudget ?? BulkSeedSearchBudget;
        var taken = BuildTakenPidSet(sav);
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNothingToDo = 0, alreadyIllegal = 0;
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

            // Never de-clone a distribution Pokemon. This rerolls PID and Encryption Constant and zeroes the
            // HOME tracker, all of which the card pinned and none of which can be recovered. The strict guard
            // is not sufficient cover: a gift whose card leaves the PID random stays perfectly legal after a
            // reroll, so the guard commits it and the event identity is gone anyway.
            // Consequence, accepted deliberately: cloned event Pokemon cannot be de-duplicated by this step and
            // will keep appearing in the clone report with nothing able to resolve them.
            if (IsEventDistribution(pk))
            {
                skippedNothingToDo++;
                continue;
            }

            // Never de-clone an entity that already carries a real, nonzero HOME Tracker. This used to clear
            // the tracker to 0 and proceed, same as any other entity -- correct for an entity that has never
            // actually been uploaded, wrong for one that demonstrably has. A nonzero Tracker is a GUID HOME
            // itself issued; HOME's own database has a record keyed to this exact PID right now. Regenerating
            // the PID (even with the tracker cleared afterward) does not undo that -- it just makes the local
            // copy quietly stop matching the real record, while the other copies sharing this same tracker are
            // provably NOT independent legitimate catches, they are local duplicates of the one entity that
            // record actually describes. At most one copy in a tracker-sharing group can be "the real one"; the
            // right fix for the rest is deleting them, which only the user can decide, not fabricating each one
            // a new identity HOME never issued. Same principle as the Cherish Ball and Mystery Gift exclusions
            // above, just keyed on a different field.
            if (pk is IHomeTrack { Tracker: not 0 })
            {
                skippedNothingToDo++;
                continue;
            }

            // Distinguish "our edit broke it" from "it was already broken". TryApplyGuarded can never keep an
            // edit on an entity that is illegal to begin with, so lumping those in with SkippedIllegal reports
            // a cause that is the exact opposite of the truth and sends the user hunting for the wrong problem.
            bool legalBefore;
            try
            {
                legalBefore = new LegalityAnalysis(pk).Valid;
            }
            catch (Exception)
            {
                legalBefore = false;
            }
            if (!legalBefore)
            {
                alreadyIllegal++;
                continue;
            }

            taken?.Remove(pk.PID); // its own current value must not block it
            if (TryRegenerateUnique(pk, taken, optimizeIVs, seedSearchAttempts, budget))
            {
                taken?.Add(pk.PID);
                modified++;
            }
            else
            {
                taken?.Add(pk.PID);
                skippedIllegal++;
            }
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNothingToDo, alreadyIllegal);

        // Reroll until the new PID is not already in use elsewhere. Keeps the last attempt even if every try
        // collided -- an unlikely-but-colliding identity is still strictly better than reverting to the
        // identical one we were asked to break apart.
        static bool TryRegenerateUnique(PKM pk, HashSet<uint>? taken, Func<PKM, bool>? optimizeIVs, int seedSearchAttempts, TimeSpan seedSearchBudget)
        {
            const int attempts = 8;
            for (int i = 0; i < attempts; i++)
            {
                if (!TryApplyGuarded(pk, RegenerateIdentity))
                    break; // the naive reroll can't make this entity legal at all -- try the seed search below
                if (taken is not null && taken.Contains(pk.PID))
                    continue;
                OptimizeWithoutCollision(pk, taken, optimizeIVs);
                return true;
            }

            // The naive reroll either never produced a legal result, or every legal result it did produce still
            // collided. Both are the expected failure mode for a seed-correlated encounter (Tera/Mighty/
            // Distribution raids): PID, Encryption Constant, IVs and Height/Weight all derive from one seed
            // together, so directly overwriting the PID can never reproduce from any seed and the strict guard
            // reverts every single attempt -- which is exactly the symptom the clone report describes as
            // "could not be auto-fixed". Search for a different seed instead, via the matched encounter's own
            // IGenerateSeed32, the same mechanism TrySeedSearchSize uses for height/weight.
            if (TrySeedSearchUniquePID(pk, taken, seedSearchAttempts, seedSearchBudget))
            {
                OptimizeWithoutCollision(pk, taken, optimizeIVs);
                return true;
            }

            // Gen8 overworld-correlated statics/slots (e.g. the Crown Tundra Galarian birds) are a second,
            // distinct seed-correlation scheme: IOverworldCorrelation8, not IGenerateSeed32, and PID/EC/IVs/
            // size all come from one Xoroshiro128Plus seed via Overworld8RNG rather than Encounter9RNG. The
            // search above never matches these (no IGenerateSeed32), so they fell through to "could not be
            // auto-fixed" exactly like the Gen9 raids did before that search was added.
            if (TrySeedSearchOverworld8(pk, taken, seedSearchAttempts, seedSearchBudget))
            {
                OptimizeWithoutCollision(pk, taken, optimizeIVs);
                return true;
            }

            return false;
        }

        /// <summary>
        /// For entities whose identity is part of a seed-correlated encounter, regenerates candidate seeds via
        /// the matched encounter's <see cref="IGenerateSeed32"/> and keeps the first that lands on a PID outside
        /// <paramref name="taken"/> while staying legal.
        /// </summary>
        /// <remarks>
        /// Identity-preserving the same way <see cref="TrySeedSearchSize"/> is: a seed the encounter accepts can
        /// also roll a different Ability, Nature, Gender or non-fixed IV alongside a different PID, and all of
        /// those would still come back legal -- they are all valid outcomes of the same encounter. This is only
        /// supposed to break the PID/EC collision, not quietly change who the Pokemon is, so a candidate is only
        /// accepted if every one of those fields still matches what the entity already had.
        /// </remarks>
        /// <remarks>
        /// Shininess is the one trait held as a hard requirement -- every candidate must match it exactly (and
        /// ShinyXor too, when shiny, so Square never silently becomes Star). Ability, Nature, Gender and the
        /// individual IVs are scored instead of required, maximizing how many still match rather than demanding
        /// all of them: see the remarks on <see cref="TrySeedSearchUniquePID"/> for why requiring an exact match
        /// on those specifically would make the search fail by construction on an ordinary (non-Mighty/
        /// Distribution) Tera Raid, where they are rolled per-seed rather than pinned by the template.
        /// </remarks>
        static int ScoreIdentityMatch(PKM pk, int origAbility, Nature origNature, byte origGender, ReadOnlySpan<int> origIVs, Span<int> ivs)
        {
            pk.GetIVs(ivs);
            var score = 0;
            if (pk.Ability == origAbility) score++;
            if (pk.Nature == origNature) score++;
            if (pk.Gender == origGender) score++;
            for (int i = 0; i < 6; i++)
            {
                if (ivs[i] == origIVs[i])
                    score++;
            }
            return score;
        }

        /// <summary>
        /// For entities whose identity is part of a seed-correlated encounter, regenerates candidate seeds via
        /// the matched encounter's <see cref="IGenerateSeed32"/> and keeps the best-scoring legal candidate
        /// outside <paramref name="taken"/>.
        /// </summary>
        /// <remarks>
        /// "Best-scoring" rather than "first found that matches exactly" is the key difference from the first
        /// version of this search. The original required Ability, Nature, Gender and all six IVs to match the
        /// original entity precisely before accepting a candidate -- correct for a fully template-fixed raid
        /// (every 7-Star Mighty/Distribution raid pins all of those, so any accepted seed reproduces them
        /// automatically and the check costs nothing), but a near-guaranteed failure for an ordinary Tera Raid
        /// den, where those fields are rolled independently per seed rather than fixed by the template. Demanding
        /// five-plus independent random draws land on their exact original values, on top of finding a seed that
        /// also produces a different PID, is close to a statistical impossibility within any attempt budget --
        /// which is exactly why species like Hisuian Braviary/Lilligant/Avalugg kept showing up as "could not be
        /// auto-fixed" even after the seed search was added.
        /// <para/>
        /// Scoring instead of requiring resolves this without having to special-case encounter types: a
        /// template-fixed raid's candidates all score the maximum already, so behavior there is unchanged, while
        /// a free-rolling den now accepts whichever legal, non-colliding candidate preserves the most of the
        /// original's traits -- typically most of them, since the search still prefers higher-scoring candidates
        /// over lower ones, but no longer blocks entirely on an unreachable exact match.
        /// </remarks>
        static bool TrySeedSearchUniquePID(PKM pk, HashSet<uint>? taken, int attempts, TimeSpan budget)
        {
            if (pk is not IScaledSize)
                return false; // seed-correlated encounters needing this are Gen9+ raids, which all implement this

            LegalityAnalysis before;
            try { before = new LegalityAnalysis(pk); }
            catch (Exception) { return false; }
            if (!before.Valid || before.Info.EncounterMatch is not IGenerateSeed32 gen)
                return false;

            var snapshot = pk.Data.ToArray();
            var origAbility = pk.Ability;
            var origNature = pk.Nature;
            var origGender = pk.Gender;
            var origShiny = pk.IsShiny;
            var origShinyXor = pk.ShinyXor;
            Span<int> origIVs = stackalloc int[6];
            pk.GetIVs(origIVs);
            Span<int> ivs = stackalloc int[6];

            byte[]? bestBytes = null;
            var bestScore = -1;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < attempts && sw.Elapsed < budget; i++)
            {
                // Random.Shared, not a per-entity Random(seed). This is the de-clone search, run on entities
                // that by definition started byte-identical -- seeding from pk.EncryptionConstant (the SAME
                // value for every member of the clone group) made every clone explore the identical candidate
                // sequence: whichever one processed first claimed the only reachable legal/untaken candidate,
                // every other clone then found that same candidate pre-taken and exhausted an identical search
                // with nothing left, and reverted unchanged. That is precisely the observed symptom -- a whole
                // clone group still sharing one PID after a fix run. Random.Shared never repeats based on the
                // entity's own bytes, so clones now genuinely diverge instead of searching in lockstep.
                var seed = (uint)Random.Shared.NextInt64(uint.MinValue, uint.MaxValue);
                try
                {
                    if (!gen.GenerateSeed32(pk, seed))
                    {
                        snapshot.CopyTo(pk.Data);
                        continue;
                    }
                    pk.RefreshChecksum();

                    var shinyOk = pk.IsShiny == origShiny && (!origShiny || pk.ShinyXor == origShinyXor);
                    if (!shinyOk || (taken is not null && taken.Contains(pk.PID)) || !new LegalityAnalysis(pk).Valid)
                    {
                        snapshot.CopyTo(pk.Data);
                        continue;
                    }

                    var score = ScoreIdentityMatch(pk, origAbility, origNature, origGender, origIVs, ivs);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestBytes = pk.Data.ToArray();
                    }
                }
                catch (Exception)
                {
                    // One bad seed should not end the search; fall through and restore like any other reject.
                }
                snapshot.CopyTo(pk.Data);
            }

            if (bestBytes is null)
            {
                pk.RefreshChecksum();
                return false;
            }

            bestBytes.CopyTo(pk.Data);
            pk.RefreshChecksum();
            return true;
        }

        /// <summary>
        /// For Gen8 overworld-correlated encounters (<see cref="IOverworldCorrelation8"/>: Galarian static
        /// encounters and overworld wild slots), regenerates PID/EC/IVs/size via <see cref="Overworld8RNG"/>
        /// instead of the naive independent-field reroll, which can never reproduce the required correlation.
        /// </summary>
        /// <remarks>
        /// Ability, Nature and Gender are untouched here -- <see cref="Overworld8RNG.ApplyDetails"/> only ever
        /// overwrites EncryptionConstant/PID/IV32/HeightScalar/WeightScalar, the same four+ fields the game
        /// itself derives from the 32-bit overworld seed; those three fields are set separately by the encounter
        /// and are not part of the correlation, so identity preservation there is automatic rather than scored.
        /// Shininess (and ShinyXor, so Square never becomes Star) is still a hard requirement, same as the
        /// Encounter9RNG search above.
        /// </remarks>
        static bool TrySeedSearchOverworld8(PKM pk, HashSet<uint>? taken, int attempts, TimeSpan budget)
        {
            if (pk is not PK8 pk8)
                return false; // the only format IOverworldCorrelation8 encounters (EncounterStatic8/EncounterSlot8) produce

            LegalityAnalysis before;
            try { before = new LegalityAnalysis(pk); }
            catch (Exception) { return false; }
            if (!before.Valid || before.Info.EncounterMatch is not IOverworldCorrelation8 ow)
                return false;
            if (ow.GetRequirement(pk) != OverworldCorrelation8Requirement.MustHave)
                return false; // this specific match doesn't actually require the correlation (e.g. a gift variant)

            var flawless = (before.Info.EncounterMatch as IFlawlessIVCount)?.FlawlessIVCount ?? 0;
            var origShiny = pk.IsShiny;
            var origShinyXor = pk.ShinyXor;
            var shinyReq = !origShiny ? Shiny.Never : origShinyXor == 0 ? Shiny.AlwaysSquare : Shiny.AlwaysStar;

            // ApplyDetails already runs its own internal search (70,000 candidate seeds by default) purely to
            // satisfy the shiny/flawless-IV filter, far more cheaply than re-validating full legality per
            // candidate the way the Encounter9RNG search above has to. The outer loop here only exists to retry
            // on a taken-PID collision or an unexpected legality failure, so a handful of calls is enough.
            var snapshot = pk.Data.ToArray();
            byte[]? bestBytes = null;
            var sw = Stopwatch.StartNew();
            var outerTries = Math.Min(attempts, 20);
            for (int i = 0; i < outerTries && sw.Elapsed < budget; i++)
            {
                try
                {
                    Overworld8RNG.ApplyDetails(pk8, EncounterCriteria.Unrestricted, shinyReq, flawless);
                    pk.RefreshChecksum();

                    var shinyOk = pk.IsShiny == origShiny && (!origShiny || pk.ShinyXor == origShinyXor);
                    if (shinyOk && (taken is null || !taken.Contains(pk.PID)) && new LegalityAnalysis(pk).Valid)
                    {
                        bestBytes = pk.Data.ToArray();
                        break;
                    }
                }
                catch (Exception)
                {
                    // One bad seed should not end the search; fall through and restore like any other reject.
                }
                snapshot.CopyTo(pk.Data);
            }

            if (bestBytes is null)
            {
                pk.RefreshChecksum();
                return false;
            }

            bestBytes.CopyTo(pk.Data);
            pk.RefreshChecksum();
            return true;
        }

        // The optimizer may itself rebuild the entity to reach a better seed, which changes the PID again -- and
        // a PID it picks freely can land straight back on one of the clones this routine exists to separate.
        // Keep the improvement only if the identity is still unique and still legal; otherwise put the
        // de-cloned version back, because breaking the clone matters more than the IV spread.
        static void OptimizeWithoutCollision(PKM pk, HashSet<uint>? taken, Func<PKM, bool>? optimizeIVs)
        {
            if (optimizeIVs is null)
                return;

            var snapshot = pk.Data.ToArray();
            try
            {
                if (!optimizeIVs(pk))
                    return; // nothing changed
                pk.RefreshChecksum();
                if (taken is not null && taken.Contains(pk.PID))
                    throw new InvalidOperationException("optimizer reintroduced a PID collision");
                if (!new LegalityAnalysis(pk).Valid)
                    throw new InvalidOperationException("optimizer produced an illegal entity");
                return;
            }
            catch (Exception)
            {
                // Fall through to the revert below.
            }
            snapshot.CopyTo(pk.Data);
            pk.RefreshChecksum();
        }

        static void RegenerateIdentity(PKM pk)
        {
            // Clear, never fabricate -- see the remarks above.
            if (pk is IHomeTrack { Tracker: not 0 } home)
                home.Tracker = 0;
            if (pk.Format >= 3)
            {
                // Reroll PID keeping species/gender/version/nature/form and shininess identical --
                // these two existing primitives also keep EncryptionConstant in sync for Gen3-5 (EC == PID there).
                if (pk.IsShiny)
                    RerollShinyPID(pk);
                else
                    pk.SetPIDGender(pk.Gender);
            }
            if (pk.Format >= 6)
                pk.SetRandomEC(); // Gen6+ EC is independent of PID; the reroll above only synced it for Gen3-5 origin
        }
    }

    /// <summary>
    /// Rerolls a shiny entity's PID, aiming for Square (<see cref="PKM.ShinyXor"/> == 0) where the format
    /// differentiates it. Always changes the PID, which is the whole point when de-cloning.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="CommonEdits.SetShiny(Shiny)"/>: that early-returns without touching the PID
    /// when the entity already satisfies the requested type, so an already-Square clone would keep its colliding
    /// PID and never actually get de-cloned.
    /// <para/>
    /// <see cref="PKM.SetShiny"/> loops only until <see cref="PKM.IsShiny"/> (xor &lt; 16), so ~15/16 of rerolls
    /// land on Star -- meaning the previous implementation silently downgraded Square shinies to Star. The retry
    /// loop below is bounded; each attempt has a ~1/16 chance on a Gen6+ (unconstrained) PID, so it converges
    /// quickly, and worst case we simply keep the Star we already have.
    /// </remarks>
    private static void RerollShinyPID(PKM pk)
    {
        pk.SetShiny();
        if (!CanPreferSquare(pk, preferSquare: true))
            return;
        for (int i = 0; i < 256 && pk.ShinyXor != 0; i++)
            pk.SetShiny();
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
    /// <summary>
    /// Repairs <see cref="LegalityCheckResultCode.TransferNature"/> on Virtual Console transfers
    /// (Gen1/Gen2 -> Gen7), where the Nature is not stored on the entity but re-derived from its Experience.
    /// </summary>
    /// <remarks>
    /// A VC transfer has no Nature byte of its own in the original games, so Gen7 recomputes it as
    /// <c>EXP % 25</c> (<see cref="Experience.GetNatureVC"/>). Editing the Nature directly desynchronizes it
    /// from the Experience and <see cref="TransferVerifier"/> flags it as Invalid. Two cases exist, and only
    /// the Nature side is adjustable in either:
    /// <list type="bullet">
    /// <item><b>Met Level 100</b>: the entity cannot gain further EXP after transfer, so the Experience is
    /// pinned and the Nature must be exactly <c>EXP % 25</c>. One candidate, no search.</item>
    /// <item><b>Met Level 2 or below</b>: the level's EXP window is too narrow to produce all 25 natures, so
    /// only the subset in <see cref="Experience.IsValidNatureMetLevel2"/> is reachable for that growth rate.
    /// Candidates are tried in order from the entity's current Nature so the change is as small as possible.</item>
    /// </list>
    /// Nature <i>is</i> on HOME's documented immutable list, so unlike the memory and Fishy repairs this must
    /// respect the "skip HOME-registered" filter -- see the caller.
    /// </remarks>
    public static BulkEditResult FixTransferNatureForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            if (!GetFindingCodes(pk).Contains(LegalityCheckResultCode.TransferNature))
            {
                skippedNotApplicable++;
                continue;
            }

            if (TryFixTransferNature(pk))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static bool TryFixTransferNature(PKM pk)
    {
        foreach (var nature in GetTransferNatureCandidates(pk))
        {
            var applied = nature;
            if (!TryClearFinding(pk, LegalityCheckResultCode.TransferNature, p => p.Nature = applied))
                continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Yields the natures worth trying for <paramref name="pk"/>, best candidate first.
    /// </summary>
    private static IEnumerable<Nature> GetTransferNatureCandidates(PKM pk)
    {
        if (pk.MetLevel == 100)
        {
            // Experience is pinned at the level-100 value, so exactly one nature can ever match it.
            yield return Experience.GetNatureVC(pk.EXP);
            yield break;
        }

        // Met Level <= 2: walk outward from the current nature so the smallest possible change wins.
        var growth = pk.PersonalInfo.EXPGrowth;
        var current = (byte)pk.Nature;
        for (byte i = 0; i < 25; i++)
        {
            var candidate = (Nature)((current + i) % 25);
            if (Experience.IsValidNatureMetLevel2(growth, candidate))
                yield return candidate;
        }
    }
    /// <summary>
    /// Repairs <see cref="LegalityCheckResultCode.MemoryStatFriendshipOTBaseEvent_0"/> by restoring
    /// <see cref="PKM.OriginalTrainerFriendship"/> to the base friendship the encounter requires.
    /// </summary>
    /// <remarks>
    /// OT Friendship is frozen at the value assigned in the entity's original generation -- unlike current
    /// friendship it can never drift -- so <c>HistoryVerifier</c> raises this whenever a traded-away
    /// ("never OT") or Virtual Console entity carries anything else.
    /// <para/>
    /// No search is needed: the verifier already computed the one acceptable value and passes it as the check's
    /// hint argument (<see cref="CheckResult.Value"/>), for both the
    /// <c>GetBaseFriendship(enc)</c> branch and the VC1/2 evolution-chain branch. Reading it back is exact and
    /// stays correct if upstream changes how that value is derived, which re-deriving it here would not.
    /// <para/>
    /// This touches OT Friendship only, never <see cref="PKM.CurrentFriendship"/>, so no stat or evolution
    /// behaviour changes. It is not on HOME's documented immutable list, so it shares the OT-memory repair's
    /// HOME-registered exemption -- see the caller.
    /// </remarks>
    public static BulkEditResult FixOriginalTrainerFriendshipForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            if (!TryGetExpectedOTFriendship(pk, out var expect))
            {
                skippedNotApplicable++;
                continue;
            }

            if (TryClearFinding(pk, LegalityCheckResultCode.MemoryStatFriendshipOTBaseEvent_0, p => p.OriginalTrainerFriendship = expect))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    /// <summary>
    /// Reports whether the OT Friendship finding is present, and the base friendship the verifier expects.
    /// </summary>
    private static bool TryGetExpectedOTFriendship(PKM pk, out byte expect)
    {
        expect = 0;
        try
        {
            foreach (var chk in new LegalityAnalysis(pk).Results)
            {
                if (chk.Result != LegalityCheckResultCode.MemoryStatFriendshipOTBaseEvent_0)
                    continue;
                expect = (byte)chk.Value;
                return true;
            }
        }
        catch (Exception)
        {
            // Corrupted data; treat as "nothing to do" rather than letting one entity break the run.
        }
        return false;
    }
    /// <summary>
    /// Codes repaired by <see cref="FixTransferSideFieldsForAll"/>: the PID-adjacent fields on a Gen3/4/5
    /// entity transferred forward, which drift when a PID is edited after the fact.
    /// </summary>
    private static readonly LegalityCheckResultCode[] TransferSideFieldCodes =
    [
        LegalityCheckResultCode.TransferEncryptGen6Equals,
        LegalityCheckResultCode.TransferEncryptGen6BitFlip,
        LegalityCheckResultCode.PIDNatureMismatch,
        LegalityCheckResultCode.AbilityHiddenFail,
    ];

    /// <summary>
    /// Repairs the PID-derived side fields (Encryption Constant, Nature, Ability) on legacy entities
    /// transferred forward into Gen6+, without ever touching the PID itself.
    /// </summary>
    /// <remarks>
    /// On a Gen3/4/5 transfer the PID is the root value and three fields hang off it:
    /// <list type="bullet">
    /// <item><b>Encryption Constant</b> -- the transfer copies the original PID into the EC, so
    /// <c>PK5.GetTransferPID(EC, ID32)</c> must reproduce the PID exactly.</item>
    /// <item><b>Nature</b> -- <c>GenderVerifier.GetExpectedNature</c> requires <c>EC % 25</c>.</item>
    /// <item><b>Ability</b> -- Gen3/4/5 encounters resolve the ability from a PID bit, and pre-Gen5 encounters
    /// have no Hidden Ability at all, so a Hidden Ability here is always wrong.</item>
    /// </list>
    /// <b>This cannot fix a PID that is wrong for its encounter</b> (<c>PIDTypeMismatch</c>, "PID+ correlation
    /// does not match what was expected for the Encounter's type"). That demands the PID be a genuine output
    /// frame of the encounter's RNG method, which no side-field edit can produce -- those entities need full
    /// regeneration via the legalizer instead. Force-shinied legacy entities are almost always in that bucket.
    /// <para/>
    /// Because of that, this uses <see cref="TryApplyReducingInvalid"/> rather than the usual
    /// "must end up fully legal" guard: an entity carrying an unfixable PID would otherwise have its genuine
    /// side-field repairs reverted for failing a condition they were never able to satisfy. The weaker guard
    /// still cannot regress anything -- it keeps the edit only if it removes at least one Invalid finding and
    /// introduces no new one.
    /// </remarks>
    public static BulkEditResult FixTransferSideFieldsForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            // This rewrites Encryption Constant, Nature and the ability slot -- all HOME-immutable, and on a
            // distribution Pokemon all part of what the card fixed. It runs under the shrink-only guard, which
            // an already-illegal event entity can satisfy without the result being right, so the guard alone is
            // not enough protection here. A genuine gift never needs this anyway: its EC came from the card,
            // not from a Gen5-to-Gen6 transfer. See IsEventDistribution.
            if (IsEventDistribution(pk))
            {
                skippedNotApplicable++;
                continue;
            }

            var invalid = GetInvalidCodes(pk);
            if (!HasAny(invalid, TransferSideFieldCodes))
            {
                skippedNotApplicable++;
                continue;
            }

            if (TryApplyReducingInvalid(pk, ApplyTransferSideFields))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static void ApplyTransferSideFields(PKM pk)
    {
        // EC first: Nature is derived from whatever EC ends up being.
        if (TryGetTransferEC(pk, out var ec))
            pk.EncryptionConstant = ec;

        pk.Nature = (Nature)(pk.EncryptionConstant % 25);

        // Pre-Gen5 encounters have no Hidden Ability, and Gen3/4/5 resolve the ability slot from a PID bit.
        if (pk.AbilityNumber == 4)
            pk.RefreshAbility(GetPIDAbilityIndex(pk));
    }

    /// <summary>
    /// Finds the Encryption Constant that reproduces the entity's existing PID under the Gen5-to-Gen6 transfer
    /// rule, leaving the PID untouched.
    /// </summary>
    /// <remarks>
    /// <c>PK5.GetTransferPID</c> maps an EC to either itself or itself with the top bit flipped, so the PID has
    /// at most two possible pre-images -- just test both rather than re-deriving the branch condition.
    /// <para/>
    /// Neither can match when the PID falls in the band that Gen6's doubled shiny rate made unreachable
    /// (<c>(xor &amp; 0xFFF8) == 8</c>): such a PID could never have come through a real transfer, so there is no
    /// EC that legitimises it and the caller reports it unfixable rather than inventing one.
    /// </remarks>
    private static bool TryGetTransferEC(PKM pk, out uint ec)
    {
        ec = pk.PID;
        if (PK5.GetTransferPID(ec, pk.ID32, out _) == pk.PID)
            return true;
        ec = pk.PID ^ 0x80000000;
        if (PK5.GetTransferPID(ec, pk.ID32, out _) == pk.PID)
            return true;
        ec = 0;
        return false;
    }

    /// <summary>
    /// Ability slot (0 or 1) that the PID dictates for a legacy-origin entity.
    /// </summary>
    /// <remarks>
    /// <see cref="PKM.PIDAbility"/> cannot be used here: it returns -1 once <see cref="PKM.Format"/> exceeds 5,
    /// which is always true for the transferred entities this repairs. Gen5 reads the high half of the PID,
    /// Gen3/4 the low bit.
    /// </remarks>
    private static int GetPIDAbilityIndex(PKM pk) => pk.Generation == 5
        ? (int)((pk.PID >> 16) & 1)
        : (int)(pk.PID & 1);

    private static HashSet<LegalityCheckResultCode> GetInvalidCodes(PKM pk)
    {
        var set = new HashSet<LegalityCheckResultCode>();
        try
        {
            foreach (var chk in new LegalityAnalysis(pk).Results)
            {
                if (chk.Judgement == Severity.Invalid)
                    set.Add(chk.Result);
            }
        }
        catch (Exception)
        {
            // Corrupted data; leave the set empty so the entity is reported as "nothing applicable".
        }
        return set;
    }

    private static bool HasAny(HashSet<LegalityCheckResultCode> set, LegalityCheckResultCode[] codes)
    {
        foreach (var code in codes)
        {
            if (set.Contains(code))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Applies <paramref name="mutate"/> and keeps it only if the set of Invalid findings strictly shrinks:
    /// at least one is cleared and no new one appears. Reverts otherwise.
    /// </summary>
    /// <remarks>
    /// A weaker guard than <see cref="TryApplyGuarded"/>, for entities that carry a defect this editor cannot
    /// repair. Requiring full legality there would throw away correct partial fixes; requiring the result to be
    /// a proper subset of what was already wrong means the entity can only ever move toward legal.
    /// </remarks>
    private static bool TryApplyReducingInvalid(PKM pk, Action<PKM> mutate)
    {
        var before = GetInvalidCodes(pk);
        Span<byte> backup = stackalloc byte[pk.Data.Length];
        pk.Data.CopyTo(backup);
        try
        {
            mutate(pk);
            pk.RefreshChecksum();
            var after = GetInvalidCodes(pk);
            if (after.Count < before.Count && after.IsSubsetOf(before))
                return true;
        }
        catch (Exception)
        {
            // Fall through and revert, same as an ordinary "this didn't work" result.
        }
        backup.CopyTo(pk.Data);
        pk.RefreshChecksum();
        return false;
    }
    /// <summary>
    /// Converts Virtual Console (Gen1/2 to Gen7) shiny transfers from Star to Square, clearing the Fishy
    /// <see cref="LegalityCheckResultCode.EncStaticPIDShiny"/> warning.
    /// </summary>
    /// <remarks>
    /// Transporter assigns a random PID and then, if the Gen1/2 entity was shiny by its DVs, rewrites the upper
    /// half to force a <b>Square</b> shiny -- see <c>PK7.SetTransferPID</c>:
    /// <code>PID = (low ^ TID16 ^ 0u) &lt;&lt; 16 | low;</code>
    /// That forcing is skipped when the random PID happened to be shiny already (<c>when !IsShiny</c>), so a
    /// Star shiny VC transfer is only reachable by a 15-in-65536 coincidence. That is what the check flags, and
    /// why the fix is to make these Square rather than to remove their shininess.
    /// <para/>
    /// The Square result is strictly more canonical than what is there now, and it matches the preference for
    /// Square shinies elsewhere in this editor. Note VC transfers carry SID 0, which is why the original
    /// expression XORs against <c>0u</c>.
    /// <para/>
    /// Only the Fishy VC case is targeted. <c>EncStaticPIDShiny</c> is reused for genuine shiny-lock violations
    /// elsewhere (<c>PIDVerifier</c>, and two other sites in <c>TransferVerifier</c>) where it is raised as
    /// Invalid and rewriting the PID would be wrong -- hence the severity and <see cref="PKM.VC"/> gates.
    /// <para/>
    /// PID <b>is</b> on HOME's documented immutable list, so this honours the "skip HOME-registered" filter.
    /// </remarks>
    public static BulkEditResult MakeVirtualConsoleShinySquareForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            if (pk is not PK7 || !pk.VC || !HasFishyTransferStarShiny(pk))
            {
                skippedNotApplicable++;
                continue;
            }

            if (TryClearFinding(pk, LegalityCheckResultCode.EncStaticPIDShiny, MakeSquareShiny))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    /// <summary>
    /// True only for the Fishy Star-shiny transfer warning, not the Invalid shiny-lock uses of the same code.
    /// </summary>
    private static bool HasFishyTransferStarShiny(PKM pk)
    {
        try
        {
            foreach (var chk in new LegalityAnalysis(pk).Results)
            {
                if (chk.Result == LegalityCheckResultCode.EncStaticPIDShiny && chk.Judgement == Severity.Fishy)
                    return true;
            }
        }
        catch (Exception)
        {
            // Corrupted data; treat as "nothing to do" rather than letting one entity break the run.
        }
        return false;
    }

    /// <summary>
    /// Rewrites the upper half of the PID so the entity is a Square shiny, keeping the lower half intact.
    /// </summary>
    private static void MakeSquareShiny(PKM pk) =>
        pk.PID = ShinyUtil.GetShinyPID(pk.TID16, pk.SID16, pk.PID, 0);
    /// <summary>
    /// Reverts entities stored in a battle-only form (Mega, Primal, Pirouette, ...) back to the form they
    /// should have reverted to when stored, clearing <see cref="LegalityCheckResultCode.FormBattle"/>.
    /// </summary>
    /// <remarks>
    /// Mega Evolution and the other battle-only forms are transient: the game reverts them when the battle
    /// ends, so no such form can legitimately exist in a box. An entity saved in one is Invalid, which also
    /// blocks every guarded repair downstream -- including the clone de-duplicator, which refuses
    /// already-illegal entities because a legality-guarded edit can never succeed on them.
    /// <para/>
    /// The revert target is not guessed: <see cref="FormInfo.GetOutOfBattleForm"/> is the same table PKHeX
    /// itself uses, so species with a non-obvious mapping (Darmanitan, Minior, Mimikyu, Meowstic, Ogerpon) are
    /// handled correctly rather than being blanket-reset to form 0.
    /// <para/>
    /// Two escalating attempts: form alone, then form plus an ability refresh. A Mega form can carry an ability
    /// that only the Mega has (Mega Venusaur's Thick Fat), which stays Invalid against the base form's personal
    /// entry once the form is reverted -- so the second attempt re-reads the ability from the reverted form.
    /// <para/>
    /// Uses the shrink-only guard for the same reason as
    /// <see cref="FixTransferSideFieldsForAll"/>: these entities are Invalid before the edit, so a
    /// "must end up legal" guard would revert a correct fix on anything with a second unrelated problem.
    /// </remarks>
    public static BulkEditResult FixBattleOnlyFormsForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            if (!FormInfo.IsBattleOnlyForm(pk.Species, pk.Form, pk.Format))
            {
                skippedNotApplicable++;
                continue;
            }

            var target = FormInfo.GetOutOfBattleForm(pk.Species, pk.Form, pk.Format);
            if (target == pk.Form)
            {
                // Nothing to revert to (e.g. Floette's Eternal Flower maps to itself); leave it alone rather
                // than inventing a form the table does not sanction.
                skippedNotApplicable++;
                continue;
            }

            if (TryRevertBattleForm(pk, target))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static bool TryRevertBattleForm(PKM pk, byte target)
    {
        if (TryClearFindingReducing(pk, LegalityCheckResultCode.FormBattle, p => p.Form = target))
            return true;

        return TryClearFindingReducing(pk, LegalityCheckResultCode.FormBattle, p =>
        {
            p.Form = target;
            p.RefreshAbility(GetAbilitySlot(p));
        });
    }

    /// <summary>
    /// Ability slot index (0/1/2) from the <see cref="PKM.AbilityNumber"/> bitflag (1/2/4).
    /// </summary>
    private static int GetAbilitySlot(PKM pk) => pk.AbilityNumber switch
    {
        4 => 2,
        2 => 1,
        _ => 0,
    };

    /// <summary>
    /// Applies <paramref name="mutate"/> and keeps it only if <paramref name="code"/> is cleared AND the set of
    /// Invalid findings strictly shrinks. Reverts otherwise.
    /// </summary>
    /// <remarks>
    /// Combines <see cref="TryClearFinding"/>'s "the targeted finding must actually be gone" requirement with
    /// <see cref="TryApplyReducingInvalid"/>'s tolerance for entities that stay Invalid for unrelated reasons.
    /// </remarks>
    private static bool TryClearFindingReducing(PKM pk, LegalityCheckResultCode code, Action<PKM> mutate)
    {
        var before = GetInvalidCodes(pk);
        Span<byte> backup = stackalloc byte[pk.Data.Length];
        pk.Data.CopyTo(backup);
        try
        {
            mutate(pk);
            pk.RefreshChecksum();
            var after = GetInvalidCodes(pk);
            if (!after.Contains(code) && after.Count < before.Count && after.IsSubsetOf(before))
                return true;
        }
        catch (Exception)
        {
            // Fall through and revert, same as an ordinary "this didn't work" result.
        }
        backup.CopyTo(pk.Data);
        pk.RefreshChecksum();
        return false;
    }
    /// <summary>
    /// Strips the hallmarks of a forged event Pokemon: a <see cref="PKM.FatefulEncounter"/> flag with no
    /// matching Mystery Gift behind it, and the ribbons that were set alongside it.
    /// </summary>
    /// <remarks>
    /// A fake event entity typically carries the Fateful flag, an event-only ribbon (Classic, Wishing, ...) and
    /// event-only moves. The Fateful flag is the load-bearing one: while it is set, the legality checker looks
    /// for a Mystery Gift and reports <c>EncGiftNotFound</c> when the database has none. Clearing it lets the
    /// analysis fall back to matching an ordinary encounter, which usually resolves the gift error and often
    /// re-frames the moves and ribbons as ordinary (fixable) problems rather than event ones.
    /// <para/>
    /// Order matters: Fateful is cleared first and the entity re-analysed, so ribbons are then judged against
    /// the encounter it actually matches rather than the gift it was pretending to be.
    /// <para/>
    /// Ribbons are repaired with <see cref="RibbonApplicator.FixInvalidRibbons"/> -- PKHeX's own routine, which
    /// removes ribbons the encounter cannot justify and sets any it requires. Clearing ribbons by hand would
    /// miss the "required but absent" direction entirely.
    /// <para/>
    /// Both steps use the shrink-only guard: these entities have several unrelated problems at once, so
    /// demanding full legality would revert each correct fix for failing a condition the others still break.
    /// </remarks>
    public static BulkEditResult FixFakeEventDataForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            var invalid = GetInvalidCodes(pk);
            var targeted = false;
            var fixedAny = false;

            if (invalid.Contains(LegalityCheckResultCode.FatefulInvalid))
            {
                targeted = true;
                fixedAny |= TryClearFindingReducing(pk, LegalityCheckResultCode.FatefulInvalid,
                    static p => p.FatefulEncounter = false);
            }

            // Re-read: clearing Fateful can change which encounter is matched, and therefore which ribbons
            // are legal. Judging ribbons off the pre-clear analysis would fix them against the wrong template.
            if (GetInvalidCodes(pk).Contains(LegalityCheckResultCode.RibbonsInvalid_0))
            {
                targeted = true;
                fixedAny |= TryClearFindingReducing(pk, LegalityCheckResultCode.RibbonsInvalid_0, FixRibbons);
            }

            if (!targeted)
                skippedNotApplicable++;
            else if (fixedAny)
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static void FixRibbons(PKM pk)
    {
        var la = new LegalityAnalysis(pk);
        var args = new RibbonVerifierArguments(pk, la.EncounterMatch, la.Info.EvoChainsAllGens);
        RibbonApplicator.FixInvalidRibbons(args);
    }

    /// <summary>
    /// Applies <paramref name="mutate"/>, preferring a fully legal result but accepting a strict improvement.
    /// </summary>
    /// <remarks>
    /// <see cref="TryApplyGuarded"/> alone deadlocks when an entity has two problems that each need a
    /// different repair: the move fix reverts because the memory is still missing, and the memory fix reverts
    /// because the moves are still wrong, so neither ever lands and the entity stays broken.
    /// <para/>
    /// Trying the strict guard first keeps the existing behaviour wherever it already worked -- an edit that
    /// reaches full legality is committed exactly as before. Only when that fails does this fall back to
    /// <see cref="TryApplyReducingInvalid"/>, which still refuses anything that introduces a new Invalid
    /// finding, so the fallback can never make an entity worse than it already was.
    /// </remarks>
    private static bool TryApplyProgressive(PKM pk, Action<PKM> mutate) =>
        TryApplyGuarded(pk, mutate) || TryApplyReducingInvalid(pk, mutate);
    /// <summary>
    /// Repairs entities whose met data does not line up with any real encounter, by trying the met level (and
    /// only then the met location) of every encounter template their species and origin game actually offer,
    /// keeping the first that makes the entity fully legal.
    /// </summary>
    /// <remarks>
    /// The motivating case: a Glastrier stored with met level 80 at the Crown Shrine. Both Glastrier and Calyrex
    /// are static encounters at that same location, but Glastrier's is level 75 and Calyrex's is level 80
    /// (<c>Encounters8</c>), so an entity carrying Calyrex's met block under Glastrier's species matches nothing.
    /// Met level is an exact comparison on a static encounter -- off by five is as fatal as off by fifty -- and
    /// once no template matches, every later verifier judges the entity against a fallback it never found, which
    /// is why one wrong byte produces a page of unrelated-looking errors (memories, encounter type, PID).
    /// <para/>
    /// This is deliberately far narrower than <see cref="RehomeToOrdinaryEncounterForAll"/>, which addresses the
    /// same <see cref="LegalityCheckResultCode.EncInvalid"/> finding but clears Fateful, re-balls, and may wipe
    /// relearn moves and the moveset. That is the right hammer for a forged gift whose encounter never existed;
    /// it is gross overkill for an entity that is one field away from correct, and it would needlessly overwrite
    /// a HOME-immutable ball. Run this first and rehome only takes what this could not fix.
    /// <para/>
    /// Two passes, least destructive first, each over every candidate template:
    /// <list type="number">
    /// <item>Met level only. The location is already right in the Glastrier case, so nothing else is disturbed.</item>
    /// <item>Met level and met location together, for entities whose location is wrong too.</item>
    /// </list>
    /// Acceptance is the <b>strict</b> guard -- the entity must come out fully legal. Shrink-only would be wrong
    /// here: met data is HOME-immutable and the original value is unrecoverable, so "fewer findings than before"
    /// is not a good enough reason to overwrite it. Either the candidate is the encounter this entity really came
    /// from, in which case everything resolves at once, or it is a guess not worth committing.
    /// <para/>
    /// Candidates come from <see cref="EncounterTypeGroup.Slot"/> and <see cref="EncounterTypeGroup.Static"/>
    /// only, so like rehome this can never move an entity onto a Mystery Gift. Eggs are skipped (their met data
    /// follows hatch rules, not encounter templates), and the location pass skips Cherish Ball entities so a real
    /// event's distribution location is never rewritten.
    /// </remarks>
    public static BulkEditResult RepairMetDataForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            if (pk.WasEgg || !GetInvalidCodes(pk).Contains(LegalityCheckResultCode.EncInvalid))
            {
                skippedNotApplicable++;
                continue;
            }

            if (TryRepairMetData(pk))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static bool TryRepairMetData(PKM pk)
    {
        var candidates = GetMetCandidates(pk);
        if (candidates.Count == 0)
            return false;

        // Pass 1: met level only, keeping the existing location.
        foreach (var (level, _) in candidates)
        {
            if (level == pk.MetLevel)
                continue;
            var applied = level;
            if (TryApplyGuarded(pk, p => ApplyMetLevel(p, applied)))
                return true;
        }

        // Pass 2: met level and location together. Never for a Cherish Ball entity -- see IsEventDistribution.
        if (IsEventDistribution(pk))
            return false;

        foreach (var (level, location) in candidates)
        {
            if (level == pk.MetLevel && location == pk.MetLocation)
                continue;
            var (l, loc) = (level, location);
            if (TryApplyGuarded(pk, p => { ApplyMetLevel(p, l); p.MetLocation = loc; }))
                return true;
        }
        return false;
    }

    private static void ApplyMetLevel(PKM pk, byte level)
    {
        pk.MetLevel = level;
        // A met level above the current level is itself invalid, so raise the entity to meet it. Lowering is
        // never done -- an over-levelled Pokemon is legal, and dropping CurrentLevel would rewrite EXP for no
        // gain.
        if (pk.CurrentLevel < level)
            pk.CurrentLevel = level;
    }

    /// <summary>
    /// Collects the distinct (met level, met location) pairs of every wild or static encounter template that
    /// <paramref name="pk"/>'s species, form and origin game could have produced.
    /// </summary>
    /// <remarks>
    /// Mirrors how <see cref="EncounterSuggestion.GetSuggestedMetInfo"/> builds its evolution chain, but keeps
    /// <b>every</b> candidate instead of picking one. That difference matters: the suggestion helper prefers the
    /// lowest-level wild slot, which is a sensible default for a human filling in a blank form but the wrong
    /// answer when a specific template is the one the entity actually came from. Trying them all and letting the
    /// legality check decide is what makes this reliable rather than a guess.
    /// </remarks>
    private static List<(byte Level, ushort Location)> GetMetCandidates(PKM pk)
    {
        var results = new List<(byte, ushort)>();
        try
        {
            var lvl = pk.CurrentLevel;
            var version = pk.Version;
            var generation = pk.Generation;
            var origin = new EvolutionOrigin(pk.Species, version.Context, generation, lvl, lvl, OriginOptions.SkipChecks);

            Span<EvoCriteria> chain = stackalloc EvoCriteria[EvolutionTree.MaxEvolutions];
            var count = EvolutionChain.GetOriginChain(chain, pk, origin);
            if (count == 0)
                return results;

            var evos = chain[..count].ToArray();
            var generator = EncounterGenerator.GetGenerator(version, generation);
            var seen = new HashSet<(byte, ushort)>();
            foreach (var enc in generator.GetPossible(pk, evos, version, EncounterTypeGroup.Slot | EncounterTypeGroup.Static))
            {
                var pair = (enc.LevelMin, enc.Location);
                if (seen.Add(pair))
                    results.Add(pair);
            }
        }
        catch (Exception)
        {
            // Corrupted data can make chain building or generation throw; treat as "no candidates".
        }
        return results;
    }

    /// <summary>
    /// Tera Type findings this repair targets.
    /// </summary>
    private static readonly LegalityCheckResultCode[] TeraFailureCodes =
    [
        LegalityCheckResultCode.TeraTypeIncorrect,
        LegalityCheckResultCode.TeraTypeMismatch,
    ];

    /// <summary>
    /// Repairs a Gen9 entity whose Tera Type does not match what its encounter allows, by trying every type
    /// and keeping the first that makes the entity legal.
    /// </summary>
    /// <remarks>
    /// Overwhelmingly a transfer artefact. <c>MiscVerifierPK9.VerifyEncounter</c> holds imported entities (any
    /// encounter that is not Gen9-native, plus anything flagged <c>GO_HOME</c>) to two conditions: the override
    /// byte must not be <see cref="TeraTypeUtil.OverrideNone"/>, and the original byte must match one of the
    /// types reachable through the entity's Gen9 evolution chain. A Pokemon that never existed in Gen9 has no
    /// meaningful value in either byte until something writes one, so a save edited outside HOME's transfer path
    /// routinely arrives with both wrong.
    /// <para/>
    /// Brute force is the honest approach here: the field is a single byte with only 18 real types, so trying
    /// them all and letting <see cref="LegalityAnalysis"/> arbitrate is both exhaustive and cheap -- far more
    /// reliable than deriving the "expected" type from the personal table, which would have to re-implement the
    /// evolution-chain and form-change rules the verifier already encodes.
    /// <para/>
    /// Two shapes are tried per type, matching the two ways the pair is legitimately written: HOME's transfer
    /// shape sets both bytes, while a Gen9-native encounter keeps the override at
    /// <see cref="TeraTypeUtil.OverrideNone"/> and carries the real type in the original byte.
    /// <para/>
    /// Strict guard only. Tera Type is not on HOME's immutable list, but a partially-correct type is worthless,
    /// and unlike a memory there is no "safe baseline" value to fall back to -- either a type resolves the
    /// finding completely or none does.
    /// </remarks>
    public static BulkEditResult FixTeraTypeForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            if (pk is not PK9 pk9 || FindFirst(GetInvalidCodes(pk), TeraFailureCodes) is null)
            {
                skippedNotApplicable++;
                continue;
            }

            if (TryFixTeraType(pk9))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static bool TryFixTeraType(PK9 pk)
    {
        for (byte type = 0; type <= TeraTypeUtil.MaxType; type++)
        {
            var t = (MoveType)type;
            // Transfer shape: both bytes carry the type.
            if (TryApplyGuarded(pk, p => ApplyTeraType((PK9)p, t, t)))
                return true;
            // Native shape: the override stays unset and the original byte carries the type.
            if (TryApplyGuarded(pk, p => ApplyTeraType((PK9)p, t, (MoveType)TeraTypeUtil.OverrideNone)))
                return true;
        }
        return false;
    }

    private static void ApplyTeraType(PK9 pk, MoveType original, MoveType @override)
    {
        pk.TeraTypeOriginal = original;
        pk.TeraTypeOverride = @override;
    }

    /// <summary>
    /// One repair from this editor, packaged so it can be invoked against a single entity instead of a whole
    /// box. See <see cref="EntityRepairs"/>.
    /// </summary>
    /// <param name="Name">Menu label.</param>
    /// <param name="AppliesTo">
    /// Cheap structural test for whether the repair could ever be relevant to this entity -- format, entity type
    /// or interface support only. Deliberately <b>not</b> a legality test: answering "would this actually change
    /// anything" means running the repair, and several of these run dozens of <see cref="LegalityAnalysis"/>
    /// passes, which is far too slow to do for every entry each time a menu opens. So this filters out the
    /// obviously impossible (Tera on a Gen5 entity) and lets the rest report "nothing to change" when clicked.
    /// </param>
    /// <param name="Apply">Runs the repair; returns whether the entity was actually modified.</param>
    public readonly record struct EntityRepair(string Name, Func<PKM, bool> AppliesTo, Func<PKM, SaveFile?, bool> Apply);

    private static bool Always(PKM pk) => true;

    /// <summary>
    /// Every repair in this editor that needs no configuration, in the order they should be attempted.
    /// </summary>
    /// <remarks>
    /// Order is not cosmetic. Encounter-level repairs come first because the matched encounter is the template
    /// every later verifier judges against -- fixing a moveset or a Tera Type before the encounter resolves
    /// scores each candidate against a template that was never found, so the repair reverts and the entity looks
    /// unfixable. This mirrors the sequence the bulk dialog runs its steps in.
    /// <para/>
    /// Edits that need a value chosen by the user (ball, met location, nature/EV preset, shiny on or off, size)
    /// are deliberately absent: they are preferences rather than repairs, and there is nowhere to pick the value
    /// from in a single-click menu. Those stay in the bulk dialog and the normal editor fields.
    /// </remarks>
    public static IReadOnlyList<EntityRepair> EntityRepairs { get; } =
    [
        new("Fix fake event data (Fateful / ribbons)", Always, static (pk, _) => FixFakeEventDataForAll([pk]).Modified > 0),
        new("Repair met level / location", Always, static (pk, _) => RepairMetDataForAll([pk]).Modified > 0),
        new("Re-home to a real encounter", Always, static (pk, _) => RehomeToOrdinaryEncounterForAll([pk]).Modified > 0),
        new("Re-origin to this game (native encounter, keeps shiny)", Always,
            static (pk, sav) => sav is not null && TryReoriginToNativeEncounter(pk, sav)),
        new("Re-origin to this game (as a bred egg)", Always,
            static (pk, sav) => sav is not null && ConvertToNativeEggForAll([pk], sav, (byte)Ball.Poke, GetDefaultMetLocation(sav)).Converted > 0),
        new("Fix Tera Type", static pk => pk is PK9, static (pk, _) => FixTeraTypeForAll([pk]).Modified > 0),
        new("Revert battle-only form", Always, static (pk, _) => FixBattleOnlyFormsForAll([pk]).Modified > 0),
        new("Fix illegal moves", Always, static (pk, _) => SetLegalMovesForAll([pk]).Modified > 0),

        new("Fix OT memory", static pk => pk is IMemoryOT, static (pk, _) => FixOriginalTrainerMemoryForAll([pk]).Modified > 0),
        new("Fix OT friendship", Always, static (pk, _) => FixOriginalTrainerFriendshipForAll([pk]).Modified > 0),
        new("Fix trash bytes and HT memory", Always, static (pk, sav) => sav is not null && FixTrashAndMemoryForAll([pk], sav).Modified > 0),
        new("Fix Fishy warnings", Always, static (pk, _) => FixFishyWarningsForAll([pk]).Modified > 0),

        new("Fix transfer Nature", static pk => pk is PK7, static (pk, _) => FixTransferNatureForAll([pk]).Modified > 0),
        new("Fix transfer PID / EC / ability", Always, static (pk, _) => FixTransferSideFieldsForAll([pk]).Modified > 0),

        new("Make Virtual Console shiny Square", static pk => pk is PK7 { VC: true }, static (pk, _) => MakeVirtualConsoleShinySquareForAll([pk]).Modified > 0),
        new("Make shiny Square", static pk => pk.IsShiny, static (pk, sav) => MakeShiniesSquareForAll([pk], sav).Modified > 0),

        new("Max PP Ups", Always, static (pk, _) => SetMaxPPUpsForAll([pk]).Modified > 0),
        new("Max IVs", Always, static (pk, _) => SetMaxIVsForAll([pk]).Modified > 0),
        new("Align size to scale", Always, static (pk, _) => AlignSizeToScaleForAll([pk]).Modified > 0),
    ];

    /// <summary>
    /// Size-extreme repairs, kept separate from <see cref="EntityRepairs"/> on purpose: maximizing and
    /// minimizing are opposite goals, so bundling either into "try all applicable repairs" would mean running
    /// one over an entity that had just been pushed the other way by nothing in particular -- there is no
    /// "applies to this entity" test that makes sense for a preference like this, only for an actual defect.
    /// Exposed as its own menu section instead.
    /// </summary>
    public static IReadOnlyList<EntityRepair> SizeRepairs { get; } =
    [
        new("Maximize legal size (Scale/Height/Weight)", static pk => pk is IScaledSize or IScaledSize3,
            static (pk, _) => TrySetSizeExtreme(pk, SizeGoal.Maximize)),
        new("Minimize legal size (Scale/Height/Weight)", static pk => pk is IScaledSize or IScaledSize3,
            static (pk, _) => TrySetSizeExtreme(pk, SizeGoal.Minimize)),

        new("Clear HOME tracker", static pk => pk is IHomeTrack, static (pk, _) => ClearHomeTrackerForAll([pk]).Modified > 0),
        new("Regenerate PID / tracker / EC", Always, static (pk, sav) => RegeneratePIDTrackerAndECForAll([pk], sav).Modified > 0),
    ];

    /// <summary>
    /// Runs every applicable repair in <see cref="EntityRepairs"/> against <paramref name="pk"/> in order,
    /// returning the names of those that changed something.
    /// </summary>
    /// <remarks>
    /// Repairs are attempted in sequence on the same entity rather than independently, because they compound:
    /// resolving the encounter is what lets the memory, Tera and move repairs find the right template. Each one
    /// carries its own guard, so a repair that cannot help reverts itself and the next still gets a clean run.
    /// </remarks>
    public static List<string> RepairEntity(PKM pk, SaveFile? sav = null)
    {
        var applied = new List<string>();
        foreach (var repair in EntityRepairs)
        {
            try
            {
                if (repair.AppliesTo(pk) && repair.Apply(pk, sav))
                    applied.Add(repair.Name);
            }
            catch (Exception)
            {
                // FIX (2026-09-19): one repair throwing (corrupted/edge-case entity data) used to propagate
                // straight out of "try all applicable repairs" and crash the whole app on the UI thread. Each
                // repair already reverts its own byte buffer via TryApplyGuarded before it can throw past that
                // point, so pk is left exactly as it was before this repair ran; skip it and keep going.
            }
        }
        return applied;
    }

    /// <param name="Converted">Entities rebuilt as a native egg of the save's own game.</param>
    /// <param name="NotConvertible">Entities that could be eggs in principle, but could not be made legal as one.</param>
    /// <param name="NotApplicable">Entities already native to the save's own generation, so there was nothing to re-origin.</param>
    /// <param name="Empty">Empty slots.</param>
    /// <param name="ProtectedEvent">Event distributions of any kind (Cherish Ball, Fateful flag, or a matched Mystery Gift), never touched.</param>
    /// <param name="NotEggCapable">Legendaries, mythicals and other species that can never hatch from an egg; refused outright.</param>
    /// <param name="FirstFailure">
    /// Diagnostic: for the first entity that could not be converted, the findings that blocked it. The guard
    /// only reports success or failure, so without this a run that converts nothing gives no indication whether
    /// the cause is one shared problem or many different ones.
    /// </param>
    public readonly record struct NativeOriginResult(int Converted, int NotConvertible, int NotApplicable, int Empty, int ProtectedEvent, int NotEggCapable, string? FirstFailure = null);

    /// <summary>
    /// Rewrites foreign-origin entities as an egg hatched in the save's own game, which removes the HOME tracker
    /// requirement entirely.
    /// </summary>
    /// <remarks>
    /// The problem this solves: <c>HomeTrackerUtil.IsRequired(current, origin) => origin != current</c> means any
    /// entity whose encounter belongs to an earlier generation must carry a HOME tracker, because HOME is the
    /// only thing that can move a Pokemon across that boundary. A tracker cannot be fabricated -- it is a key
    /// into HOME's own database, so an invented value fails the lookup exactly as reliably as a missing one. The
    /// only local fix is to stop claiming foreign origin at all, and a bred egg is the cleanest such claim: it
    /// asserts nothing except that the player hatched it in the game they are holding.
    /// <para/>
    /// Fields are edited in place rather than regenerated from the template, deliberately. Rebuilding would
    /// discard PID, IVs, nature and moves, which is most of what makes a given Pokemon worth keeping; editing
    /// keeps the entity recognisably itself and lets the guard decide whether the new origin story fits it.
    /// <para/>
    /// <b>Only breedable species can take this route.</b> Legendaries and mythicals have no egg encounter, so
    /// they are reported under <see cref="NativeOriginResult.NotConvertible"/> rather than forced -- on this
    /// save that is the large majority of the affected entities. Their only native option is an ordinary
    /// in-game encounter, which is a different and more destructive edit (most are shiny-locked), so it is not
    /// bundled in here.
    /// <para/>
    /// Strict guard: origin, met data, ball and trainer are all HOME-immutable and unrecoverable, so a partial
    /// result is worse than none.
    /// </remarks>
    /// <param name="mons">Entities to convert.</param>
    /// <param name="sav">Save supplying the destination game, trainer identity and hatch date bounds.</param>
    /// <param name="ball">Preferred ball; falls back through the encounter's other legal balls if it is refused.</param>
    /// <param name="metLocation">Hatch location to claim.</param>
    /// <param name="restampTrainer">Overwrite OT name, IDs, gender and language from <paramref name="sav"/>.</param>
    public static NativeOriginResult ConvertToNativeEggForAll(IEnumerable<PKM> mons, SaveFile sav, byte ball, ushort metLocation, bool restampTrainer = true)
    {
        int converted = 0, notConvertible = 0, notApplicable = 0, empty = 0, protectedEvent = 0, notEggCapable = 0;
        string? firstFailure = null;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                empty++;
                continue;
            }
            if (IsEventOrigin(pk))
            {
                protectedEvent++;
                continue;
            }
            if (IsEggIneligible(pk))
            {
                notEggCapable++;
                continue;
            }
            if (!IsForeignOrigin(pk, sav))
            {
                notApplicable++;
                continue;
            }

            if (TryConvertToNativeEgg(pk, sav, ball, metLocation, restampTrainer))
            {
                converted++;
            }
            else
            {
                notConvertible++;
                firstFailure ??= DescribeConversionFailure(pk, sav, ball, metLocation, restampTrainer);
            }
        }
        return new NativeOriginResult(converted, notConvertible, notApplicable, empty, protectedEvent, notEggCapable, firstFailure);
    }

    /// <summary>
    /// Reports whether <paramref name="pk"/> is a species that must never be turned into an egg.
    /// </summary>
    /// <remarks>
    /// Legendaries and mythicals do not breed, and neither do a number of ordinary species (the Ditto line, the
    /// Paradox forms, baby-stage exclusions). The strict guard would reject all of them anyway -- there is no
    /// egg encounter for the analysis to match, so the conversion cannot come out legal -- but relying on that
    /// is the same mistake the ball and met-location setters made with Cherish Ball. A guard proves "the result
    /// is legal", not "the edit was appropriate", and an explicit refusal cannot be undone by a future change to
    /// how permissive the guard is.
    /// <para/>
    /// It also makes the reporting honest: these are counted as
    /// <see cref="NativeOriginResult.NotEggCapable"/> rather than lumped in with entities that genuinely could
    /// have been eggs and failed for some fixable reason.
    /// </remarks>
    private static bool IsEggIneligible(PKM pk)
    {
        if (SpeciesCategory.IsLegendary(pk.Species) || SpeciesCategory.IsMythical(pk.Species))
            return true;
        if (!Breeding.CanHatchAsEgg(pk.Species))
            return true;
        return !Breeding.CanHatchAsEgg(pk.Species, pk.Form, pk.Context);
    }

    /// <summary>
    /// Reports whether <paramref name="pk"/> came from a different game generation than the save it is sitting in.
    /// </summary>
    /// <remarks>
    /// The gate for re-origining, and deliberately broader than "does it need a HOME tracker". The tracker rule
    /// only exists from Gen8 onward -- a PK7 has no tracker field at all -- so keying on it made this a
    /// Sword/Shield and Scarlet/Violet feature only, and did nothing in a Sun, Moon or Ultra Sun/Moon save where
    /// the same cleanup is just as useful. Comparing contexts is the underlying condition either way:
    /// <c>HomeTrackerUtil.IsRequired(current, origin) => origin != current</c> is exactly this test, and a
    /// tracker is demanded precisely because the entity crossed a generation boundary.
    /// <para/>
    /// The encounter's context is what matters, not <see cref="PKM.Context"/>: an entity's storage format
    /// changes on transfer, but where it was originally obtained does not, and that is what the checker judges.
    /// </remarks>
    private static bool IsForeignOrigin(PKM pk, SaveFile sav)
    {
        try
        {
            var la = new LegalityAnalysis(pk);
            return la.Info.EncounterMatch.Context != sav.Context;
        }
        catch (Exception)
        {
            return false; // Cannot tell; leave it alone rather than guess.
        }
    }

    /// <summary>
    /// Reports whether <paramref name="pk"/> is an event distribution of any kind.
    /// </summary>
    /// <remarks>
    /// Wider than <see cref="IsEventDistribution"/>, which tests only for a Cherish Ball. That is the right test
    /// for the ball and met-location setters, where the Cherish Ball itself is the thing being protected, but it
    /// is not sufficient here: plenty of real events arrive in an ordinary ball. A Gen3/4 Mystery Gift predates
    /// the Cherish Ball entirely, an in-game event Pokemon can come in a Poke Ball, and only the Fateful
    /// Encounter flag or the matched encounter marks them as special.
    /// <para/>
    /// Three independent signals, any one of which is enough: the Cherish Ball, the Fateful Encounter flag, and
    /// an encounter that actually resolves to a <see cref="MysteryGift"/>. Re-origining any of these would
    /// destroy provenance that cannot be reconstructed, so the test errs toward refusing.
    /// </remarks>
    private static bool IsEventOrigin(PKM pk)
    {
        if (IsEventDistribution(pk))
            return true;
        if (pk.FatefulEncounter)
            return true;
        try
        {
            return new LegalityAnalysis(pk).Info.EncounterMatch is MysteryGift;
        }
        catch (Exception)
        {
            return true; // Cannot tell; refuse rather than risk destroying an event.
        }
    }

    /// <summary>
    /// Applies the conversion to a throwaway copy without the guard, and reports what the checker objected to.
    /// </summary>
    /// <remarks>
    /// Purely diagnostic. Move and relearn-move results are included explicitly because they do not appear in
    /// <see cref="LegalityAnalysis.Results"/> -- they live in <c>Info.Moves</c>/<c>Info.Relearn</c> and are
    /// folded into <see cref="LegalityAnalysis.Valid"/> separately -- so a failure caused by them otherwise
    /// shows up as an empty list of reasons.
    /// </remarks>
    private static string DescribeConversionFailure(PKM pk, SaveFile sav, byte ball, ushort metLocation, bool restampTrainer)
    {
        try
        {
            var copy = pk.Clone();
            ApplyNativeEgg(copy, sav, ball, metLocation, restampTrainer);
            copy.RefreshChecksum();
            var la = new LegalityAnalysis(copy);
            var reasons = new List<string>();
            foreach (var r in la.Results)
            {
                if (r.Judgement != Severity.Invalid)
                    continue;
                var name = r.Result.ToString();
                if (!reasons.Contains(name))
                    reasons.Add(name);
            }
            for (int i = 0; i < la.Info.Moves.Length; i++)
            {
                if (!la.Info.Moves[i].Valid)
                    reasons.Add($"move{i + 1}");
            }
            for (int i = 0; i < la.Info.Relearn.Length; i++)
            {
                if (!la.Info.Relearn[i].Valid)
                    reasons.Add($"relearn{i + 1}");
            }
            var enc = la.Info.EncounterMatch.GetType().Name;
            var why = reasons.Count == 0 ? "no findings" : string.Join(", ", reasons);
            return $"e.g. {(Species)pk.Species} (from {pk.Version}) -> {enc}: {why}";
        }
        catch (Exception ex)
        {
            return $"e.g. {(Species)pk.Species}: threw {ex.GetType().Name}";
        }
    }

    private static bool TryConvertToNativeEgg(PKM pk, SaveFile sav, byte ball, ushort metLocation, bool restampTrainer)
    {
        // Preferred ball first, then everything else the destination permits. A ball that the egg's inheritance
        // rules refuse is a per-species answer (Beast Ball is fine for most, but not for every line), so trying
        // rather than predicting keeps this correct without duplicating BallApplicator's tables.
        Span<Ball> legal = stackalloc Ball[BallApplicator.MaxBallSpanAlloc];
        var candidates = new List<byte> { ball };
        var count = BallApplicator.GetLegalBalls(legal, pk);
        for (int i = 0; i < count; i++)
        {
            var b = (byte)legal[i];
            if (b != ball)
                candidates.Add(b);
        }

        foreach (var candidate in candidates)
        {
            if (TryApplyGuarded(pk, p => ApplyNativeEgg(p, sav, candidate, metLocation, restampTrainer)))
                return true;
        }
        return false;
    }

    private static void ApplyNativeEgg(PKM pk, SaveFile sav, byte ball, ushort metLocation, bool restampTrainer)
    {
        // Shininess is not stored -- it is derived as ShinyXor = ID32 ^ PID. Restamping the trainer therefore
        // changes it for free: a shiny Pokemon silently turns ordinary, and an ordinary one can turn shiny.
        // Capture the state now and restore it after the IDs move.
        var wasShiny = pk.IsShiny;
        var wasSquare = wasShiny && pk.ShinyXor == 0;
        pk.Version = sav.Version;
        pk.MetLocation = metLocation;
        pk.EggLocation = Locations.GetDaycareLocation(sav.Generation, sav.Version);
        pk.MetLevel = 1;
        pk.Ball = ball;
        pk.IsEgg = false; // hatched
        if (pk.CurrentLevel < 1)
            pk.CurrentLevel = 1;

        // Both are HOME-facing leftovers from the previous origin and are meaningless for a locally bred egg.
        if (pk is IHomeTrack home)
            home.Tracker = 0;
        if (pk is IBattleVersion bv)
            bv.BattleVersion = 0;

        // An egg's obedience level tracks its met level; leaving the old value behind is an immediate mismatch.
        if (pk is IObedienceLevel ob)
            ob.ObedienceLevel = pk.MetLevel;

        if (restampTrainer)
        {
            pk.OriginalTrainerName = sav.OT;
            pk.TID16 = sav.TID16;
            pk.SID16 = sav.SID16;
            pk.OriginalTrainerGender = sav.Gender;
            pk.Language = sav.Language;
            // The entity now claims the save owner as its Original Trainer, so it cannot still be flagged as
            // being held by someone else -- that is TransferCurrentHandlerInvalid. Only the flag is reset.
            pk.CurrentHandler = 0;

            // The rest of the Handling Trainer block is deliberately left alone. The instinct is to clear it -- the entity is
            // now the save owner's, so why would anyone else hold it -- but clearing it fails with
            // MemoryHTLanguage: the HT language field is expected to stay populated (the same reason PK9's own
            // FixMemories skips it), and zeroing it alongside the memories makes an otherwise clean conversion
            // illegal. An OT-restamped entity that is still flagged as traded is valid; one with a half-cleared
            // handler block is not.
        }

        if (restampTrainer)
            RestoreShinyState(pk, wasShiny, wasSquare);

        SyncEncounterDates(pk);

        // Everything below depends on the encounter having already changed, so it runs last and in this order.
        pk.RefreshChecksum();

        // Ribbons and marks are judged against the matched encounter, and a Pokemon carrying its old origin's
        // awards (a Hisui-exclusive mark, a Galar tower title) fails as soon as it claims to be a Paldea egg.
        FixRibbons(pk);
        pk.RefreshChecksum();

        // Moves last, and this one is easy to miss: LegalityAnalysis.Valid is not only "no Invalid results" --
        // it also requires MoveResult.AllValid for the moveset AND the relearn moves, and those live in
        // Info.Moves/Info.Relearn rather than in Results. A foreign moveset therefore fails the guard while
        // showing nothing at all in the invalid-findings list, which makes it look like the conversion failed
        // for no reason.
        FixEggMoves(pk);
    }

    /// <summary>
    /// Reconciles relearn moves and the moveset against a freshly-assigned egg encounter.
    /// </summary>
    /// <remarks>
    /// Relearn moves are fixed <b>before</b> the moveset, which is the opposite of the order
    /// <see cref="FixMoves"/> uses and matters here. On an egg the relearn slots are part of what the encounter
    /// expects, so <c>SetMoveset</c> run first picks its moves against a half-built template and produces a
    /// moveset that goes invalid the moment the relearn slots are then corrected.
    /// <para/>
    /// Iterated rather than done once because the two settle against each other: correcting the relearn moves
    /// changes what the moveset may contain, and vice versa. Three passes is comfortably enough in practice, and
    /// the loop exits as soon as the entity is legal.
    /// </remarks>
    private static void FixEggMoves(PKM pk)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            var la = new LegalityAnalysis(pk);
            if (!la.Parsed)
                return;
            if (!MoveResult.AllValid(la.Info.Relearn))
            {
                pk.SetRelearnMoves(la);
                pk.RefreshChecksum();
            }
            var after = new LegalityAnalysis(pk);
            if (!MoveResult.AllValid(after.Info.Moves))
            {
                pk.SetMoveset();
                pk.RefreshChecksum();
            }
            if (new LegalityAnalysis(pk).Valid)
                return;
        }
    }

    /// <summary>
    /// Re-derives the PID so <paramref name="pk"/> keeps the shiny state it had before its trainer IDs changed.
    /// </summary>
    /// <remarks>
    /// Safe to do here because a bred egg has no PID correlation to preserve -- any PID is legal for it -- which
    /// is exactly why the same rewrite would be wrong on a seed-derived encounter.
    /// <para/>
    /// Square is restored as Square rather than merely "shiny": the two are visually distinct from Gen8 onward,
    /// and quietly downgrading one to a Star is the sort of change that is only noticed much later.
    /// </remarks>
    private static void RestoreShinyState(PKM pk, bool wasShiny, bool wasSquare)
    {
        if (!wasShiny)
        {
            if (pk.IsShiny)
                pk.SetUnshiny();
            return;
        }

        if (wasSquare)
        {
            if (!pk.IsShiny || pk.ShinyXor != 0)
                pk.SetShiny(Shiny.AlwaysSquare);
            return;
        }

        if (!pk.IsShiny || pk.ShinyXor == 0)
            pk.SetShiny(Shiny.AlwaysStar);
    }

    /// <summary>
    /// Re-origins a single entity onto an ordinary encounter from the save's own game, keeping its shiny state.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="ConvertToNativeEggForAll"/> for everything that cannot be an egg --
    /// legendaries and mythicals above all. Instead of claiming to have been bred, the entity claims to have
    /// been caught where the current game actually offers it, which removes the cross-generation HOME tracker
    /// requirement for exactly the same reason.
    /// <para/>
    /// Shininess is a hard constraint, not a preference. Most legendary encounters are shiny-locked, so a shiny
    /// entity simply has no legal home among them and is left alone -- silently producing a non-shiny legendary
    /// would destroy the only property that made it worth keeping. Where a shiny-capable encounter does exist
    /// (Sword/Shield's Dynamax Adventures are the useful case, since their shininess is rolled at the catch
    /// screen rather than fixed by the template) the entity lands there with its shiny state intact.
    /// <para/>
    /// Candidates come from <see cref="EncounterTypeGroup.Slot"/> and <see cref="EncounterTypeGroup.Static"/>
    /// only, so this can never move an entity onto a Mystery Gift.
    /// </remarks>
    /// <returns>True if the entity was re-origined.</returns>
    public static bool TryReoriginToNativeEncounter(PKM pk, SaveFile sav, bool restampTrainer = true)
    {
        if (pk.Species == 0 || IsEventOrigin(pk) || !IsForeignOrigin(pk, sav))
            return false;

        var wasShiny = pk.IsShiny;
        var wasSquare = wasShiny && pk.ShinyXor == 0;

        foreach (var enc in GetNativeEncounters(pk, sav))
        {
            // A shiny-locked template can never hold a shiny entity, so skip it before spending a guarded
            // attempt on it. Doing this by inspection rather than by trial keeps the rejection honest: the guard
            // would also refuse, but only after the mutation, and a later relaxation of the guard must not be
            // able to turn this into a silent de-shinying.
            if (wasShiny && enc is IShinyPotential { Shiny: Shiny.Never })
                continue;

            var target = enc;
            var snapshot = pk.Data.ToArray();

            // Try to keep Square first, then settle for Star. Some encounters pin the shiny type outright --
            // Dynamax Adventures hardcode XOR 1 -- so asking for Square there produces an illegal entity and the
            // guard reverts. Without the Star retry the whole candidate is discarded and a perfectly good
            // re-origin is lost over a cosmetic preference.
            var applied = (wasSquare && TryApplyGuarded(pk, p => ApplyNativeEncounter(p, sav, target, restampTrainer, wasShiny, true)))
                || TryApplyGuarded(pk, p => ApplyNativeEncounter(p, sav, target, restampTrainer, wasShiny, false));
            if (!applied)
                continue;

            // Shiny or not shiny is the constraint; Star vs Square is not. Some encounters pin the shiny type --
            // Dynamax Adventures force XOR 1, so a Square shiny can only land there as a Star -- and refusing
            // that would leave the entity stuck on its foreign origin over a cosmetic difference. Keeping it
            // shiny is what matters; the downgrade is the lesser loss.
            if (pk.IsShiny == wasShiny)
                return true;

            // The guard has already committed at this point, so a mismatch has to be undone explicitly rather
            // than just reported -- otherwise this returns false having silently changed the entity.
            snapshot.CopyTo(pk.Data);
            pk.RefreshChecksum();
        }
        return false;
    }

    /// <summary>
    /// A met location the save's own game actually has, for the single-entity egg re-origin where there is no
    /// dropdown to take one from.
    /// </summary>
    /// <remarks>
    /// Matches the defaults the bulk dialog preselects. Falls back to 0, which the guard rejects if the game
    /// does not accept it -- better than inventing an ID that means something unrelated in that generation,
    /// since met location tables are per-generation and do not share a namespace.
    /// </remarks>
    private static ushort GetDefaultMetLocation(SaveFile sav) => sav.Version switch
    {
        GameVersion.SN or GameVersion.MN or GameVersion.US or GameVersion.UM => 188, // Aether Paradise
        GameVersion.SW or GameVersion.SH => 220,  // Crown Shrine (Crown Tundra)
        GameVersion.SL or GameVersion.VL => 124,  // Area Zero (5)
        _ => 0,
    };

    private static List<IEncounterable> GetNativeEncounters(PKM pk, SaveFile sav)
    {
        var results = new List<IEncounterable>();
        try
        {
            var version = sav.Version;
            var generation = sav.Generation;
            var origin = new EvolutionOrigin(pk.Species, sav.Context, generation, 1, 100, OriginOptions.SkipChecks);

            var probe = EntityBlank.GetBlank(sav.Context);
            probe.Species = pk.Species;
            probe.Form = pk.Form;
            probe.Version = version;
            probe.CurrentLevel = 100;

            Span<EvoCriteria> chain = stackalloc EvoCriteria[EvolutionTree.MaxEvolutions];
            var count = EvolutionChain.GetOriginChain(chain, probe, origin);
            if (count == 0)
                return results;

            var evos = chain[..count].ToArray();
            var generator = EncounterGenerator.GetGenerator(version, generation);
            foreach (var enc in generator.GetPossible(probe, evos, version, EncounterTypeGroup.Slot | EncounterTypeGroup.Static))
            {
                if (enc.Species == pk.Species && enc.Context == sav.Context)
                    results.Add(enc);
            }
        }
        catch (Exception)
        {
            // Corrupted data; treat as "no candidates".
        }
        return results;
    }

    private static void ApplyNativeEncounter(PKM pk, SaveFile sav, IEncounterable enc, bool restampTrainer, bool wasShiny, bool wasSquare)
    {
        pk.Version = sav.Version;
        pk.MetLocation = enc.Location;
        pk.MetLevel = enc.LevelMin;
        if (pk.CurrentLevel < enc.LevelMin)
            pk.CurrentLevel = enc.LevelMin;

        // Caught, not hatched: any egg data left over from the previous origin contradicts the new encounter.
        pk.IsEgg = false;
        pk.EggLocation = 0;

        if (enc is IFixedBall { FixedBall: not Ball.None } fb)
            pk.Ball = (byte)fb.FixedBall;

        if (pk is IHomeTrack home)
            home.Tracker = 0;
        if (pk is IBattleVersion bv)
            bv.BattleVersion = 0;
        if (pk is IObedienceLevel ob)
            ob.ObedienceLevel = pk.MetLevel;

        if (restampTrainer)
        {
            pk.OriginalTrainerName = sav.OT;
            pk.TID16 = sav.TID16;
            pk.SID16 = sav.SID16;
            pk.OriginalTrainerGender = sav.Gender;
            pk.Language = sav.Language;
            pk.CurrentHandler = 0;
            RestoreShinyState(pk, wasShiny, wasSquare);
        }

        pk.MetDate ??= DateOnly.FromDateTime(DateTime.Now);
        pk.RefreshChecksum();
        FixRibbons(pk);
        pk.RefreshChecksum();
        FixEggMoves(pk);
    }

    /// <param name="Synced">Entities whose dates were adjusted.</param>
    /// <param name="AlreadyConsistent">Entities whose dates were already fine.</param>
    /// <param name="Reverted">Entities where the adjustment would have broken legality, so it was undone.</param>
    /// <param name="Empty">Empty slots.</param>
    public readonly record struct DateSyncResult(int Synced, int AlreadyConsistent, int Reverted, int Empty);

    /// <summary>
    /// Makes met date and egg met date agree, so a hatched entity does not read as having been met before it was
    /// received.
    /// </summary>
    /// <remarks>
    /// A bred Pokemon carries two dates: when the egg was obtained and when it hatched. The second cannot
    /// precede the first, and an entity edited field-by-field very often ends up with one date set and the other
    /// blank, or with the pair in the wrong order -- a cheap and very visible tell.
    /// <para/>
    /// Sets the two equal rather than inventing a plausible gap. Same-day hatching is perfectly ordinary, it is
    /// the least assumption available, and it is stable: running this twice never drifts the dates.
    /// </remarks>
    public static DateSyncResult SyncEncounterDatesForAll(IEnumerable<PKM> mons)
    {
        int synced = 0, already = 0, reverted = 0, empty = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                empty++;
                continue;
            }
            if (IsDateConsistent(pk))
            {
                already++;
                continue;
            }

            if (TryApplyNonWorsening(pk, SyncEncounterDates))
                synced++;
            else
                reverted++;
        }
        return new DateSyncResult(synced, already, reverted, empty);
    }

    private static bool IsDateConsistent(PKM pk)
    {
        var egg = pk.EggMetDate;
        var met = pk.MetDate;
        if (!pk.WasEgg)
            return true; // Nothing to reconcile against.
        if (egg is null || met is null)
            return false;
        return met >= egg;
    }

    private static void SyncEncounterDates(PKM pk)
    {
        if (!pk.WasEgg)
            return;

        // Prefer a date the entity already carries over inventing one, so an edit keeps whatever history is
        // genuinely there. Only fall back to today when neither field holds anything usable.
        var egg = pk.EggMetDate;
        var met = pk.MetDate;
        var chosen = egg ?? met ?? DateOnly.FromDateTime(DateTime.Now);
        if (met is not null && egg is not null && met < egg)
            chosen = met.Value; // Hatch date is the earlier one; trust it and pull the egg date back to match.

        pk.EggMetDate = chosen;
        pk.MetDate = chosen;
    }

    /// <summary>
    /// Encounter-matching failures this repair targets.
    /// </summary>
    private static readonly LegalityCheckResultCode[] EncounterFailureCodes =
    [
        LegalityCheckResultCode.EncInvalid,
        LegalityCheckResultCode.EncGiftNotFound,
    ];

    /// <summary>
    /// Re-homes forged "event" Pokemon onto an ordinary wild or static encounter from their own origin game,
    /// replacing the Cherish Ball / event met location that no real Mystery Gift backs.
    /// </summary>
    /// <remarks>
    /// A fake event entity claims an encounter that does not exist, so the checker reports
    /// <see cref="LegalityCheckResultCode.EncGiftNotFound"/> or <see cref="LegalityCheckResultCode.EncInvalid"/>
    /// and then judges every other field against a template it never found -- which is why one forged gift
    /// produces a wall of unrelated-looking errors (ability, PID/EC, encounter type, handler, memories). Give it
    /// a real encounter and most of them resolve at once.
    /// <para/>
    /// <see cref="EncounterSuggestion.GetSuggestedMetInfo"/> is the right source for that encounter: it queries
    /// only <see cref="EncounterTypeGroup.Slot"/> and <see cref="EncounterTypeGroup.Static"/> and prefers the
    /// wild slot, so by construction it can never suggest another Mystery Gift.
    /// <para/>
    /// Three escalating attempts, each committing the least amount of change that works:
    /// <list type="number">
    /// <item>Clear Fateful, apply the suggested met location/level, and re-ball.</item>
    /// <item>Also clear relearn moves and the egg location -- a wild capture has neither, and a forged gift
    /// usually carries both.</item>
    /// <item>Also re-derive the moveset, for gifts whose event-only moves the new encounter cannot learn.</item>
    /// </list>
    /// The ball is re-picked <b>after</b> the met data is applied, because
    /// <see cref="BallApplicator.GetLegalBalls(Span{Ball},PKM)"/> derives the legal set from the matched
    /// encounter -- asking first would score it against the encounter that does not exist. A Poke Ball is
    /// preferred when legal so results are deterministic rather than randomly re-balled.
    /// <para/>
    /// This deliberately <b>discards the event identity</b>: the entity stops claiming to be a gift and becomes
    /// an ordinary caught Pokemon of the same species. Ball, met location, met level and current level all
    /// change. It is the intended trade for entities whose gift never existed, but it is not reversible.
    /// <para/>
    /// Met data and Ball are HOME-immutable, so this honours the "skip HOME-registered" filter.
    /// </remarks>
    public static BulkEditResult RehomeToOrdinaryEncounterForAll(IEnumerable<PKM> mons)
    {
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }

            var invalid = GetInvalidCodes(pk);
            var code = FindFirst(invalid, EncounterFailureCodes);
            if (code is null)
            {
                skippedNotApplicable++;
                continue;
            }

            if (TryRehome(pk, code.Value))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static bool TryRehome(PKM pk, LegalityCheckResultCode code)
    {
        // An event entity is all-or-nothing. Re-homing spends its Cherish Ball, its distribution met location
        // and its met level, none of which can be recovered -- so committing a partial improvement would leave
        // it both illegal AND stripped of the identity that made the original state meaningful. Either an
        // alternative encounter makes it fully legal, or it stays exactly as it was.
        if (IsEventDistribution(pk))
        {
            return TryApplyGuarded(pk, static p => ApplyOrdinaryEncounter(p, clearExtras: false, refreshMoves: false))
                || TryApplyGuarded(pk, static p => ApplyOrdinaryEncounter(p, clearExtras: true, refreshMoves: false))
                || TryApplyGuarded(pk, static p => ApplyOrdinaryEncounter(p, clearExtras: true, refreshMoves: true));
        }

        // Non-event entities have no identity to preserve, so a strict improvement is still worth keeping.
        if (TryClearFindingReducing(pk, code, static p => ApplyOrdinaryEncounter(p, clearExtras: false, refreshMoves: false)))
            return true;
        if (TryClearFindingReducing(pk, code, static p => ApplyOrdinaryEncounter(p, clearExtras: true, refreshMoves: false)))
            return true;
        return TryClearFindingReducing(pk, code, static p => ApplyOrdinaryEncounter(p, clearExtras: true, refreshMoves: true));
    }

    private static void ApplyOrdinaryEncounter(PKM pk, bool clearExtras, bool refreshMoves)
    {
        // The forged gift's calling card. Cleared first so the suggestion below is computed for an ordinary
        // encounter rather than for the gift the entity was pretending to be.
        pk.FatefulEncounter = false;

        var suggestion = EncounterSuggestion.GetSuggestedMetInfo(pk);
        if (suggestion is null)
            return; // No wild/static encounter exists for this species+version; the guard reverts the no-op.

        if (clearExtras)
        {
            pk.SetRelearnMoves(default);
            if (!pk.WasEgg)
                pk.EggLocation = 0;
        }

        var level = suggestion.LevelMin;
        pk.MetLevel = level;
        pk.MetLocation = suggestion.Location;
        pk.CurrentLevel = Math.Max(EncounterSuggestion.GetLowestLevel(pk, level), level);

        SetPreferredLegalBall(pk);

        if (refreshMoves)
            pk.SetMoveset();
    }

    /// <summary>
    /// Replaces the Ball with a legal one for the now-matched encounter, preferring a Poke Ball.
    /// </summary>
    private static void SetPreferredLegalBall(PKM pk)
    {
        Span<Ball> legal = stackalloc Ball[BallApplicator.MaxBallSpanAlloc];
        var count = BallApplicator.GetLegalBalls(legal, pk);
        if (count == 0)
            return;

        var options = legal[..count];
        foreach (var ball in options)
        {
            if (ball != Ball.Poke)
                continue;
            pk.Ball = (byte)ball;
            return;
        }
        pk.Ball = (byte)options[0];
    }

    private static LegalityCheckResultCode? FindFirst(HashSet<LegalityCheckResultCode> set, LegalityCheckResultCode[] codes)
    {
        foreach (var code in codes)
        {
            if (set.Contains(code))
                return code;
        }
        return null;
    }
    /// <summary>
    /// Converts every Star shiny to a Square shiny where the change keeps the entity legal, leaving shininess
    /// itself untouched.
    /// </summary>
    /// <remarks>
    /// Square and Star are not stored fields -- both are shiny, and which one shows is derived from the PID
    /// against the trainer ID: <c>ShinyXor == 0</c> renders Square, <c>1..15</c> renders Star. So the only way
    /// to convert is to rewrite the PID, which is why this is guarded rather than applied blindly.
    /// <para/>
    /// The strict guard is deliberate here, unlike the repair steps: this is a cosmetic preference, not a fix.
    /// An entity whose encounter pins its PID (fixed-PID gifts, correlated Gen3/4/5 encounters, shiny-locked
    /// or Star-locked cards) reverts untouched, and an entity that was already illegal is skipped outright --
    /// its legality cannot be re-verified afterward, so changing its PID would be unverifiable.
    /// <para/>
    /// Below Format 8 the games do not draw the two differently, so the conversion is cosmetically invisible
    /// there. It is still applied, because it is not always cosmetic: a Virtual Console transfer is
    /// <i>expected</i> to be Square, and Star raises a warning -- see
    /// <see cref="MakeVirtualConsoleShinySquareForAll"/>.
    /// <para/>
    /// <paramref name="sav"/> seeds the set of PIDs already in use. Square shinies for a given trainer ID
    /// occupy only ~65,536 of the 2^32 PID values (the upper half is forced to
    /// <c>low ^ TID ^ SID</c>), so converting a whole box collapses the space enough for birthday-paradox
    /// collisions to appear within a few hundred conversions. Without this, the step would quietly manufacture
    /// the very PID duplicates the clone detector exists to find.
    /// <para/>
    /// PID is HOME-immutable, so this honours the "skip HOME-registered" filter.
    /// </remarks>
    public static BulkEditResult MakeShiniesSquareForAll(IEnumerable<PKM> mons, SaveFile? sav = null)
    {
        var taken = BuildTakenPidSet(sav);
        int modified = 0, skippedIllegal = 0, skippedInvalid = 0, skippedNotApplicable = 0, alreadyIllegal = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0)
            {
                skippedInvalid++;
                continue;
            }
            // Not shiny, or already Square: nothing to do either way.
            if (pk.Format < 3 || !pk.IsShiny || pk.ShinyXor == 0)
            {
                skippedNotApplicable++;
                continue;
            }

            bool legalBefore;
            try
            {
                legalBefore = new LegalityAnalysis(pk).Valid;
            }
            catch (Exception)
            {
                legalBefore = false;
            }
            if (!legalBefore)
            {
                alreadyIllegal++;
                continue;
            }

            taken?.Remove(pk.PID); // its own current value must not block it
            if (TrySquareWithoutCollision(pk, taken))
                modified++;
            else
                skippedIllegal++;
            taken?.Add(pk.PID);
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable, alreadyIllegal);
    }

    /// <summary>
    /// Makes the entity a Square shiny without landing on a PID already used elsewhere in the save.
    /// </summary>
    /// <remarks>
    /// The Square PID is fully determined by its lower 16 bits, so a collision is escaped by varying that half
    /// and recomputing rather than by rerolling blindly. Every candidate is still legality-guarded, and the
    /// first attempt keeps the entity's existing lower half so an uncontested conversion changes as little as
    /// possible.
    /// </remarks>
    private static bool TrySquareWithoutCollision(PKM pk, HashSet<uint>? taken)
    {
        const int attempts = 16;
        var low = pk.PID & 0xFFFF;
        for (int i = 0; i < attempts; i++)
        {
            var candidate = ShinyUtil.GetShinyPID(pk.TID16, pk.SID16, low, 0);
            if (taken?.Contains(candidate) != true)
            {
                if (TryApplyGuarded(pk, p => p.PID = candidate))
                    return true;
                // A legality failure here is a property of the encounter (pinned or Star-locked PID), not of
                // this particular value, so trying more lower halves cannot help.
                return false;
            }
            low = (low + 1) & 0xFFFF;
        }
        return false;
    }
    /// <summary>
    /// True if the entity is in a Cherish Ball, which marks it as a Mystery Gift distribution.
    /// </summary>
    /// <remarks>
    /// A Cherish Ball is only ever issued with an event distribution, and the gift's own record pins the ball
    /// and met data. Overwriting either detaches the entity from the card it came from, and because the
    /// original values are gone the entity can never be matched back to that gift -- the damage is permanent
    /// and no re-run can undo it.
    /// <para/>
    /// The legality guard alone is not sufficient protection. It reverts the change on a <i>legal</i> event
    /// Pokemon, but an event Pokemon that is already illegal for some unrelated reason goes through the
    /// non-worsening guard instead, where overwriting the ball may add no new finding and so would be kept.
    /// That is precisely the case where the loss is unrecoverable, so these bulk setters refuse Cherish Ball
    /// entities outright rather than relying on the guard to catch it.
    /// <para/>
    /// This does not affect <see cref="RehomeToOrdinaryEncounterForAll"/>, which deliberately re-homes forged
    /// events: that step only acts on entities whose encounter <i>failed</i> to match, so a genuine gift is
    /// never in its scope.
    /// </remarks>
    private static bool IsEventDistribution(PKM pk) => pk.Ball == (byte)Ball.Cherish;
}
