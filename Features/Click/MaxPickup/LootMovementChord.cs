namespace ClickItMax.Features.Click.MaxPickup;

public readonly record struct LootMovementChord(Keys Key, bool Ctrl, bool Shift, bool Alt)
{
    public static bool TryParse(string? text, out LootMovementChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var tokens = text.Trim().Split('+', StringSplitOptions.TrimEntries);
        bool ctrl = false, shift = false, alt = false;
        for (int i = 0; i < tokens.Length - 1; i++)
            switch (tokens[i].ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": case "CTL": ctrl = true; break;
                case "SHIFT": shift = true; break;
                case "ALT": alt = true; break;
                default: return false;
            }
        string keyText = tokens[^1].ToUpperInvariant();
        keyText = keyText switch { "SPC" => "SPACE", "ESC" => "ESCAPE", _ => keyText };
        if (!Enum.TryParse<Keys>(keyText.Length == 1 && char.IsDigit(keyText[0]) ? "D" + keyText : keyText, true, out var key)
            || key == Keys.None || (key & Keys.Modifiers) != 0 || (int)key <= 6
            || key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey)
            return false;
        chord = new(key, ctrl, shift, alt);
        return true;
    }
}
