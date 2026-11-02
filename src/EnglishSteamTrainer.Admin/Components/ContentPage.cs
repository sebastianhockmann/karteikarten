using EnglishSteamTrainer.Admin.Services;
using EnglishSteamTrainer.Core;
using Microsoft.AspNetCore.Components;

namespace EnglishSteamTrainer.Admin.Components;

/// <summary>Basis für Seiten, die die geladenen Inhalte zeigen: neu zeichnen, sobald sich etwas ändert.</summary>
public abstract class ContentPage : ComponentBase, IDisposable
{
    [Inject] protected ContentState State { get; set; } = null!;

    protected ContentSet Set => State.Set;

    protected override void OnInitialized() => State.Changed += OnChanged;

    private void OnChanged() => InvokeAsync(StateHasChanged);

    /// <summary>Nach jeder Änderung am Inhalt aufrufen (aktualisiert Speichern-Leiste und Prüfung).</summary>
    protected void Changed() => State.NotifyChanged();

    public virtual void Dispose()
    {
        State.Changed -= OnChanged;
        GC.SuppressFinalize(this);
    }
}
