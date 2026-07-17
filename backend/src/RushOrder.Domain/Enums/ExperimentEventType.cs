namespace RushOrder.Domain.Enums;

// Append-only event log — conversion metrics are computed by aggregating these
// (see ExperimentRepository.GetResultsAsync), not by mutating a row per session.
public enum ExperimentEventType { Exposure, SuggestionAdded, OrderCompleted }
