using System;
using System.Collections.Generic;
using PKHeX.Core.Bulk;

namespace PKHeX.Core;

/// <summary>
/// Scans every Pokémon in a save file for signs of being a duplicate/clone of another entity in the same save
/// — a shared nonzero HOME Tracker GUID, or a shared raw PID/Encryption Constant — either of which is a strong
/// signal that Pokémon HOME's own anti-duplicate detection will reject one of the copies, even though each
/// individually passes PKHeX's ordinary cartridge-legality check. Read-only: reports findings, doesn't fix them
/// (there's no safe generic "fix" for a clone — the duplicate has to be identified and manually resolved).
/// </summary>
/// <remarks>
/// Backed by <see cref="BulkAnalysis"/>'s existing <c>StandardCloneChecker</c>/<c>DuplicatePIDChecker</c>/
/// <c>DuplicateEncryptionChecker</c>/<c>DuplicateGiftChecker</c> analyzers — this just filters their combined
/// output down to the duplicate/clone-specific result codes and resolves the slot indices back to
/// human-readable box/party locations.
/// </remarks>
public static class CloneDetector
{
    /// <param name="Description">Human-readable explanation of what matched.</param>
    /// <param name="First">The entity the finding is about.</param>
    /// <param name="Second">The other entity it collides with, if the finding is a pairwise match.</param>
    /// <param name="Code">The raw result code -- determines which of <paramref name="First"/>/<paramref name="Second"/>
    /// is safe to auto-regenerate (see <see cref="DuplicateSlot"/>); the underlying analyzers don't agree on which
    /// side is "first-seen" vs "newly-detected duplicate" across finding types, so this can't be guessed from
    /// <paramref name="Description"/> alone.</param>
    public readonly record struct Finding(string Description, SlotCache First, SlotCache? Second, LegalityCheckResultCode Code)
    {
        /// <summary>
        /// Whichever of <see cref="First"/>/<see cref="Second"/> is the newly-detected duplicate copy (safe to
        /// regenerate a fresh identity for) rather than the first-seen original (left untouched) -- or null if
        /// this finding isn't a pairwise match with a clear "duplicate side" (e.g. a duplicated Mystery Gift egg).
        /// </summary>
        public SlotCache? DuplicateSlot => Code switch
        {
            // StandardCloneChecker's AddLine index args happen to put the newly-detected duplicate at index1
            // (-> First) for both of its finding types.
            LegalityCheckResultCode.BulkCloneDetectedTracker => First,
            LegalityCheckResultCode.BulkCloneDetectedDetails => First,
            // DuplicatePIDChecker/DuplicateEncryptionChecker instead put the first-seen original at index1
            // (-> First) and the newly-detected duplicate at index2 (-> Second) -- the opposite convention.
            LegalityCheckResultCode.BulkSharingPIDGenerationDifferent or
            LegalityCheckResultCode.BulkSharingPIDGenerationSame or
            LegalityCheckResultCode.BulkSharingPIDEncounterType or
            LegalityCheckResultCode.BulkSharingEncryptionConstantGenerationDifferent or
            LegalityCheckResultCode.BulkSharingEncryptionConstantGenerationSame or
            LegalityCheckResultCode.BulkSharingEncryptionConstantEncounterType => Second,
            _ => null,
        };
    }

    private static readonly HashSet<LegalityCheckResultCode> CloneRelatedCodes =
    [
        LegalityCheckResultCode.BulkCloneDetectedTracker,
        LegalityCheckResultCode.BulkCloneDetectedDetails,
        LegalityCheckResultCode.BulkSharingPIDGenerationDifferent,
        LegalityCheckResultCode.BulkSharingPIDGenerationSame,
        LegalityCheckResultCode.BulkSharingPIDEncounterType,
        LegalityCheckResultCode.BulkSharingEncryptionConstantGenerationDifferent,
        LegalityCheckResultCode.BulkSharingEncryptionConstantGenerationSame,
        LegalityCheckResultCode.BulkSharingEncryptionConstantEncounterType,
        LegalityCheckResultCode.BulkDuplicateMysteryGiftEggReceived,
    ];

