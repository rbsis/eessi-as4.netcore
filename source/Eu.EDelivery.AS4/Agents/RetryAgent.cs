using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotSupportedException = System.NotSupportedException;
using RetryReliability = Eu.EDelivery.AS4.Entities.RetryReliability;

namespace Eu.EDelivery.AS4.Agents;

public class RetryAgent : BackgroundService, IAgent
{
    private readonly ILogger<RetryAgent> _logger;
    private readonly IReceiver _receiver;
    private readonly IDatastoreRepository _repository;
    private readonly IInMessageService _inMessageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryAgent"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="receiver">The receiver used to retrieve <see cref="RetryReliability"/> entities</param>
    /// <param name="repository"></param>
    /// <param name="inMessageService"></param>
    public RetryAgent(
        ILogger<RetryAgent> logger,
        IReceiver receiver,
        IDatastoreRepository repository,
        IInMessageService inMessageService)
    {
        _logger = logger;
        _receiver = receiver;
        _repository = repository;
        _inMessageService = inMessageService;
    }

    /// <summary>
    /// Gets the agent configuration.
    /// </summary>
    /// <value>The agent configuration.</value>
    public AgentConfig AgentConfig { get; } = new AgentConfig("Retry Agent") { Type = AgentType.Retry };

    /// <summary>
    /// Starts the specified agent.
    /// </summary>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns></returns>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting {Name}...", AgentConfig.Name);

        var task = base.StartAsync(cancellationToken);

        _logger.LogInformation("{Name} Started!", AgentConfig.Name);

        return task;
    }

    /// <summary>
    /// Stops this agent.
    /// </summary>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns></returns>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping {Name} ...", AgentConfig.Name);
        _receiver.StopReceiving();

        var task = base.StopAsync(cancellationToken);

        _logger.LogInformation("{Name} stopped.", AgentConfig.Name);

        return task;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Factory.StartNew(
        () => _receiver.StartReceiving(OnReceivedAsync, stoppingToken), TaskCreationOptions.LongRunning);


    private Task<MessagingContext> OnReceivedAsync(ReceivedMessage rm, CancellationToken ct)
    {
        try
        {
            if (rm is ReceivedEntityMessage rem && rem.Entity is RetryReliability rr)
            {
                OnReceivedEntity(rr);
            }
            else
            {
                throw new NotSupportedException($"Only {nameof(ReceivedEntityMessage)} implementations are allowed");
            }
        }
        catch (Exception ex)
        {
            // TODO: must the agent be stopped?
            _logger.LogError(ex, ex.Message);
        }

        return Task.FromResult(
            new MessagingContext(rm, MessagingContextMode.Unknown));
    }

    private void OnReceivedEntity(RetryReliability rr)
    {
        (var refToEntityId, var entityType) = GetRefToEntityIdWithType(rr);
        var op = GetRefEntityOperation(refToEntityId, entityType);

        if (op == Operation.ToBeRetried && rr.CurrentRetryCount < rr.MaxRetryCount)
        {
            var updateOperation = rr.RetryType switch
            {
                RetryType.Delivery => Operation.ToBeDelivered,
                RetryType.Notification => Operation.ToBeNotified,
                RetryType.Send => Operation.ToBeSent,
                RetryType.PiggyBack => Operation.ToBePiggyBacked,
                _ => throw new InvalidOperationException($"Unknown RetryType: {rr.RetryType}")
            };

            var message = "({RetryType}) Update {EntityType} to retry again" + Environment.NewLine
                + " -> Set messages's Operation={UpdateOperation}" + Environment.NewLine
                + " -> Update retry info {{CurrentRetry={CurrentRetryCount}, Status=Pending, LastRetryTime=Now}}";
            _logger.LogDebug(message,
                rr.RetryType,
                entityType,
                updateOperation,
                rr.CurrentRetryCount + 1);

            UpdateRefEntityOperation(refToEntityId, entityType, updateOperation);

            _repository.UpdateRetryReliability(rr.Id, r =>
            {
                r.CurrentRetryCount++;
                r.LastRetryTime = DateTimeOffset.Now;
            });

        }
        else if (rr.CurrentRetryCount >= rr.MaxRetryCount)
        {
            var message = $"({rr.RetryType}) Retry operation is completed, no new retries will happen" + Environment.NewLine
                + " -> Update {entityType}'s Operation=DeadLettered" + Environment.NewLine
                + " -> Update retry cycle {{Status=Completed}}";
            _logger.LogDebug(message,
                 rr.RetryType,
                 entityType);

            UpdateRefEntityOperation(refToEntityId, entityType, Operation.DeadLettered);

            _repository.UpdateRetryReliability(rr.Id, r => r.Status = RetryStatus.Completed);

            if (rr.RetryType == RetryType.Send)
            {
                InsertDeadLetteredError(refToEntityId);
            }
        }
    }

    private static (long, Entity) GetRefToEntityIdWithType(RetryReliability r)
    {
        if (r.RefToInMessageId.HasValue)
        {
            return (r.RefToInMessageId.Value, Entity.InMessage);
        }

        if (r.RefToOutMessageId.HasValue)
        {
            return (r.RefToOutMessageId.Value, Entity.OutMessage);
        }

        if (r.RefToInExceptionId.HasValue)
        {
            return (r.RefToInExceptionId.Value, Entity.InException);
        }

        if (r.RefToOutExceptionId.HasValue)
        {
            return (r.RefToOutExceptionId.Value, Entity.OutException);
        }

        throw new InvalidOperationException(
            "Invalid 'RetryReliability' record: requries a reference to In/Out Messages/Exceptions");
    }

    private enum Entity { InMessage, OutMessage, InException, OutException }

    private Operation GetRefEntityOperation(long id, Entity type) => type switch
    {
        Entity.InMessage => _repository.GetInMessageData(id, m => m.Operation),
        Entity.OutMessage => _repository.GetOutMessageData(id, m => m.Operation),
        Entity.InException => _repository.GetInExceptionData(id, ex => ex.Operation),
        Entity.OutException => _repository.GetOutExceptionData(id, ex => ex.Operation),
        _ => throw new ArgumentOutOfRangeException(paramName: nameof(type), actualValue: type, message: null),
    };

    private void UpdateRefEntityOperation(long id, Entity type, Operation o)
    {
        switch (type)
        {
            case Entity.InMessage:
                _repository.UpdateInMessage(id, m => m.Operation = o);
                break;
            case Entity.OutMessage:
                _repository.UpdateOutMessage(id, m => m.Operation = o);
                break;
            case Entity.InException:
                _repository.UpdateInException(id, ex => ex.Operation = o);
                break;
            case Entity.OutException:
                _repository.UpdateOutException(id, ex => ex.Operation = o);
                break;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(type), actualValue: type, message: null);
        }
    }

    private void InsertDeadLetteredError(long outMessageId)
    {
        var data = _repository.GetOutMessageData(
            outMessageId,
            m => Tuple.Create(m.EbmsMessageId, m.MEP, m.PMode));

        if (data is null)
        {
            return;
        }

        var ebmsMessageId = data.Item1;
        var mep = data.Item2;
        var sendPMode = AS4XmlSerializer.FromString<SendingProcessingMode>(data.Item3);

        _inMessageService.InsertDeadLetteredErrorForAsync(ebmsMessageId, mep, sendPMode);
    }

}
