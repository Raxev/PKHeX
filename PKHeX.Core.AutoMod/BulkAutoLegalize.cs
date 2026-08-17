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

    public static Result LegalizeAll(IEnumerable<PKM> mons, SaveFile sav)
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

            if (TryLegalize(pk, sav))
                modified++;
            else
                failed++;
        }
        return new Result(modified, failed, alreadyLegal, invalid);
    }

    private static bool TryLegalize(PKM pk, SaveFile sav)
    {
        try
        {
            var regen = new RegenTemplate(pk);
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
}
