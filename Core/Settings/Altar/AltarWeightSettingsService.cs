namespace ClickItMax.Core.Settings.Altar
{
    internal static class AltarWeightSettingsService
    {
        internal static void InitializeDefaultWeights(ClickItMaxSettings settings)
        {
            // A hand-edited/corrupted config can deserialize ModTiers/ModAlerts as null; never NRE at startup.
            settings.ModTiers ??= new(StringComparer.OrdinalIgnoreCase);
            settings.ModAlerts ??= new(StringComparer.OrdinalIgnoreCase);

            foreach ((string id, string _, string type, int defaultValue) in AltarModsConstants.UpsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                settings.ModTiers.TryAdd(compositeKey, defaultValue);
            }

            foreach ((string id, string _, string type, int defaultValue) in AltarModsConstants.DownsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                settings.ModTiers.TryAdd(compositeKey, defaultValue);
            }

            foreach ((string id, string _, string type, int _) in AltarModsConstants.UpsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                bool isDivineOrbAlert = (type == ClickItMaxSettings.AltarTypeMinion && id == "#% chance to drop an additional Divine Orb")
                    || (type == ClickItMaxSettings.AltarTypeBoss && id == "Final Boss drops # additional Divine Orbs");

                if (settings.ModAlerts.TryAdd(compositeKey, false) && isDivineOrbAlert)
                    settings.ModAlerts[compositeKey] = true;

            }
        }

        internal static string BuildCompositeKey(string type, string id)
            => $"{type}|{id}";

        internal static int GetModTier(ClickItMaxSettings settings, string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return 1;

            return settings.ModTiers.TryGetValue(modId, out int value) ? value : 1;
        }

        internal static int GetModTier(ClickItMaxSettings settings, string modId, string type)
        {
            if (string.IsNullOrEmpty(modId))
                return 1;

            string compositeKey = BuildCompositeKey(type, modId);
            return settings.ModTiers.TryGetValue(compositeKey, out int value) ? value : 1;
        }

        internal static bool GetModAlert(ClickItMaxSettings settings, string modId, string type)
        {
            if (string.IsNullOrEmpty(modId))
                return false;

            string compositeKey = BuildCompositeKey(type, modId);
            if (settings.ModAlerts.TryGetValue(compositeKey, out bool enabled))
                return enabled;

            return settings.ModAlerts.TryGetValue(modId, out enabled) && enabled;
        }
    }
}
