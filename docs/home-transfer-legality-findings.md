# HOME Transfer vs. PKHeX Legality: Findings

Investigation into why non-Mystery-Gift Pokemon generated in a Pokemon Violet (Gen9 / PK9) save pass every
PKHeX legality check but are rejected by Pokemon HOME.

Date: 2026-08-23. Branch: `feature/bulk-qol-editor`.

## Status at a glance

### Fixed (implemented, verified by smoke test)

- **De-cloner fabricated HOME Trackers.** `BulkQoLEditor.RegeneratePIDTrackerAndECForAll` now clears an
  existing Tracker to zero instead of inventing a random one.
- **Max size left an impossible Scale/Mark pair.** `BulkQoLEditor.SetMaxSizeForAll` now sets
  `RibbonMarkJumbo` alongside `Scale = 255`.
- **Untraded HT cleanup silently reverted.** `BulkQoLEditor.FixTrashAndMemoryForAll` now routes PK9 through
  `PK9.FixMemories()` and clears `HandlingTrainerLanguage` for untraded non-gift entities.
- **Doc comment bug.** `NatureEVPreset` described Jolly/Timid with the wrong boosted stat.

### Fixed in a later pass (2026-08-24), verified by smoke test

- **`RegenSet` `.MetDate` silently failed.** Confirmed empirically (wanted 2023-03-15, got today's date). Now
  formats as `yyyyMMdd` invariant. `RegenSet.cs:47`.
- **HOME Transfer Pre-Check built.** `HomeTransferPreCheck.Scan` + "Check HOME Transfer Risk" button.

### Corrected: two agent claims that did NOT reproduce

- **`ObedienceLevel` drift — NOT reproduced.** Legalized entities came back with `ObedienceLevel == MetLevel`
  in every case tested, including after `BulkAutoLegalize` regeneration. No speculative fix was written.
  The pre-check reports a mismatch if one ever occurs, rather than blindly rewriting the field.
- **`TeraTypeOverride` non-None — reproduced, but it is NOT a defect.** Requesting a different Tera type does
  make ALM write an override (e.g. Garchomp `TeraOrig=Ground`, `Override=9/Fire`), but that is exactly what a
  Tera Shard does in-game and it is legitimately legal. Auto-resetting it would destroy a deliberate user
  choice. The pre-check instead flags only *out-of-range* override values, which PKHeX genuinely never checks.

### Still open (proposed, not built)

- TM record flags are never required to be *present* for a TM-learned move (mitigated: `LearnSource9SV`
  catches it via a different path, so the effect is over-strict rejection rather than bad output).
- `Scale` for `EncounterOutbreak9` is not range-verified.

### Unknown / out of scope

- HOME's actual server-side acceptance rules are undocumented publicly. Everything below is inference from
  what HOME *persists* plus what its own importer *normalizes*.

---

## Key structural insight: PKHeX ships HOME's own format

`PKHeX.Core/PKM/HOME/` is a complete read/write implementation of HOME's `.pkh` format
(`PKH.cs`, `HomeCrypto.cs`, `GameDataCore.cs`, `GameDataPK9.cs`).

This matters because the fields HOME chooses to *persist* are the fields HOME can *validate*. For Gen9,
`GameDataPK9.cs` stores: `Scale` (0x00), `TeraTypeOriginal` (0x19), `TeraTypeOverride` (0x1A),
`RecordFlagsBase` (0x20, 0x19 bytes of TM flags), `Obedience_Level` (0x39), and `RecordFlagsDLC` (0x3D).

There is no `HomeConverter` validation gate. PKHeX can produce a `.pkh` but does not model HOME's
acceptance checks. `TransferVerifier.cs:170` says so outright: *"Can't validate the actual values (we aren't
the server), so we can only check against zero."*

## The severity model hides findings

`PKHeX.Core/Legality/Structures/CheckResult.cs:13`:

