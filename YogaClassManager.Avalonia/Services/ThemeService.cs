using Avalonia;
using Avalonia.Styling;
using YogaClassManager.Avalonia.Services.Database;

namespace YogaClassManager.Avalonia.Services;

/// <summary>The appearance choice on the Settings page.</summary>
public enum AppTheme
{
    /// <summary>Follow the operating system's light/dark setting (the default).</summary>
    System,
    Light,
    Dark
}

public interface IThemeService
{
    AppTheme Current { get; }

    /// <summary>Applies the saved choice - called once at startup, before the first window shows.</summary>
    void ApplySaved();

    /// <summary>Applies <paramref name="theme" /> immediately and saves it for next launch.</summary>
    void Set(AppTheme theme);
}

/// <summary>
///     Applies the app-wide theme through Avalonia's <c>Application.RequestedThemeVariant</c>: every
///     token in Styles/Tokens.axaml that's theme-scoped (ThemeDictionaries Light/Dark) follows it, so the
///     whole UI switches live, no restart. "Match system" is <see cref="ThemeVariant.Default" />, which
///     tracks the OS setting - including when it changes while the app is open.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly IAppSettingsService settingsService;
    private readonly Action<ThemeVariant> applyVariant;

    /// <param name="applyVariant">How to apply a variant - the running Application by default; tests
    /// pass a recorder.</param>
    public ThemeService(IAppSettingsService settingsService, Action<ThemeVariant>? applyVariant = null)
    {
        this.settingsService = settingsService;
        this.applyVariant = applyVariant ?? (variant =>
        {
            if (Application.Current is { } app)
                app.RequestedThemeVariant = variant;
        });
        Current = settingsService.Load().Theme;
    }

    public AppTheme Current { get; private set; }

    public static ThemeVariant ToVariant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };

    public void ApplySaved() => applyVariant(ToVariant(Current));

    public void Set(AppTheme theme)
    {
        if (theme == Current)
            return;

        Current = theme;
        applyVariant(ToVariant(theme));

        // Load-modify-save, so the other settings (the database path) survive.
        var settings = settingsService.Load();
        settings.Theme = theme;
        settingsService.Save(settings);
    }
}
