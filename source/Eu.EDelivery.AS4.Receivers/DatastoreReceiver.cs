using System.Configuration;
using System.Reflection;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers.Datastore;
using Eu.EDelivery.AS4.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Function =
    System.Func<Eu.EDelivery.AS4.Model.Internal.ReceivedMessage, System.Threading.CancellationToken,
        System.Threading.Tasks.Task<Eu.EDelivery.AS4.Model.Internal.MessagingContext>>;

namespace Eu.EDelivery.AS4.Receivers;

/// <summary>
/// Receiver to poll the database to get the messages which validates a given Expression
/// </summary>
[Info("Datastore receiver")]
public class DatastoreReceiver : PollingTemplate<Entity, ReceivedMessage>, IReceiver
{
    private readonly IDbContextFactory<DatastoreContext> _contextFactory;
    private readonly IAS4MessageBodyStore _bodyStore;

    private DatastoreReceiverSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatastoreReceiver" /> class.
    /// </summary>
    public DatastoreReceiver(
        ILogger<DatastoreReceiver> logger,
        IDbContextFactory<DatastoreContext> contextFactory,
        IAS4MessageBodyStore bodyStore,
        IOptions<DatastoreReceiverSettings> options) : base(logger)
    {
        _contextFactory = contextFactory;
        _bodyStore = bodyStore;
        _settings = options.Value;
    }

    /// <summary>
    /// Start Receiving on the Data Store
    /// </summary>
    /// <param name="messageCallback"></param>
    /// <param name="cancellationToken"></param>
    public void StartReceiving(Function messageCallback, CancellationToken cancellationToken)
    {
        _logger.LogTrace("Start Receiving on Datastore {DisplayString}", _settings.DisplayString);
        StartPolling(messageCallback, cancellationToken);
    }

    /// <summary>
    /// Stop the <see cref="IReceiver"/> instance from receiving.
    /// </summary>
    public void StopReceiving()
    {
        _logger.LogTrace("Stop Receiving on Datastore {DisplayString}", _settings.DisplayString);
    }

    #region Configuration

    [Info("Table", required: true)]
    private string Table => _settings.TableName;

    [Info("Filter", required: true)]
    private string Filter => _settings.Filter;

    [Info("How many rows to take", defaultValue: SettingKeys.TakeRowsDefault, type: "int32")]
    private int TakeRows => _settings.TakeRows;

    [Info("Update", attributes: ["field"])]
    private string Update => _settings.UpdateValue;

    private static class SettingKeys
    {
        public const string PollingInterval = "PollingInterval";
        public const string Table = "Table";
        public const string Filter = "Filter";
        public const string TakeRows = "BatchSize";
        public const string TakeRowsDefault = "20";
        public const string Update = "Update";
        public const string PollingIntervalDefault = "00:00:03";
    }

    [Info("Polling interval (every)", defaultValue: SettingKeys.PollingIntervalDefault)]
    protected override TimeSpan PollingInterval => _settings.PollingInterval;

    /// <summary>
    /// Configure the receiver with a given settings dictionary.
    /// </summary>
    /// <param name="settings"></param>
    void IReceiver.Configure(IEnumerable<Setting> settings)
    {
        var properties = settings.ToDictionary(s => s.Key, s => s);

        var configuredTakeRecords = properties.ReadOptionalProperty(SettingKeys.TakeRows, null!);
        if (configuredTakeRecords == null || !int.TryParse(configuredTakeRecords.Value, out var takeRecords))
        {
            takeRecords = DatastoreReceiverSettings.DefaultTakeRows;
        }

        var pollingInterval = GetPollingIntervalFromProperties(properties);

        var updateSetting = properties.ReadMandatoryProperty(SettingKeys.Update);
        var field = updateSetting["field"]
            ?? throw new ConfigurationErrorsException("The Update setting does not contain a field attribute that indicates the field that must be updated");

        _settings = new DatastoreReceiverSettings
        {
            TableName = properties.ReadMandatoryProperty(SettingKeys.Table).Value,
            Filter = properties.ReadMandatoryProperty(SettingKeys.Filter).Value,
            UpdateField = field.Value,
            UpdateValue = updateSetting.Value,
            PollingInterval = pollingInterval,
            TakeRows = takeRecords
        };
    }

    private static TimeSpan GetPollingIntervalFromProperties(Dictionary<string, Setting> properties)
    {
        if (!properties.TryGetValue(SettingKeys.PollingInterval, out var pollingInterval))
        {
            return DatastoreReceiverSettings.DefaultPollingInterval;
        }

        return pollingInterval.Value.AsTimeSpan(DatastoreReceiverSettings.DefaultPollingInterval);
    }

    #endregion