    public static IReadOnlyList<Finding> FindLikelyClones(SaveFile sav)
    {
        var analysis = new BulkAnalysis(sav, new BulkAnalysisSettings());
        var findings = new List<Finding>();
        foreach (var (chk, _, index1, index2) in analysis.Parse)
        {
            if (!CloneRelatedCodes.Contains(chk.Result))
                continue;

            var first = analysis.AllData[index1];
            SlotCache? second = index2 == BulkCheckResult.NoIndex ? null : analysis.AllData[index2];
            if (IsMandatedZeroEncryptionPair(chk.Result, first, second))
                continue;
            findings.Add(new Finding(Describe(chk.Result), first, second, chk.Result));
        }
        return findings;
    }

    private static readonly HashSet<LegalityCheckResultCode> EncryptionSharingCodes =
    [
        LegalityCheckResultCode.BulkSharingEncryptionConstantGenerationDifferent,
        LegalityCheckResultCode.BulkSharingEncryptionConstantGenerationSame,
        LegalityCheckResultCode.BulkSharingEncryptionConstantEncounterType,
    ];

    /// <summary>
    /// Suppresses a shared-Encryption-Constant finding when the shared value is zero and both entities are
    /// REQUIRED to have it, which makes the match evidence of nothing at all.
    /// </summary>
    /// <remarks>
    /// HOME gifts redeemed before HOME 3.0.0 were written with a literal zero PID and Encryption Constant, and
    /// <c>WC8.IsMatchExact</c> enforces <c>EncryptionConstant == 0</c> for them. Two such entities therefore
    /// "share" an EC no matter how unrelated they are -- a Melmetal and a Zeraora will collide purely because
    /// neither is allowed to have a nonzero value. Reporting that as "almost certainly cloned" is a false
    /// positive, and the fix it invites is impossible by construction.
    /// <para/>
    /// Verified by attempting a nonzero EC on a throwaway copy rather than by guessing at encounter types: if
    /// the entity stays legal with a different EC, the zero was not mandated and the finding is genuine.
    /// </remarks>
    private static bool IsMandatedZeroEncryptionPair(LegalityCheckResultCode code, SlotCache first, SlotCache? second)
    {
        if (!EncryptionSharingCodes.Contains(code) || second is not { } other)
            return false;
        if (first.Entity.EncryptionConstant != 0 || other.Entity.EncryptionConstant != 0)
            return false;
        return IsZeroEncryptionMandated(first.Entity) && IsZeroEncryptionMandated(other.Entity);
    }

    private static bool IsZeroEncryptionMandated(PKM pk)
    {
        try
        {
            var probe = pk.Clone();
            probe.EncryptionConstant = 0x12345678; // any nonzero value
            probe.RefreshChecksum();
            return !new LegalityAnalysis(probe).Valid;
        }
        catch (Exception)
        {
            return false; // can't tell -> keep reporting it
        }
    }

    private static string Describe(LegalityCheckResultCode code) => code switch
    {
        LegalityCheckResultCode.BulkCloneDetectedTracker =>
            "Duplicate HOME Tracker: this is the exact same HOME upload, copied. HOME will very likely reject (or has already rejected) one of these two.",
        LegalityCheckResultCode.BulkCloneDetectedDetails =>
            "Identical species/PID/IVs/form to another slot — almost certainly a cloned copy.",
        LegalityCheckResultCode.BulkSharingPIDGenerationDifferent or
        LegalityCheckResultCode.BulkSharingPIDGenerationSame or
        LegalityCheckResultCode.BulkSharingPIDEncounterType =>
            "Shares a raw PID with another slot — astronomically unlikely to happen legitimately, almost certainly cloned.",
        LegalityCheckResultCode.BulkSharingEncryptionConstantGenerationDifferent or
        LegalityCheckResultCode.BulkSharingEncryptionConstantGenerationSame or
        LegalityCheckResultCode.BulkSharingEncryptionConstantEncounterType =>
            "Shares a raw Encryption Constant with another slot — astronomically unlikely to happen legitimately, almost certainly cloned.",
        LegalityCheckResultCode.BulkDuplicateMysteryGiftEggReceived =>
            "The same Mystery Gift egg was redeemed more than once.",
        _ => code.ToString(),
    };
}
