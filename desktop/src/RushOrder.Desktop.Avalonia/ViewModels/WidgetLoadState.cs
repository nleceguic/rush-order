namespace RushOrder.Desktop.Avalonia.ViewModels;

/// <summary>
/// Reflects only the result of the real data-source fetch — never the presence of
/// simulated fallback data. See <see cref="WidgetViewModelBase.IsShowingSimulatedData"/>
/// for that orthogonal dimension. <see cref="Loaded"/> never coexists with simulated data.
/// </summary>
public enum WidgetLoadState { Loading, Loaded, Empty, Error }
