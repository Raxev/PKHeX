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
    /// <param name="scaleSearchLimit">
    /// How many values below maximum to try when the maximum itself cannot be applied legally. Each attempt
    /// costs a full legality analysis, so this is deliberately bounded rather than scanning all 255.
    /// </param>
    public static BulkEditResult SetMaxSizeForAll(IEnumerable<PKM> mons, int scaleSearchLimit = 64)
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

            // Three escalating attempts, each individually guarded, so a failure at one step still leaves the
            // entity untouched and lets the next step try:
            //   1. Max size with the Jumbo Mark, which the game awards automatically at that size.
            //   2. Max size WITHOUT the mark. Required because MarkRules.IsMarkAllowedJumbo also demands
            //      EvolutionHistory.HasVisitedGen9 and a mark-capable encounter (IsEncounterMarkLost excludes
            //      e.g. Nincada -> Shedinja). Without this fallback, an entity that simply cannot hold the mark
            //      would have the whole edit reverted and get no size increase at all.
            //   3. The largest legal value below maximum, for encounters that constrain scale to a range
            //      (EncounterOutbreak9's ScaleMin/ScaleMax, EncounterFixed9's MinScaleStrongTera floor).
            if (TrySetSize(pk, byte.MaxValue, withJumbo: true)
                || TrySetSize(pk, byte.MaxValue, withJumbo: false)
                || TryGrowToLargestLegal(pk, scaleSearchLimit))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedUnsupported);
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
        // Resync the derived displayed height/weight (meters/kg) where the format tracks it separately;
        // stale Absolute values aren't a legality problem here, but leaving them stale would be a visible bug.
        if (pk is IScaledSizeValue sv)
        {
            sv.ResetHeight();
            sv.ResetWeight();
        }
    }

    /// <summary>
    /// Walks down from maximum looking for the largest size this entity can legally hold. Never shrinks the
    /// entity: the walk stops once it reaches the size it already has.
    /// </summary>
    private static bool TryGrowToLargestLegal(PKM pk, int limit)
    {
        var current = pk switch
        {
            IScaledSize3 s3 => s3.Scale,
            IScaledSize s2 => s2.HeightScalar,
            _ => byte.MaxValue,
        };

        for (int i = 1; i <= limit; i++)
        {
            var candidate = byte.MaxValue - i;
            if (candidate <= current)
                break; // anything further down would make it smaller than it already is
            if (TrySetSize(pk, (byte)candidate, withJumbo: false))
                return true;
        }
        return false;
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
    /// </summary>
    /// <remarks>
    /// Unlike Handling Trainer memory there is no single canonical OT memory to apply, so this searches rather
    /// than guessing: several values are typically acceptable for a given encounter (a Dynamax Adventure catch
    /// accepts memories 8, 9, 11, 12, 13 and 15 among others), and which ones depends on the encounter.
    /// Intensity comes from <see cref="MemoryContext.GetMinimumIntensity"/> and feeling from
    /// <see cref="MemoryContext8.GetRandomFeeling8"/>, so the applied memory is internally consistent.
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
            // Only Gen8 has a feeling helper here; other contexts are left alone rather than guessed at.
            if (pk is not IMemoryOT || pk.Context != EntityContext.Gen8 || !IsMissingOTMemory(pk))
            {
                skippedNotApplicable++;
                continue;
            }

            if (TrySearchOTMemory(pk))
                modified++;
            else
                skippedIllegal++;
        }
        return new BulkEditResult(modified, skippedIllegal, skippedInvalid, skippedNotApplicable);
    }

    private static bool IsMissingOTMemory(PKM pk)
    {
        try
        {
            foreach (var chk in new LegalityAnalysis(pk).Results)
            {
                if (chk.Result == LegalityCheckResultCode.MemoryMissingOT)
                    return true;
            }
        }
        catch (Exception)
        {
            // Corrupted data; treat as "nothing to do" rather than letting one entity break the run.
        }
        return false;
    }

    private static bool TrySearchOTMemory(PKM pk)
    {
        var context = Memories.GetContext(EntityContext.Gen8);
        for (byte memory = 1; memory < 100; memory++)
        {
            if (!context.CanObtainMemoryOT(pk.Version, memory))
                continue;

            var applied = memory;
            if (!TryApplyGuarded(pk, p => ApplyOTMemory(p, context, applied)))
                continue;
            // The guard only proves legality; confirm the finding it was raised for is actually gone.
            if (!IsMissingOTMemory(pk))
                return true;
        }
        return false;
    }

    private static void ApplyOTMemory(PKM pk, MemoryContext context, byte memory)
    {
        if (pk is not IMemoryOT ot)
            return;
        ot.OriginalTrainerMemory = memory;
        ot.OriginalTrainerMemoryIntensity = context.GetMinimumIntensity(memory);
        ot.OriginalTrainerMemoryFeeling = MemoryContext8.GetRandomFeeling8(memory);
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
    public static BulkEditResult RegeneratePIDTrackerAndECForAll(IEnumerable<PKM> mons, SaveFile? sav = null)
    {
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
            if (TryRegenerateUnique(pk, taken))
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
        static bool TryRegenerateUnique(PKM pk, HashSet<uint>? taken)
        {
            const int attempts = 8;
            for (int i = 0; i < attempts; i++)
            {
                if (!TryApplyGuarded(pk, RegenerateIdentity))
                    return false; // a legality failure will not improve with more rerolls
                if (taken is null || !taken.Contains(pk.PID))
                    return true;
            }
            return true;
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
}