```csharp
public bool Valid => Judgement != Severity.Invalid;
```

Only `Severity.Invalid` turns the verdict red. `Severity.Fishy` still reports as Legal. This means
`BulkQoLEditor.TryApplyGuarded` will *keep* an edit that lands in Fishy territory.

Fishy codes reachable for PK9 include `ZeroHeightWeight` (`HOMETransferSettings.cs:12`, default Fishy),
`TransferHandlerMismatchLanguage` (`HistoryVerifier.cs:152`), `Effort2Remaining` / `EffortAllEqual`
(`EffortValueVerifier.cs:50-54`), and several trash-byte and TID/SID checks.

**Practical consequence:** reading the boolean verdict is not the same as reading the report.

---

## Findings

### 1. FIXED. Fabricated HOME Trackers

`BulkQoLEditor.cs` previously did:

```csharp
if (pk is IHomeTrack { Tracker: not 0 } home)
    home.Tracker = GetRandomNonZeroTracker();
```

A Tracker is a GUID issued by HOME's servers. PKHeX's own source tells you not to do this
(`PKHeX.Core/Legality/Verifiers/TransferVerifier.cs:174-176`):

> To the reader: It seems like the best course of action for setting a tracker is:
> - Transfer a 0-Tracker pk to HOME to get assigned a valid Tracker via the game it originated from.
> - **Don't make one up.**

`TryApplyGuarded` structurally cannot catch this: `VerifyHOMETracker` only checks zero vs. nonzero, because
PKHeX has no server data to compare against.

Aggravating factor: the method was gated on `Tracker: not 0`, so it fired *only* on Pokemon that had already
been through HOME (exactly the population where HOME holds an authoritative record).

**Fix:** clear the Tracker to zero. Zero means "never uploaded", so HOME assigns a fresh one on next transfer.
Zeroing is self-guarding: where a Tracker is genuinely required (GO transfers, HOME gifts, cross-generation
transfers, see `HomeTrackerUtil.IsRequired`), clearing produces `TransferTrackerMissing` and the guard reverts.

### 2. FIXED. Scale 255 without the Jumbo Mark

`RibbonVerifierMark9.cs:20-28` checks Jumbo one-directionally:

```csharp
if (r.RibbonMarkJumbo && !MarkRules.IsMarkAllowedJumbo(args.History, args.Entity))
```

That is `mark && !allowed`. The inverse (`Scale == 255 && !RibbonMarkJumbo`) is never flagged, unlike
Alpha/Mightiest/Titan which use bidirectional `!=` comparisons.

Gen9 awards the Jumbo Mark automatically at capture when Scale is maxed, so "max Scale, no Jumbo" is a state
the game never produces. `SetMaxSizeForAll` was producing exactly that.

**Fix:** set `RibbonMarkJumbo = true` alongside `Scale = 255`. The guard reverts if the encounter cannot hold
marks (e.g. `MarkRules.IsEncounterMarkLost` for Nincada to Shedinja).

Related confirmations: `Scale` is authoritative for SV; `HeightScalar` must equal `Scale` only once
HOME-tracked (`MiscScaleVerifier.cs:93`); `WeightScalar` is never compared to `Scale` at all; PK9 does **not**
implement `IScaledSizeValue`, so the `ResetHeight()/ResetWeight()` branch is dead code for PK9.

### 3. FIXED. Untraded HT cleanup silently reverted

Two separate causes, both producing "reported as fixed, nothing changed":

- `ClearMemoriesHT()` only zeroes the four memory fields. PK9's canonical `FixMemories()` (`PK9.cs:640-647`)
  also clears `HandlingTrainerGender`, `HandlingTrainerFriendship`, and the HT name trash. Leaving friendship
  populated trips `MemoryStatFriendshipHT0` as Invalid, reverting the whole edit.
