using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PKHeX.Core.AutoMod;

/// <summary>
/// Finds the best legal IV spread for a Pokémon, prioritizing whichever stats actually benefit it most
/// (base stat, Nature, and existing EV investment all factored in via the real stat formula).
/// </summary>
/// <remarks>
/// Most encounters allow any IVs 0-31 on some/all stats (a flawless-count minimum, or a partially-fixed set),
/// where "best" is trivially "max whatever's free" — <see cref="TryOptimize"/> tries that first, guarded, and
/// stops there if it works.
/// <para/>
/// Some encounters (Tera raids, raid dens, certain overworld/static gifts, ...) correlate the PID and all 6 IVs
/// to a single RNG seed, so IVs aren't independently choosable at all — the only way to influence them is to
/// land on a different seed. For those, this regenerates the entity via the AutoMod legalizer many times with
/// IVs left completely unconstrained (each attempt naturally rolls a different seed/IV spread), scores every
/// legal result, and keeps the best one found. This is a best-of-N heuristic, not a guaranteed-optimal search.
/// </remarks>
public static class IVOptimizer
{
    private const int DefaultSearchAttempts = 2000;

    /// <param name="Improved">Entities that ended up with a better (or newly legal) IV spread.</param>
    /// <param name="AlreadyOptimal">Entities that were already at (or the search couldn't beat) their best legal IV spread.</param>
    /// <param name="Failed">Entities where no legal IV spread could be found at all within the search budget.</param>
    /// <param name="SkippedInvalid">Entities that were skipped entirely (empty slot).</param>
    public readonly record struct Result(int Improved, int AlreadyOptimal, int Failed, int SkippedInvalid)
    {
        public int Total => Improved + AlreadyOptimal + Failed + SkippedInvalid;
    }

    public static Result OptimizeAll(IEnumerable<PKM> mons, SaveFile sav, int searchAttempts = DefaultSearchAttempts)
    {
        int improved = 0, alreadyOptimal = 0, failed = 0, invalid = 0;
        foreach (var pk in mons)
        {
            if (pk.Species == 0 || pk.Species > sav.MaxSpeciesID)
            {
                invalid++;
                continue;
            }

            switch (TryOptimizeCore(pk, sav, searchAttempts))
            {
                case OptimizeResult.Improved: improved++; break;
                case OptimizeResult.AlreadyOptimal: alreadyOptimal++; break;
                case OptimizeResult.Failed: failed++; break;
            }
        }
        return new Result(improved, alreadyOptimal, failed, invalid);
    }

    private enum OptimizeResult { Improved, AlreadyOptimal, Failed }

    /// <summary>
    /// Tries to improve <paramref name="pk"/>'s IVs in place. Returns whether it was changed.
    /// </summary>
    public static bool TryOptimize(PKM pk, SaveFile sav, int searchAttempts = DefaultSearchAttempts) =>
        TryOptimizeCore(pk, sav, searchAttempts) == OptimizeResult.Improved;

    private static OptimizeResult TryOptimizeCore(PKM pk, SaveFile sav, int searchAttempts)
    {
        try
        {
            var originalLegal = new LegalityAnalysis(pk).Valid;
            var originalScore = originalLegal ? Score(pk) : double.MinValue;

            // Cheap path first: most encounters allow any IVs 0-31 on some/all stats (a flawless-count minimum
            // is a floor, not a ceiling; a partially-fixed set only constrains the fixed slots). Maxing everything
            // is always the best legal answer for those, and BulkQoLEditor.TryApplyGuarded reverts harmlessly if not.
            if (BulkQoLEditor.TryApplyGuarded(pk, p => p.SetIVs(MaxIVs)))
                return (!originalLegal || Score(pk) > originalScore) ? OptimizeResult.Improved : OptimizeResult.AlreadyOptimal;

            // The cheap path failed: this is very likely a PID/IV-correlated encounter (Tera raid, raid den,
            // certain overworld/static gifts, ...) where IVs are a byproduct of whichever seed the PID came from,
            // not independently settable. Search for a better seed instead.
            return TryOptimizeViaSearch(pk, sav, searchAttempts, originalLegal, originalScore);
        }
        catch (Exception)
        {
            // The legalizer is a large, format-spanning engine (Gen1-9+) with edge cases it doesn't handle
            // cleanly for every entity (e.g. corrupted/out-of-range data). One bad entity in a bulk run
            // should count as a failure, not take down the whole run.
            return OptimizeResult.Failed;
        }
    }

    private static readonly int[] MaxIVs = [31, 31, 31, 31, 31, 31];

