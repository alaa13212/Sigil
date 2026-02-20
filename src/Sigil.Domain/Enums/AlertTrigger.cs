namespace Sigil.Domain.Enums;

public enum AlertTrigger
{
    NewIssue,           // first occurrence of a new issue
    IssueRegression,    // resolved issue receives new events
    ThresholdExceeded,  // issue exceeds N events in time window
    NewHighSeverity,    // new issue with error/fatal severity
}
