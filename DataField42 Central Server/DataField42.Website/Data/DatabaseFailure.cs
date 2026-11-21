using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataField42.Website.Data;

/// <summary>
/// Recognises a failure to reach the database, so the few places that can carry on without it say
/// so instead of returning an error page. The retrying execution strategy wraps the real failure,
/// which is why the wrapper has to be matched as well as the failure itself.
/// </summary>
public static class DatabaseFailure
{
    public static bool IsUnavailable(Exception exception) => exception
        is DbException
        or DbUpdateException
        or RetryLimitExceededException
        or InvalidOperationException;
}
