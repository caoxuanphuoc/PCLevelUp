using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GrowthTracker.Desktop.ViewModels.Calendar;
using GrowthTracker.Desktop.ViewModels.Pages;

namespace GrowthTracker.Desktop.Views.Pages;

/// <summary>
/// Pointer plumbing the ViewModel can't express: tap a day to select it, drag a session chip onto another day to
/// reschedule it (TDD §6.3), and "T" to jump to today (TDD §6.2).
/// </summary>
public partial class CalendarView : UserControl
{
    private const double DragThreshold = 6;
    private PointerPressedEventArgs? _pressed;
    private SessionChipViewModel? _pressedSession;
    private Point _pressedAt;

    public CalendarView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, _) => ClearPress(), RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.TappedEvent, OnTapped);
        AddHandler(InputElement.DoubleTappedEvent, OnDoubleTapped);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        KeyDown += OnKeyDown;
        AttachedToVisualTree += (_, _) => Focus();
    }

    private CalendarViewModel? ViewModel => DataContext as CalendarViewModel;

    /// <summary>The nearest ancestor (or self) of the event source whose DataContext is a <typeparamref name="T"/>.</summary>
    private static T? Find<T>(object? source) where T : class
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is StyledElement { DataContext: T match })
                return match;
            if (visual is StyledElement { DataContext: TimedBlock block } && typeof(T) == typeof(SessionChipViewModel))
                return block.Session as T;
        }
        return null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var session = Find<SessionChipViewModel>(e.Source);
        if (session is { CanReschedule: true } && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = e;
            _pressedSession = session;
            _pressedAt = e.GetPosition(this);
        }
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is null || _pressedSession is null)
            return;
        var delta = e.GetPosition(this) - _pressedAt;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        var pressed = _pressed;
        var session = _pressedSession;
        ClearPress();
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(session.OccurrenceId.ToString()));
        ShowGhost(session, e.GetPosition(DragLayer));
        try
        {
            await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Move);
        }
        finally
        {
            HideGhost(); // dropped, cancelled with Esc, or released outside the window
        }
    }

    private void ClearPress()
    {
        _pressed = null;
        _pressedSession = null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var cell = DropCell(e.Source);
        bool accepts = cell is not null
                       && (Find<DayCellViewModel>(e.Source)?.AcceptsDrop ?? Find<WeekColumnViewModel>(e.Source)?.AcceptsDrop) == true
                       && e.DataTransfer.TryGetText() is { } text && Guid.TryParse(text, out _);
        e.DragEffects = accepts ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
        MoveGhost(e.GetPosition(DragLayer), accepts);
        HighlightDropCell(accepts ? cell : null);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        HideGhost();
        var target = Find<DayCellViewModel>(e.Source)?.Date ?? Find<WeekColumnViewModel>(e.Source)?.Date;
        if (target is null || ViewModel is null || e.DataTransfer.TryGetText() is not { } text || !Guid.TryParse(text, out var id))
            return;
        e.Handled = true;
        await ViewModel.MoveSessionAsync(id, target.Value);
    }

    // ── Drag ghost ──
    private static readonly Vector GhostOffset = new(14, 14); // just below-right of the pointer, so it never hides the target
    private Border? _highlighted;

    private void ShowGhost(SessionChipViewModel session, Point at)
    {
        var color = Presentation.Display.Brush(session.ColorHex);
        DragGhost.BorderBrush = color;
        DragGhostIcon.Foreground = color;
        DragGhostTitle.Text = string.IsNullOrEmpty(session.TimeText) ? session.Name : $"{session.TimeText} · {session.Name}";
        DragGhostTitle.Foreground = color;
        DragGhost.IsVisible = true;
        MoveGhost(at, accepts: true);
    }

    private void MoveGhost(Point at, bool accepts)
    {
        if (!DragGhost.IsVisible)
            return;
        Canvas.SetLeft(DragGhost, at.X + GhostOffset.X);
        Canvas.SetTop(DragGhost, at.Y + GhostOffset.Y);
        DragGhost.Opacity = accepts ? 0.95 : 0.5;
        DragGhostIcon.Kind = accepts ? Material.Icons.MaterialIconKind.CalendarArrowRight : Material.Icons.MaterialIconKind.Cancel;
        DragGhostHint.Text = accepts ? "Thả để dời lịch" : "Không thể dời vào ngày này";
        DragGhostHint.Foreground = accepts ? Avalonia.Media.Brushes.Gray : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DC2626"));
    }

    private void HideGhost()
    {
        DragGhost.IsVisible = false;
        DragGhostTitle.Text = null;
        HighlightDropCell(null);
    }

    /// <summary>The day cell / week column border under the pointer.</summary>
    private static Border? DropCell(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
            if (visual is Border border && border.Classes.Contains("day-cell"))
                return border;
        return null;
    }

    private void HighlightDropCell(Border? cell)
    {
        if (ReferenceEquals(cell, _highlighted))
            return;
        _highlighted?.Classes.Remove("drop-target");
        cell?.Classes.Add("drop-target");
        _highlighted = cell;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Find<Button>(e.Source) is not null)
            return;
        if (Find<DayCellViewModel>(e.Source) is { } day)
            day.SelectCommand.Execute(null);
        else if (Find<WeekColumnViewModel>(e.Source) is { } column)
            column.SelectCommand.Execute(null);
    }

    /// <summary>Double-click a session to log it or see its result.</summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Find<SessionChipViewModel>(e.Source) is { CanOpen: true } session)
            session.OpenCommand.Execute(null);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.T && e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && ViewModel is not null)
        {
            ViewModel.GoToTodayCommand.Execute(null);
            e.Handled = true;
        }
    }
}
