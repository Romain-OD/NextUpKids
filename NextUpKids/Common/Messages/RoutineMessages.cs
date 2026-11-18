using NextUpKids.Common.Database;

namespace NextUpKids.Common.Messages;

public sealed record PeriodStartedMessage(DateOnly Day, Period Period);
public sealed record RoutineChangedMessage;
public sealed record ParentGateRequestedMessage;
