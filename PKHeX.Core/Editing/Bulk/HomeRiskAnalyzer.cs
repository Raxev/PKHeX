using System.Collections.Generic;
using System.Linq;

namespace PKHeX.Core;

/// <summary>
/// Flags Pokémon that are structurally more likely to be rejected by Pokémon HOME's server-side upload checks
/// even when PKHeX's own <see cref="LegalityAnalysis"/> reports them as fully legal — HOME performs checks
/// PKHeX has no local data to replicate. See remarks for the two distinct, unrelated risk categories this
/// covers, and which one is actually fixable.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Gift-origin ("signature risk")</b>: the entity's matched encounter is a Mystery Gift. Real gift
/// files carry an authenticity signature issued by Nintendo's servers that PKHeX cannot generate or verify
/// locally — a legally-obtained gift can still be rejected by HOME if that signature doesn't check out (e.g.
/// redistributed via a save editor rather than the original in-game redemption). There is no local fix for
/// this; it's reported purely as an FYI so a HOME rejection on one of these isn't mistaken for a clone/tracker
/// problem.</item>
/// <item><b>Tracker/PID/EC collision ("duplicate risk")</b>: see <see cref="CloneDetector"/> — this one <i>is</i>
/// fixable, via <see cref="BulkQoLEditor.RegenerateTrackerAndECForAll"/>.</item>
/// </list>
/// </remarks>
public static class HomeRiskAnalyzer
{
    /// <summary>
    /// True if <paramref name="pk"/>'s matched encounter is a Mystery Gift — see remarks on <see cref="HomeRiskAnalyzer"/>.
    /// </summary>
    public static bool IsGiftOrigin(PKM pk)
    {
        try
        {
            return new LegalityAnalysis(pk).Info.EncounterMatch is MysteryGift;
        }
        catch
        {
            // Corrupted/edge-case data can make analysis itself throw; treat as "can't tell, assume no" rather
            // than letting one bad entity take down a whole scan.
            return false;
        }
    }

    /// <summary>
    /// Counts how many of <paramref name="mons"/> are gift-origin (see <see cref="IsGiftOrigin"/>).
    /// </summary>
    public static int CountGiftOrigin(IEnumerable<PKM> mons) => mons.Count(IsGiftOrigin);
}