- `MemoryVerifier.cs:112-113` requires `HandlingTrainerLanguage == 0` for an untraded entity. `FixMemories()`
  deliberately will not clear it (Gen9 gift encounters with `GiftWithLanguage` legitimately keep one), and our
  own language fixer early-returned on `IsUntraded`. So nothing cleared it.

**Fix:** route PK9 through `pk9.FixMemories()`, and resolve the matched encounter *before* mutating to decide
whether the entity is a `GiftWithLanguage` case (keep, set to `pk.Language`) or not (clear to 0).

---

## Confirmed gaps not yet addressed (proposed, not built)

### `TeraTypeOverride` unvalidated for ordinary species

`MiscVerifierPK9.VerifyTeraType` (`MiscVerifierPK9.cs:79-101`) special-cases eggs, Terapagos, and Ogerpon.
For every other species it falls to:

```csharp
else { if (!TeraTypeUtil.IsValid((byte)pk9.TeraTypeOriginal)) ... }
```

Only `TeraTypeOriginal` is range-checked. **`TeraTypeOverride` is never validated.** Meanwhile the vendored
AutoLegalityMod calls `SetTeraType` unguarded at `APILegality.cs:997-998` and `SimpleEdits.cs:592-593`, which
writes an arbitrary override whenever the requested Tera type differs from the original. Result: generated SV
Pokemon routinely carry `TeraTypeOverride != 19`, i.e. "a Tera Shard was used", with no in-game justification.
HOME persists this field.

### `ObedienceLevel` never recomputed

AutoLegalityMod only assigns `ObedienceLevel` in the egg path (`APILegality.cs:1760`). Meanwhile
`RegenSet.cs:47` force-restores the original entity's `MetLevel` as a batch instruction *after* generation,
without updating `ObedienceLevel`.

PKHeX's check (`MiscVerifierHelpers.cs:23-32`) is exact for untraded (`ObedienceLevel == MetLevel`) but only a
loose band for traded (`MetLevel <= ObedienceLevel <= CurrentLevel`). AutoLegalityMod sets
`CurrentHandler = 1` (`SimpleEdits.cs:682`), so generated Pokemon land in the loose case. HOME's own importer
sets `Obedience_Level = MetLevel` (`GameDataPK9.cs:208`).

### TM record flags never required to be present

`MiscVerifierPK9.VerifyTechRecordFlags` (`MiscVerifierPK9.cs:155-174`):

```csharp
if (!pk.GetMoveRecordFlag(i))
    continue;               // unset flags are skipped entirely
```

Only the false-positive direction is checked (a flag set for a TM the species cannot learn). The
`MoveTechRecordFlagMissing_0` code name is misleading.

Mitigating: `LearnSource9SV.GetIsTM` (`LearnSource9SV.cs:101-122`) *does* require the flag under the normal
`LearnOption.Current` path, so a TM move without its flag fails move validation. Net effect is not a bad
output but an over-strict one: `SetLegalMovesForAll` will over-report `skippedIllegal` for SV Pokemon whose
only legal moveset needs a TM, because `SetMoveset` never sets record flags
(`MoveSetApplicator.cs:16-21`; `SetRecordFlags` lives in a separate, never-invoked applicator).

Additionally the vendored AutoLegalityMod sets flags from the **final species' `Permit` only**
(`SimpleEdits.cs:829-856`), using the single-argument overload. Current PKHeX uses the evolution-chain-aware
overload (`CommonEdits.cs:258-259`). Pre-evolution-exclusive TMs (the Zorua/Zoroark Encore case that
`MiscVerifierPK9.CanPreEvoLearn` exists to permit) never get their flag set.

### `EncounterOutbreak9` Scale not range-verified

`EncounterOutbreak9` forces `Scale` into `[ScaleMin, ScaleMax]` (`EncounterOutbreak9.cs:31-33, 130-132`) but
its `IsMatchExact` never verifies Scale. AutoLegalityMod's `isFixedScale` switch
(`SimpleEdits.cs:435-443`) does not cover `EncounterOutbreak9`, so it overwrites Scale with a PID-derived
value that PKHeX will not flag.

