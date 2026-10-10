namespace ClickItMax
{
    public partial class ClickItMaxSettings
    {
        internal IReadOnlyList<string> GetUltimatumModifierPriority()
            => UltimatumSettingsRuntimeService.GetModifierPriority(this);

        internal IReadOnlyCollection<string> GetUltimatumTakeRewardModifierNames()
            => UltimatumSettingsRuntimeService.GetTakeRewardModifierNames(this);

        internal bool ShouldTakeRewardForGruelingGauntletModifier(string? modifierName)
            => UltimatumSettingsRuntimeService.ShouldTakeRewardForGruelingGauntletModifier(this, modifierName);
    }
}