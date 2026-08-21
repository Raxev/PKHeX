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
    public readonly record struct Finding(string Description, SlotCache First, SlotCache? Second);

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
            findings.Add(new Finding(Describe(chk.Result), first, second));
        }
        return findings;
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
