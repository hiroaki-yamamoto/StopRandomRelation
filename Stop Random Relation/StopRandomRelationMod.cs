using HarmonyLib;
using UnityEngine;
using Verse;

namespace Stop_Random_Relation;

/// <summary>
/// Entry point and Mod Settings UI controller for Stop Random Relation.
/// </summary>
public class StopRandomRelationMod : Mod
{
  private const string HarmonyId = "hiroaki.stoprandomrelation";
  private const float ScrollBarWidthMargin = 20f;
  private const float ResetButtonWidth = 180f;
  private const float ResetButtonHeight = 32f;

  private readonly StopRandomRelationSettings _settings;
  private Vector2 _scrollPosition;
  private float _cachedContentHeight = 620f;
  private string? _offMapSweepIntervalBuffer;
  private string? _worldPawnThresholdBuffer;

  /// <summary>
  /// Gets the active mod instance initialized by RimWorld at startup.
  /// </summary>
  public static StopRandomRelationMod? Instance { get; private set; }

  /// <summary>
  /// Initializes the mod, loads persisted settings, and applies all Harmony patches.
  /// </summary>
  /// <param name="content">The RimWorld mod content pack descriptor.</param>
  public StopRandomRelationMod(ModContentPack content)
    : base(content)
  {
    Instance = this;
    _settings = GetSettings<StopRandomRelationSettings>();
    _settings.RefreshSnapshot();

    var harmony = new Harmony(HarmonyId);
    harmony.PatchAll(typeof(StopRandomRelationMod).Assembly);
  }

  /// <summary>
  /// Returns an immutable, thread-safe snapshot of the active mod settings.
  /// </summary>
  /// <returns>The current <see cref="RelationCleanupOptions"/>.</returns>
  public static RelationCleanupOptions GetOptions()
  {
    return Instance?._settings.GetOptionsSnapshot() ?? RelationCleanupOptions.Default;
  }

  /// <summary>
  /// Returns the display title of the mod in RimWorld's Mod Settings menu.
  /// </summary>
  /// <returns>The localized settings category name.</returns>
  public override string SettingsCategory()
  {
    return "SRR_SettingsCategory".Translate();
  }

  /// <summary>
  /// Renders the vanilla-styled settings window using <see cref="Listing_Standard"/>.
  /// All changes update the in-memory configuration snapshot immediately.
  /// </summary>
  /// <param name="inRect">Available drawing rectangle provided by RimWorld.</param>
  public override void DoSettingsWindowContents(Rect inRect)
  {
    Rect viewRect = new(
      0f,
      0f,
      inRect.width - ScrollBarWidthMargin,
      Mathf.Max(_cachedContentHeight, inRect.height));

    Widgets.BeginScrollView(inRect, ref _scrollPosition, viewRect);

    var listing = new Listing_Standard();
    listing.Begin(viewRect);

    DrawCheckbox(listing, "SRR_ModEnabled", ref _settings.modEnabled);
    listing.GapLine();

    DrawSectionHeader(listing, "SRR_Section_ColonistRelations");
    DrawCheckbox(
      listing,
      "SRR_CleanColonistOnPermDeath",
      ref _settings.cleanColonistRelationsOnPermanentDeath);
    listing.Gap();

    DrawSectionHeader(listing, "SRR_Section_VisitorRaiderRelations");
    DrawCheckbox(
      listing,
      "SRR_CleanColonistToWorld",
      ref _settings.cleanColonistToWorldRelationsOnExitOrDeath);
    listing.Gap();

    DrawSectionHeader(listing, "SRR_Section_WorldPawnRelations");
    DrawCheckbox(
      listing,
      "SRR_CleanWorldToWorld",
      ref _settings.cleanWorldToWorldRelationsOnExitOrDeath);
    DrawCheckbox(
      listing,
      "SRR_CleanOffMapWorldPawns",
      ref _settings.cleanOffMapWorldPawnRelations);
    DrawIntField(
      listing,
      "SRR_OffMapSweepIntervalTicks",
      ref _settings.offMapSweepIntervalTicks,
      ref _offMapSweepIntervalBuffer,
      0,
      RelationCleanupOptions.MaxOffMapSweepIntervalTicks);
    DrawCheckbox(
      listing,
      "SRR_DeleteUnprotectedOffMapWorldPawns",
      ref _settings.deleteUnprotectedOffMapWorldPawns);
    DrawIntField(
      listing,
      "SRR_UnprotectedOffMapWorldPawnThreshold",
      ref _settings.unprotectedOffMapWorldPawnThreshold,
      ref _worldPawnThresholdBuffer,
      RelationCleanupOptions.MinWorldPawnThreshold,
      RelationCleanupOptions.MaxWorldPawnThreshold);
    listing.Gap();

    DrawSectionHeader(listing, "SRR_Section_ProtectionRules");
    DrawCheckbox(
      listing,
      "SRR_ProtectFactionLeaders",
      ref _settings.protectLivingFactionLeaders);
    DrawCheckbox(
      listing,
      "SRR_IncludeNonBlood",
      ref _settings.includeNonBloodRelations);
    listing.Gap();

    DrawSectionHeader(listing, "SRR_Section_Debugging");
    DrawCheckbox(listing, "SRR_VerboseLogging", ref _settings.verboseLogging);
    listing.GapLine();

    Rect resetRect = listing.GetRect(ResetButtonHeight);
    resetRect.width = ResetButtonWidth;
    if (Widgets.ButtonText(resetRect, "SRR_ResetDefaults".Translate()))
    {
      _settings.ResetToDefaults();
      SyncNumericBuffers();
    }

    _cachedContentHeight = listing.CurHeight + 16f;
    listing.End();
    Widgets.EndScrollView();

    _settings.RefreshSnapshot();
    base.DoSettingsWindowContents(inRect);
  }

  private static void DrawCheckbox(
    Listing_Standard listing,
    string baseKey,
    ref bool value)
  {
    listing.CheckboxLabeled(
      (baseKey + "_Label").Translate(),
      ref value,
      (baseKey + "_Tip").Translate());
  }

  private static void DrawIntField(
    Listing_Standard listing,
    string baseKey,
    ref int value,
    ref string? buffer,
    int min,
    int max)
  {
    buffer ??= value.ToString();

    Rect rect = listing.GetRect(Text.LineHeight);
    if (Mouse.IsOver(rect))
    {
      Widgets.DrawHighlight(rect);
    }

    TooltipHandler.TipRegion(rect, (baseKey + "_Tip").Translate());

    Rect labelRect = rect.LeftPart(0.72f);
    Rect fieldRect = rect.RightPart(0.26f);

    TextAnchor previousAnchor = Text.Anchor;
    Text.Anchor = TextAnchor.MiddleLeft;
    Widgets.Label(labelRect, (baseKey + "_Label").Translate());
    Text.Anchor = previousAnchor;

    Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, min, max);
    listing.Gap(listing.verticalSpacing);
  }

  private void SyncNumericBuffers()
  {
    _offMapSweepIntervalBuffer = _settings.offMapSweepIntervalTicks.ToString();
    _worldPawnThresholdBuffer = _settings.unprotectedOffMapWorldPawnThreshold.ToString();
  }

  private static void DrawSectionHeader(Listing_Standard listing, string titleKey)
  {
    GameFont previousFont = Text.Font;
    Text.Font = GameFont.Small;
    GUI.color = ColoredText.TipSectionTitleColor;
    listing.Label(titleKey.Translate());
    GUI.color = Color.white;
    Text.Font = previousFont;
  }
}
