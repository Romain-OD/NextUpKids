using NextUpKids.Common.Database;

namespace NextUpKids.Common.Messages;

// EventId > 0: a dated calendar event started (Period is then irrelevant).
public sealed record PeriodStartedMessage(DateOnly Day, Period Period, int EventId = 0);
public sealed record RoutineChangedMessage;
public sealed record ParentGateRequestedMessage;