    // Overall wall-clock budget for the whole search, regardless of remaining attempt count -- some entities
    // (e.g. an origin with very few or no matching legal encounters) can make every attempt slow, and 2000
    // slow attempts back to back would otherwise take far too long. Individual attempts are additionally
    // capped via a much shorter per-attempt timeout below, so one pathological attempt can't eat the budget.
    private static readonly TimeSpan SearchBudget = TimeSpan.FromSeconds(20);
    private const int PerAttemptTimeoutSeconds = 2;

    private static OptimizeResult TryOptimizeViaSearch(PKM pk, SaveFile sav, int searchAttempts, bool originalLegal, double originalScore)
    {
        // Build once: same species/form/level/ability/moves/nature as pk, but with every IV slot marked
        // unconstrained (EncounterCriteria.RandomIV == -1), so each regeneration attempt below is free to roll
        // whatever a fresh seed produces instead of hunting for pk's own (possibly illegal) current IVs.
        var regen = new RegenTemplate(pk);
        for (int i = 0; i < regen.IVs.Length; i++)
            regen.IVs[i] = -1;

        PKM? best = null;
        var bestScore = double.MinValue;

        var originalTimeout = APILegality.Timeout;
        var sw = Stopwatch.StartNew();
        try
        {
            APILegality.Timeout = PerAttemptTimeoutSeconds;
            for (int attempt = 0; attempt < searchAttempts && sw.Elapsed < SearchBudget; attempt++)
            {
                var blank = EntityBlank.GetBlank(sav);
                var async = sav.GetLegalFromTemplateTimeout(blank, regen);
                var (result, status) = (async.Created, async.Status);
                if (status != LegalizationResult.Regenerated)
                    continue;

                // Convert back to the entity's original storage format so it stays comparable/committable.
                var converted = EntityConverter.ConvertToType(result, pk.GetType(), out _);
                if (converted is null || converted.Data.Length != pk.Data.Length)
                    continue;
                if (!new LegalityAnalysis(converted).Valid)
                    continue;

                var score = Score(converted);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = converted;
            }
        }
        finally
        {
            APILegality.Timeout = originalTimeout;
        }

        if (best is null)
            return OptimizeResult.Failed;
        if (originalLegal && bestScore <= originalScore)
            return OptimizeResult.AlreadyOptimal;

        // best's own bytes were already verified legal above; re-verify once more after copying into pk's
        // buffer specifically (not just the standalone converted clone), and revert if that somehow differs.
        Span<byte> backup = stackalloc byte[pk.Data.Length];
        pk.Data.CopyTo(backup);
        best.Data.CopyTo(pk.Data);
        pk.RefreshChecksum();
        if (new LegalityAnalysis(pk).Valid)
            return OptimizeResult.Improved;

        backup.CopyTo(pk.Data);
        pk.RefreshChecksum();
        return originalLegal ? OptimizeResult.AlreadyOptimal : OptimizeResult.Failed;
    }

    /// <summary>
    /// Weighted sum of (base stat x Nature multiplier x EV-investment multiplier x IV) across all six stats
    /// — higher is better.
    /// </summary>
    /// <remarks>
    /// A plain sum of real battle stats can't actually discriminate between two candidates that only differ
    /// in *which* of two equal-base stats got the higher IV (e.g. Deoxys's 150 Atk/SpA/Spe): redistributing
    /// the same total IV between two stats of equal base+EV changes nothing about their sum, since addition
    /// doesn't care which slot the points sit in. Nature already breaks that tie by rewarding one stat over
    /// the other asymmetrically; this does the same for EV investment -- a stat with 252 EVs already sunk into
    /// it gets up to 2x the weight of one with none, so the search prefers landing IVs on stats the existing
    /// build actually cares about instead of splitting the difference.
    /// </remarks>
    private static double Score(PKM pk)
    {
        var pi = pk.PersonalInfo;
        Span<int> ivs = stackalloc int[6];
        pk.GetIVs(ivs); // HP, ATK, DEF, SPE, SPA, SPD
        Span<int> evs = stackalloc int[6];
        pk.GetEVs(evs); // same order

        var (up, dn) = pk.StatAlignment.GetNatureModification(); // indices into ATK,DEF,SPE,SPA,SPD (0-4)

        double score = 0;
        for (int i = 0; i < 6; i++)
        {
            var natureMult = 1.0;
            if (i > 0) // HP is never boosted/hindered by Nature
            {
                var natureIndex = i - 1;
                if (natureIndex == up)
                    natureMult = 1.1;
                else if (natureIndex == dn)
                    natureMult = 0.9;
            }
            var evMult = 1.0 + evs[i] / 252.0; // 1x (no investment) up to 2x (fully invested)
            score += pi.GetBaseStatValue(i) * natureMult * evMult * ivs[i];
        }
        return score;
    }
}