### `RegenSet` `.MetDate` silently fails

`RegenSet.cs:24-52` emits `$".MetDate={pk.MetDate}"`. `pk.MetDate` is `DateOnly?`, so this interpolates a
culture-dependent short date (`8/19/2026`). The batch handler parses with
`DateOnly.ParseExact(val, "yyyyMMdd", CultureInfo.InvariantCulture)` (`BatchMods.cs:58,62`) and throws
`FormatException`, which is swallowed at `BatchEditingBase.cs:200-207`.

Net effect: `.MetLocation` and `.MetLevel` apply while `.MetDate` silently does not, so a regenerated Pokemon
can carry the original's Met location/level paired with the encounter's Met date.

### `FormArgument` computed with an empty `EvolutionHistory`

`ShowdownEdits.cs:153` passes `new EvolutionHistory()` to `SetSuggestedFormArgument`. Its Gen9 logic is
`history.HasVisitedGen9`, which is always false for a default-constructed history. Hoopa, Farfetch'd-1, and
Sirfetch'd therefore get a non-zero FormArgument in an SV save where the game leaves it at 0
(`IFormArgument.cs:102-115`).

### Confirmed Gen9-missing branches in vendored AutoLegalityMod

- `ApplyBattleVersion`: `if (pk is PK8 { SWSH: false } pk8)` (`APILegality.cs:808`), no PK9 branch.
- `FixEdgeCases` (`APILegality.cs:1401-1427`): no Gen9 cases at all.
- `wasMetLost` switch (`APILegality.cs:1370-1376`): no Gen8/Gen9 arm.

### Confirmed clean

- AutoLegalityMod never writes `Tracker` anywhere. Correct.
- PK9 HT/memory handling in AutoLegalityMod has a correct Gen9 branch.
- `EntityConverter.ConvertToType` returns the same instance for PK9 to PK9, and the raw `Data.CopyTo`
  in `BulkAutoLegalize`/`IVOptimizer` is a full byte-identical replacement. No field loss.
- No hardcoded TM index tables in the vendored code; index mapping comes from live `PKHeX.Core`.
- `SetRandomEC` handles Wurmple, Dunsparce/Dudunsparce, and Tandemaus/Maushold correctly for Gen9.
- `SetNatureEVPresetForAll` mint behavior (`Nature != StatAlignment`) is explicitly legal
  (`MiscVerifierHelpers.cs:10-21`). EV spreads total 504, under the 510 cap and dodging the 508 Fishy check.
- Tera raid IVs are seed-verified (`Encounter9RNG.cs:139-186`), so `SetMaxIVsForAll` correctly reverts on them.

---

## External research summary

No public authoritative documentation of HOME's server-side validation exists. Notable sourced findings:

- **HOME tracker invalidation is a documented mechanism.** ProjectPokemon's staff-maintained doc publishes an
  immutable-value list: Species, PID, Gender, Nature, Ability, Language, Origin Game, Met Location, Ball, Met
  Date, Met Level, Egg Met Location/Date, IVs, Original Tera Type, Gigantamax Factor, Relearn Moves, Height,
  Weight, Scale, Ribbons, OT Name, OT Gender, TID, SID, Encryption Constant. Changing any of these on a
  Pokemon that already has a tracker invalidates it, and HOME then refuses it, typically with error 10015.
  <https://projectpokemon.org/home/docs/home_165/relevance-of-home-tracker-home-v200-v300-and-beyond-r154/>
