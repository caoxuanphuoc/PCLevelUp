using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using GrowthTracker.Application.Areas;

namespace GrowthTracker.Desktop.ViewModels.Dialogs;

public sealed partial class ColorSwatch(string hex) : ObservableObject
{
    public string Hex => hex;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Create or rename/recolor an Area (FR-01). Returns the Area id, or null when cancelled.</summary>
public sealed partial class AreaEditorViewModel : DialogViewModel<Guid?>
{
    private readonly IUseCaseDispatcher _dispatcher;
    private readonly Guid? _areaId;

    /// <summary>The six Area hues from UI Design §2.1 plus a few neutral extras.</summary>
    public static IReadOnlyList<string> Palette { get; } =
        ["#2563EB", "#7C3AED", "#16A34A", "#EA580C", "#0D9488", "#DB2777", "#CA8A04", "#0891B2", "#475569"];

    public AreaEditorViewModel(IUseCaseDispatcher dispatcher, AreaDto? area = null)
    {
        _dispatcher = dispatcher;
        _areaId = area?.Id;
        Name = area?.Name;
        Swatches = Palette.Union(area is null ? [] : [area.ColorHex]).Select(h => new ColorSwatch(h)).ToList();
        Select(Swatches.First(s => s.Hex == (area?.ColorHex ?? Palette[0])));
    }

    public override string Title => _areaId is null ? "Thêm lĩnh vực" : "Sửa lĩnh vực";
    public IReadOnlyList<ColorSwatch> Swatches { get; }

    [ObservableProperty] public partial string? Name { get; set; }

    private string _colorHex = Palette[0];

    [RelayCommand]
    private void Select(ColorSwatch swatch)
    {
        foreach (var s in Swatches)
            s.IsSelected = ReferenceEquals(s, swatch);
        _colorHex = swatch.Hex;
    }

    [RelayCommand]
    private Task Submit()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Hãy nhập tên lĩnh vực.";
            return Task.CompletedTask;
        }
        return SubmitAsync(
            () => _areaId is Guid id
                ? _dispatcher.SendAsync(new UpdateAreaCommand(id, Name, _colorHex))
                : _dispatcher.SendAsync(new CreateAreaCommand(Name, _colorHex)),
            id => (Guid?)id);
    }
}
