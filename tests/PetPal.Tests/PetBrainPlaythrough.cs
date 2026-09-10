using System.Net.Http.Json;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Macərəni uşağın etdiyi kimi oynayan ortaq köməkçi.
///
/// <para>Üç test faylı eyni addımları ayrı-ayrılıqda yazırdı və budaqlanan
/// hekayə gələndə üçü də eyni cür sındı: yeni ekran növləri (<c>Consequence</c>,
/// <c>Ending</c>) variantsızdır və tapmaca daşımır, köhnə köməkçilər isə
/// «variant yoxdursa tapmacadır» fərziyyəsi ilə işləyirdi.</para>
///
/// <para><b>Doğru cavab serverdən ALINMIR.</b> Tapmaca yalnız DTO-dakı görünən
/// məlumatdan həll olunur — bu, həm də bir invariantı yoxlayır: tapmaca
/// ekranda görünənlə həll oluna bilməlidir.</para>
/// </summary>
public static class PetBrainPlaythrough
{
    /// <summary>Sonsuz döngəyə qarşı sərt hədd.</summary>
    private const int MaxSteps = 40;

    public static async Task<PetBrainRunDto> StartAsync(ApiTestClient client)
    {
        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    /// <summary>Macəra bitibmi — həm xətti, həm də budaqlanan model üçün.</summary>
    public static bool IsFinished(PetBrainRunDto run) =>
        run.Stage is null || run.Stage.Kind == PetBrainStageKind.Ending;

    /// <summary>Bir addım atır. Cavab uğursuz olsa test dərhal dayanır.</summary>
    public static async Task<PetBrainRunDto> StepAsync(
        ApiTestClient client, PetBrainRunDto run, string? optionKey = null)
    {
        var stage = run.Stage!;

        var request = new PetBrainChoiceRequest
        {
            StageIndex = stage.Index,
            NodeId = string.IsNullOrEmpty(stage.NodeId) ? null : stage.NodeId
        };

        if (stage.Kind == PetBrainStageKind.Puzzle)
            request.SelectedIds = Solve(stage.Puzzle!);
        else
            request.OptionKey = optionKey ?? FirstOption(stage);

        var response = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices", request);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    /// <summary>Variantsız ekranlarda (giriş, nəticə) uşaq yalnız "davam" deyir.</summary>
    public static string FirstOption(PetBrainStageDto stage) =>
        stage.Options.Count > 0 ? stage.Options[0].Key : "continue";

    /// <summary>Macərəni sona qədər oynayır; <paramref name="choices"/> verilibsə onları işlədir.</summary>
    public static async Task<PetBrainRunDto> PlayToEndAsync(
        ApiTestClient client, params string[] choices)
    {
        var run = await StartAsync(client);
        return await ContinueToEndAsync(client, run, choices);
    }

    /// <summary>
    /// Başlamış macərəni sona qədər aparır.
    ///
    /// <para><paramref name="choices"/> yalnız SEÇİM ekranlarında sıra ilə
    /// işlədilir; qalan ekranlarda ilk (yeganə) yol seçilir.</para>
    /// </summary>
    public static async Task<PetBrainRunDto> ContinueToEndAsync(
        ApiTestClient client, PetBrainRunDto run, params string[] choices)
    {
        var next = 0;
        var guard = 0;

        while (!IsFinished(run) && guard++ < MaxSteps)
        {
            string? option = null;

            if (run.Stage!.Kind == PetBrainStageKind.Choice && next < choices.Length)
                option = choices[next++];

            run = await StepAsync(client, run, option);
        }

        Assert.True(IsFinished(run), "Macəra sona çatmadı.");

        return run;
    }

    /// <summary>Tapmaca ekranına qədər aparır.</summary>
    public static async Task<PetBrainRunDto> AdvanceToPuzzleAsync(
        ApiTestClient client, PetBrainRunDto run)
    {
        var guard = 0;

        while (!IsFinished(run) && run.Stage!.Kind != PetBrainStageKind.Puzzle && guard++ < MaxSteps)
            run = await StepAsync(client, run);

        Assert.Equal(PetBrainStageKind.Puzzle, run.Stage!.Kind);

        return run;
    }

    public static async Task<PetBrainRunDto> ReachPuzzleAsync(ApiTestClient client) =>
        await AdvanceToPuzzleAsync(client, await StartAsync(client));

    /// <summary>Adı verilmiş qraf düyününə qədər aparır.</summary>
    public static async Task<PetBrainRunDto> AdvanceToNodeAsync(
        ApiTestClient client, PetBrainRunDto run, string nodeId)
    {
        var guard = 0;

        while (!IsFinished(run)
               && !string.Equals(run.Stage!.NodeId, nodeId, StringComparison.Ordinal)
               && guard++ < MaxSteps)
            run = await StepAsync(client, run);

        Assert.Equal(nodeId, run.Stage!.NodeId);

        return run;
    }

    public static async Task<PetBrainRunDto> CompleteAsync(ApiTestClient client, Guid runId)
    {
        var response = await client.Http.PostAsync($"/api/pet-brain/runs/{runId}/complete", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    /// <summary>
    /// Tapmacanı GÖRÜNƏN məlumatdan həll edir.
    ///
    /// <para>Qaydalar burada QƏSDƏN yenidən yazılıb: serverin <c>RouteRules</c>
    /// sinfini çağırsaydıq, test məhz yoxlamalı olduğu şeyi — serverin qaydası
    /// ilə ekranda görünən məlumatın üst-üstə düşməsini — yoxlamazdı.</para>
    /// </summary>
    public static List<string> Solve(PetBrainPuzzleDto puzzle) => puzzle.Mechanic switch
    {
        PetBrainPuzzleMechanic.OrderedRoute => SolveRoute(puzzle)
            ?? throw new InvalidOperationException("Görünən məlumatla marşrut tapılmadı."),

        PetBrainPuzzleMechanic.SequenceOrder =>
            [.. puzzle.Items.OrderBy(i => i.Value).Select(i => i.Id)],

        PetBrainPuzzleMechanic.RouteLogic =>
            [puzzle.Items.Where(i => i.Icon != "⛔").OrderBy(i => i.Value).First().Id],

        // Yaradıcı yolda səhv seçim yoxdur — sxemin istədiyi say kifayətdir.
        _ => [.. puzzle.Items.Take(puzzle.AnswerSchema.Min).Select(i => i.Id)]
    };

    /// <summary>Enerji büdcəsinə və məcburi düyünlərə uyğun ən qısa marşrut.</summary>
    public static List<string>? SolveRoute(PetBrainPuzzleDto puzzle)
    {
        var start = puzzle.Nodes.FirstOrDefault(n => n.Kind == PetBrainNodeKind.Start);

        if (start is null)
            return null;

        var cost = puzzle.MoveCost ?? 1;
        var maximum = puzzle.MaximumEnergy ?? 0;

        List<string>? found = null;

        Walk([start.Id], puzzle.InitialEnergy ?? 0);

        return found;

        void Walk(List<string> path, int energy)
        {
            if (found is not null || path.Count > puzzle.AnswerSchema.Max)
                return;

            var here = puzzle.Nodes.First(n => n.Id == path[^1]);

            if (here.Kind == PetBrainNodeKind.Recharge)
                energy = Math.Min(maximum, energy + (here.EnergyDelta ?? 0));

            if (here.Kind == PetBrainNodeKind.Goal)
            {
                // Hədəfə çatmaq azdır: MƏCBURİ düyünlərdən keçmək şərtdir.
                if (puzzle.RequiredBeforeGoal.All(path.Contains))
                    found = [.. path];

                return;
            }

            foreach (var edge in puzzle.Edges.Where(e => e.From == here.Id))
            {
                if (path.Contains(edge.To) || energy < cost)
                    continue;

                Walk([.. path, edge.To], energy - cost);
            }
        }
    }
}
