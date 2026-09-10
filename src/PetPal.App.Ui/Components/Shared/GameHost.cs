using Microsoft.AspNetCore.Components;

namespace PetPal.App.Ui.Components.Shared;

/// <summary>
/// Oyunu açan ekranın oyuna verdiyi tək əlaqə: çıxış.
///
/// Kaskad dəyər kimi ötürülür ki, on oyun komponentinin hər biri eyni
/// parametri əl ilə daşımasın — çıxış düyməsi oyunun özündə deyil, ortaq
/// <see cref="GameShell"/> çərçivəsindədir.
/// </summary>
public sealed class GameHost
{
    public GameHost(EventCallback exit) => Exit = exit;

    public EventCallback Exit { get; }
}