    /// <summary>
    /// Get the Out Messages from the Store with <see cref="Operation.ToBeSent" /> as Operation
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override IEnumerable<Entity> GetMessagesToPoll(CancellationToken cancellationToken)
    {
        try
        {
            return GetMessagesEntitiesForConfiguredExpression();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occured while polling the datastore. Polling on table {Table} with interval {PollingInterval} seconds",
                Table,
                PollingInterval.TotalSeconds);

            return [];
        }
    }

    private IEnumerable<Entity> GetMessagesEntitiesForConfiguredExpression()
    {
        using var context = _contextFactory.CreateDbContext();
        if (context.NativeCommands.ExclusiveLockIsolation.HasValue)
        {
            context.Database.BeginTransaction(context.NativeCommands.ExclusiveLockIsolation.Value);
        }
        else
        {
            context.Database.BeginTransaction();
        }

        try
        {
            var entities = FindAnyMessageEntitiesWithConfiguredExpression(context);
            context.Database.CommitTransaction();
            return entities;
        }
        catch (Exception exception)
        {
            context.Database.RollbackTransaction();
            _logger.LogError(exception, "GetMessagesEntitiesForConfiguredExpression failed");
        }

        return [];
    }

    private IEnumerable<Entity> FindAnyMessageEntitiesWithConfiguredExpression(DatastoreContext context)
    {
        var tablePropertyInfo = GetTableSetPropertyInfo();
        if (tablePropertyInfo?.GetValue(context) is not IQueryable<Entity>)
        {
            throw new ConfigurationErrorsException(
                $"The configured table {Table} could not be found");
        }

        // TODO: 
        // - validate the Filter clause for sql injection
        // - make sure that single quotes are used around string vars.  
        //      (Maybe make it dependent on the DB type, same is true for escape characters [] in sql server, ...             

        var entities = context.NativeCommands.ExclusivelyRetrieveEntities(Table, Filter, TakeRows);
        if (!entities.Any())
        {
            return entities;
        }

        LockEntitiesBeforeContinueToProcessThem(entities);

        context.SaveChanges();

        return entities;
    }

    // TODO: isn't this something Lazy<> would solve?
    // ReSharper disable once InconsistentNaming
    private PropertyInfo? _tableSetPropertyInfo;

    private PropertyInfo? GetTableSetPropertyInfo()
    {
        if (_tableSetPropertyInfo == null)
        {
            _tableSetPropertyInfo = typeof(DatastoreContext).GetProperty(Table);
        }

        return _tableSetPropertyInfo;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>")]
    private void LockEntitiesBeforeContinueToProcessThem(IEnumerable<Entity> entities)
    {
        if (!entities.Any())
        {
            return;
        }

        var updateFieldInfo = GetUpdateFieldProperty(entities.First());

        foreach (var entity in entities)
        {
            var updateValue = Conversion.Convert(updateFieldInfo.PropertyType, Update);

            _logger.LogTrace("Update {Entity}.{UpdateFieldInfo}={Value}", entity.GetType().Name, updateFieldInfo.Name, updateValue);

            updateFieldInfo.SetValue(
                obj: entity,
                value: updateValue,
                invokeAttr: BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                index: null,
                culture: null);
        }
    }

    // TODO: isn't this someting 'Lazy<>' would solve?
    // ReSharper disable InconsistentNaming  (__ indicates that this field should not be used directly; use the GetUpdateFieldProperty instead.)               
    private PropertyInfo? __updateProperty;
    // ReSharper restore InconsistentNaming

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>")]
    private PropertyInfo GetUpdateFieldProperty(Entity entity)
    {
        static PropertyInfo? FindPropertyInHierarchy(string propertyName, Type t) => t.BaseType == null
            ? null
            : t.GetProperty(
                propertyName,
                BindingFlags.Instance
                | BindingFlags.DeclaredOnly
                | BindingFlags.Public
                | BindingFlags.NonPublic) ?? FindPropertyInHierarchy(propertyName, t.BaseType);

        if (__updateProperty == null)
        {
            __updateProperty = FindPropertyInHierarchy(_settings!.UpdateField, entity.GetType());

            if (__updateProperty == null)
            {
                throw new ConfigurationErrorsException(
                    $"The configured Update-field {_settings!.UpdateField} could not be found");
            }
        }

        return __updateProperty;
    }

    /// <summary>
    /// Describe what to do when a Out Message is received
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="messageCallback"></param>
    /// <param name="cancellation"></param>
    protected override async Task MessageReceivedAsync(Entity entity, Function messageCallback, CancellationToken cancellation)
    {
        if (entity is MessageEntity messageEntity)
        {
            await ReceiveMessageEntityAsync(messageEntity, messageCallback, cancellation);
        }
        else
        {
            await ReceiveEntity(entity, messageCallback, cancellation);
        }
    }

    private async Task ReceiveMessageEntityAsync(
        MessageEntity messageEntity,
        Function messageCallback,
        CancellationToken cancellation)
    {
        _logger.LogDebug("Received message FROM {Table} WHERE {Filter}", Table, Filter);

        using var stream = await _bodyStore.LoadMessageBodyAsync(messageEntity.MessageLocation, cancellation);
        if (stream == null)
        {
            _logger.LogError("MessageBody cannot be retrieved for EbmsMessageId: {EbmsMessageId}", messageEntity.EbmsMessageId);
        }
        else if (messageEntity.ContentType == null)
        {
            _logger.LogError("ContentType cannot be found for EbmsMessageId: {EbmsMessageId}", messageEntity.EbmsMessageId);
        }
        else
        {
            ReceivedEntityMessage? receivedMessage = null;
            try
            {
                receivedMessage = new ReceivedEntityMessage(messageEntity, stream, messageEntity.ContentType);
                await messageCallback(receivedMessage, cancellation);
            }
            finally
            {
                if (receivedMessage != null)
                {
                    await receivedMessage.UnderlyingStream.DisposeAsync().AsTask();
                }
            }
        }
    }

    private static async Task ReceiveEntity(Entity entity, Function messageCallback, CancellationToken token)
    {
        var message = new ReceivedEntityMessage(entity);
        var result = await messageCallback(message, token);
        result?.Dispose();
    }


    protected override void HandleMessageException(Entity message, Exception exception)
    {
        _logger.LogError(exception, "HandleMessage failed");

        if (exception is not AggregateException aggregate)
        {
            return;
        }

        foreach (var ex in aggregate.InnerExceptions)
        {
            _logger.LogError(ex, "AggregateException InnerException");
        }
    }


    protected override void ReleasePendingItems()
    {
        // TODO: we should release the records that have been held locked by this
        // DataStoreReceiver so that they won't be locked forever.
        // -> Reset the records that have been locked by this process and who'sestatus is still the same.
    }
}