- **Reproduced upstream.** PKHeX issue #4185: changing *only the met date* on an SV Pokemon made it
  permanently undepositable while PKHeX still reported Legal. Closed `invalid` (HOME's business).
  <https://github.com/kwsch/PKHeX/issues/4185>
- **Error codes are not one phenomenon.** Nintendo's own support page groups 999, 992, 8807, and 2-ALZTA-0005
  as connection/session errors with no legality explanation, and attributes 10015 to a specific May 2022 bug
  since patched. Only 10015 is consistently community-associated with legality rejection. No HOME-specific
  source could be found for 10005 or 10000.
  <https://en-americas-support.nintendo.com/app/answers/detail/a_id/48849/>
- **No HOME legality checker exists** in PKHeX or the community. The closest is the `EnableHOMETrackerCheck`
  setting (promotes missing-tracker from Fishy to Invalid) and `BallContextHOME.cs` (models HOME 3.0.0 ball
  inheritance for SV breeding).
- **Whether HOME re-derives encounters server-side is unknown.** Community opinion is split and
  under-evidenced. No reverse-engineering writeup of HOME's responses could be located.
- **Source-quality warning:** `genpkm.com/blog/*` surfaces prominently in searches, cites nothing, and
  contradicts itself across pages. Not evidence.

## Implication for the immutable-value list

Nearly every Bulk QoL operation touches at least one immutable value: `SetBallForAll` (Ball),
`SetMetLocationForAll` (Met Location), `SetMaxIVsForAll` / `IVOptimizer` (IVs), `SetMaxSizeForAll`
(Height/Weight/Scale), `SetShinyForAll` (PID), `SetLegalMovesForAll` (Relearn Moves),
`RegeneratePIDTrackerAndECForAll` (PID + EC), and `BulkAutoLegalize` (effectively everything).

**Any of these applied to a Pokemon with a nonzero Tracker will invalidate that Tracker**, while PKHeX
continues to report Legal. This is the best-sourced explanation for the reported symptom and is not currently
guarded against.

Forensic markers for already-damaged Pokemon: a nonzero `Tracker` that no longer matches HOME's record, and
`Scale == 255 && !RibbonMarkJumbo` (pre-fix output of `SetMaxSizeForAll`).


---

## Addendum (2026-08-24): what the pre-check surfaces, and two new confirmations

`PKHeX.Core/Editing/Bulk/HomeTransferPreCheck.cs`, surfaced via "Check HOME Transfer Risk" in Bulk QoL.
Read-only. Groups findings by category, since a whole save produces the same finding hundreds of times.

Detects:

- **Already HOME-registered** (nonzero Tracker). Editing any immutable value invalidates the Tracker and HOME
  refuses the upload while PKHeX still says Legal. This is the best-sourced cause of the reported symptom.
- **Fishy-severity checks**, which never turn the verdict red (`CheckResult.cs:13`).
- **`HeightScalar != Scale`** (Gen9), **max/min Scale without the Jumbo/Mini Mark**, **out-of-range
  `TeraTypeOverride`**, and **`ObedienceLevel != MetLevel`** on traded entities.

### New confirmation 1: every legalizer-generated entity carries two hidden Fishy checks

A freshly generated, "Legal" Garchomp reports `EffortEXPIncreased` and `LevelEXPThreshold` as Fishy. Cause:
EXP sits exactly on the level boundary (270000 for level 60) with all EVs at 0 — a "generated, never played"
fingerprint. Legal, invisible in the headline verdict, and present on essentially every legalized Pokemon.

### New confirmation 2: the Scale rule is latent until HOME touches the entity

`MiscScaleVerifier.IsHeightScaleMatchRequired` is `pk is IHomeTrack { HasTracker: true }`, so
`HeightScalar != Scale` is entirely unflagged before a HOME visit. The legalizer produces mismatched values.
Verified: assigning a Tracker to such an entity immediately turns it Invalid with `StatIncorrectScaleValue_0`.

Since HOME's own importer copies `Scale` over `HeightScalar` (`GameDataPK9.cs:135`), an entity that goes to
HOME and returns comes back with the rule active. This is a concrete "looks fine now, illegal after a HOME
round trip" trap, and the pre-check now flags it up front.
