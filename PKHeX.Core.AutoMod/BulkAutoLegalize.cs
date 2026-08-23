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

    /// <param name="forceShiny">
    /// Overrides the shiny type the <see cref="RegenTemplate"/> would otherwise inherit from the entity
    /// (<c>RegenSet</c>'s constructor maps an existing Star shiny to <see cref="Shiny.AlwaysStar"/>, which would
    /// keep reproducing Star). Null leaves the inherited value alone.
    /// </param>
    private static bool TryLegalize(PKM pk, SaveFile sav, Shiny? forceShiny)
    {
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
