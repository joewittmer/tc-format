using System;
using System.ComponentModel;
using System.Globalization;

namespace TcFormat.Xae;

[TypeConverter(typeof(FormatShortcutPresetConverter))]
public enum FormatShortcutPreset
{
    CtrlRThenCtrlF,
    Ctrl1,
    Ctrl2,
    Ctrl3,
    Ctrl4,
    Ctrl5,
    Ctrl6,
    Ctrl7,
    Ctrl8,
    Ctrl9
}

internal static class FormatShortcutPresetExtensions
{
    public static string ToDisplayText(this FormatShortcutPreset shortcut) => shortcut switch
    {
        FormatShortcutPreset.CtrlRThenCtrlF => "Ctrl+R, Ctrl+F",
        FormatShortcutPreset.Ctrl1 => "Ctrl+1",
        FormatShortcutPreset.Ctrl2 => "Ctrl+2",
        FormatShortcutPreset.Ctrl3 => "Ctrl+3",
        FormatShortcutPreset.Ctrl4 => "Ctrl+4",
        FormatShortcutPreset.Ctrl5 => "Ctrl+5",
        FormatShortcutPreset.Ctrl6 => "Ctrl+6",
        FormatShortcutPreset.Ctrl7 => "Ctrl+7",
        FormatShortcutPreset.Ctrl8 => "Ctrl+8",
        FormatShortcutPreset.Ctrl9 => "Ctrl+9",
        _ => throw new ArgumentOutOfRangeException(nameof(shortcut), shortcut, null)
    };
}

internal sealed class FormatShortcutPresetConverter : EnumConverter
{
    public FormatShortcutPresetConverter()
        : base(typeof(FormatShortcutPreset))
    {
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is string text)
        {
            foreach (FormatShortcutPreset shortcut in Enum.GetValues(typeof(FormatShortcutPreset)))
            {
                if (string.Equals(text, shortcut.ToDisplayText(), StringComparison.OrdinalIgnoreCase))
                {
                    return shortcut;
                }
            }
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (destinationType == typeof(string) && value is FormatShortcutPreset shortcut)
        {
            return shortcut.ToDisplayText();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
