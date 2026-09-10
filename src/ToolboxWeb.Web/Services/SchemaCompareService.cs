using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.SqlSchema;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Services;

public interface ISchemaCompareService
{
    Task<SqlServerProbeViewModel> ProbeAsync(SqlConnectionInputModel input, CancellationToken cancellationToken = default);

    Task<SchemaCompareResultViewModel> CompareAsync(
        SchemaCompareRequest request,
        string sessionId,
        CancellationToken cancellationToken = default);

    SchemaCompareStoredResult? GetStoredResult(string resultId, string sessionId);
}

public sealed class SchemaCompareService : ISchemaCompareService
{
    private readonly ISqlSchemaReader _reader;
    private readonly ISchemaComparer _comparer;
    private readonly ISyncScriptGenerator _scriptGenerator;
    private readonly ISchemaCompareResultStore _results;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogService _activityLog;

    public SchemaCompareService(
        ISqlSchemaReader reader,
        ISchemaComparer comparer,
        ISyncScriptGenerator scriptGenerator,
        ISchemaCompareResultStore results,
        ICurrentUserService currentUser,
        IActivityLogService activityLog)
    {
        _reader = reader;
        _comparer = comparer;
        _scriptGenerator = scriptGenerator;
        _results = results;
        _currentUser = currentUser;
        _activityLog = activityLog;
    }

    public Task<SqlServerProbeViewModel> ProbeAsync(
        SqlConnectionInputModel input,
        CancellationToken cancellationToken = default)
    {
        return _reader.ProbeAsync(input, cancellationToken);
    }

    public async Task<SchemaCompareResultViewModel> CompareAsync(
        SchemaCompareRequest request,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        // Each read opens its own connection, so the two sides can run at the same time.
        var sourceTask = _reader.ReadAsync(request.Source, request.Options, cancellationToken);
        var targetTask = _reader.ReadAsync(request.Target, request.Options, cancellationToken);
        await Task.WhenAll(sourceTask, targetTask);

        var source = await sourceTask;
        var target = await targetTask;

        var differences = _comparer.Compare(source, target, request.Options);
        var script = _scriptGenerator.Generate(source, target, differences, request.Options);

        var now = DateTime.Now;
        var resultId = Guid.NewGuid().ToString("N");

        _results.Save(new SchemaCompareStoredResult
        {
            ResultId = resultId,
            SessionId = sessionId,
            Script = script,
            SourceLabel = source.Label,
            TargetLabel = target.Label,
            CreatedAt = now,
            ExpiresAt = now.Add(_results.Ttl)
        });

        await LogComparisonAsync(source, target, differences);

        return new SchemaCompareResultViewModel
        {
            ResultId = resultId,
            SourceLabel = source.Label,
            TargetLabel = target.Label,
            GeneratedAt = now,
            Stats = BuildStats(differences),
            Rows = BuildRows(differences),
            Script = script
        };
    }

    public SchemaCompareStoredResult? GetStoredResult(string resultId, string sessionId)
    {
        return _results.Get(resultId, sessionId);
    }

    private async Task LogComparisonAsync(
        DatabaseSchema source,
        DatabaseSchema target,
        IReadOnlyList<SchemaDifference> differences)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Labels carry server and database only; credentials are never logged.
        var changeCount = differences.Count(x => x.ChangeType != SchemaChangeType.Same);
        await _activityLog.LogAsync(
            userId,
            ActivityActionType.Use,
            ActivityEntityType.SchemaCompare,
            null,
            $"Compared {source.Label} with {target.Label}: {changeCount} difference(s).");
    }

    private static SchemaCompareStatsViewModel BuildStats(IReadOnlyList<SchemaDifference> differences)
    {
        return new SchemaCompareStatsViewModel
        {
            Total = differences.Count,
            OnlyInSource = differences.Count(x => x.ChangeType == SchemaChangeType.OnlyInSource),
            OnlyInTarget = differences.Count(x => x.ChangeType == SchemaChangeType.OnlyInTarget),
            Different = differences.Count(x => x.ChangeType == SchemaChangeType.Different),
            Same = differences.Count(x => x.ChangeType == SchemaChangeType.Same)
        };
    }

    private static IReadOnlyList<SchemaDifferenceRowViewModel> BuildRows(IReadOnlyList<SchemaDifference> differences)
    {
        return differences
            .Select((difference, index) => new SchemaDifferenceRowViewModel
            {
                Index = index + 1,
                ObjectType = difference.ObjectType.ToString(),
                Schema = difference.Schema,
                Name = difference.Name,
                ChangeType = difference.ChangeType.ToString(),
                DetailCount = difference.Details.Count,
                Details = difference.Details
                    .Select(detail => new SchemaDifferenceDetailViewModel
                    {
                        Category = detail.Category,
                        Member = detail.Member,
                        Property = detail.Property,
                        SourceValue = detail.SourceValue ?? string.Empty,
                        TargetValue = detail.TargetValue ?? string.Empty,
                        ChangeType = detail.ChangeType.ToString()
                    })
                    .ToArray()
            })
            .ToArray();
    }
}
