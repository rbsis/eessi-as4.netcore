using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Pmodes;
using Eu.EDelivery.AS4.Fe.Pmodes.Model;
using Eu.EDelivery.AS4.Model.PMode;
using FluentValidation;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
///     Manage pmodes
/// </summary>
/// <seealso cref="IPmodeService" />
public class PmodeService : IPmodeService
{
    private readonly IAs4PmodeSource _source;
    private readonly IValidator<SendingProcessingMode> _sendingProcessingModeValidator;
    private readonly IValidator<ReceivingProcessingMode> _receivingProcessingModeValidator;
    private readonly bool _disableValidation;

    /// <summary>
    /// Initializes a new instance of the <see cref="PmodeService" /> class.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="sendingProcessingModeValidator"></param>
    /// <param name="receivingProcessingModeValidator"></param>
    /// <param name="disableValidation">if set to <c>true</c> [disable validation].</param>
    public PmodeService(
        IAs4PmodeSource source,
        IValidator<SendingProcessingMode> sendingProcessingModeValidator,
        IValidator<ReceivingProcessingMode> receivingProcessingModeValidator,
        bool disableValidation = false)
    {
        _source = source;
        _sendingProcessingModeValidator = sendingProcessingModeValidator;
        _receivingProcessingModeValidator = receivingProcessingModeValidator;
        _disableValidation = disableValidation;
    }

    /// <summary>
    ///     Gets the receiving names.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<string>> GetReceivingNamesAsync(CancellationToken cancellationToken) =>
        await _source.GetReceivingNamesAsync(cancellationToken);

    /// <summary>
    ///     Get a list of receiving pmodes
    /// </summary>
    /// <param name="name">The name of the pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ReceivingBasePmode?> GetReceivingByNameAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        return await _source.GetReceivingByNameAsync(name, cancellationToken);
    }

    /// <summary>
    ///     Get a list of sending pmodes
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<string>> GetSendingNamesAsync(CancellationToken cancellationToken) =>
        await _source.GetSendingNamesAsync(cancellationToken);

    /// <summary>
    ///     Get a sending pmode by name
    /// </summary>
    /// <param name="name">The name of the pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<SendingBasePmode?> GetSendingByNameAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        return await _source.GetSendingByNameAsync(name, cancellationToken);
    }

    /// <summary>
    ///     Create a receiving pmode
    /// </summary>
    /// <param name="basePmode">The pmode to create</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">
    ///     Exception thrown when a pmode with the supplied name
    ///     already exists
    /// </exception>
    public async Task CreateReceivingAsync(ReceivingBasePmode basePmode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePmode.Name, nameof(basePmode.Name));
        var exists = await _source.GetReceivingByNameAsync(basePmode.Name, cancellationToken);
        if (exists != null)
        {
            throw new AlreadyExistsException($"BasePmode with name {basePmode.Name} already exists.");
        }
        ValidateReceivingPmode(basePmode);
        await _source.CreateReceivingAsync(basePmode, cancellationToken);
    }

    /// <summary>
    ///     Create sending pmode
    /// </summary>
    /// <param name="basePmode">The pmode to create.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">
    ///     Exception thrown when a pmode with the supplied name
    ///     already exists
    /// </exception>
    public async Task CreateSendingAsync(SendingBasePmode basePmode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePmode.Name, nameof(basePmode.Name));
        var exists = await _source.GetSendingByNameAsync(basePmode.Name, cancellationToken);
        if (exists != null)
        {
            throw new AlreadyExistsException($"BasePmode with name {basePmode.Name} already exists.");
        }
        ValidateSendingPmode(basePmode);
        await _source.CreateSendingAsync(basePmode, cancellationToken);
    }

    /// <summary>
    ///     Delete a receiving pmode
    /// </summary>
    /// <param name="name">The name of the pmode to delete.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">Exception thrown when the pmode doesn't exist</exception>
    public async Task DeleteReceivingAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        _ = await _source.GetReceivingByNameAsync(name, cancellationToken)
            ?? throw new NotFoundException($"BasePmode with name {name} doesn't exist");
        await _source.DeleteReceivingAsync(name, cancellationToken);
    }

    /// <summary>
    ///     Delete a sending pmode
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">Exception thrown when the pmode doesn't exist</exception>
    public async Task DeleteSendingAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        _ = await _source.GetSendingByNameAsync(name, cancellationToken)
            ?? throw new NotFoundException($"BasePmode with name {name} doesn't exist");
        await _source.DeleteSendingAsync(name, cancellationToken);
    }

    /// <summary>
    ///     Update sending pmode
    /// </summary>
    /// <param name="basePmode">Date to update the sending pmode with</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">
    ///     Exception thrown when a sending pmode with the supplied
    ///     name already exists
    /// </exception>
    public async Task UpdateSendingAsync(SendingBasePmode basePmode, string originalName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePmode.Name, nameof(basePmode.Name));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        if (basePmode.Name != originalName)
        {
            var newExists = await GetSendingByNameAsync(basePmode.Name, cancellationToken);
            if (newExists != null)
            {
                throw new AlreadyExistsException($"BasePmode with {originalName} already exists");
            }
        }
        ValidateSendingPmode(basePmode);
        await _source.UpdateSendingAsync(basePmode, originalName, cancellationToken);
    }

    /// <summary>
    ///     Update receiving pmode
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">
    ///     Exception thrown when a pmode with the supplied name
    ///     already exists.
    /// </exception>
    public async Task UpdateReceivingAsync(ReceivingBasePmode basePmode, string originalName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePmode.Name, nameof(basePmode.Name));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        if (basePmode.Name != originalName)
        {
            var newExists = await GetReceivingByNameAsync(basePmode.Name, cancellationToken);
            if (newExists != null)
            {
                throw new AlreadyExistsException($"BasePmode with {originalName} already exists");
            }
        }
        ValidateReceivingPmode(basePmode);
        await _source.UpdateReceivingAsync(basePmode, originalName, cancellationToken);
    }

    /// <summary>
    /// Validates the sending pmode.
    /// </summary>
    /// <param name="sendingPmode">The sending pmode.</param>
    /// <exception cref="InvalidPModeException">Invalid PMode</exception>
    private void ValidateSendingPmode(SendingBasePmode sendingPmode)
    {
        if (_disableValidation)
        {
            return;
        }

        var result = _sendingProcessingModeValidator.Validate(sendingPmode.Pmode!);
        if (!result.IsValid)
        {
            throw new InvalidPModeException("Invalid PMode", result);
        }
    }

    private void ValidateReceivingPmode(ReceivingBasePmode receivingPmode)
    {
        if (_disableValidation)
        {
            return;
        }

        var result = _receivingProcessingModeValidator.Validate(receivingPmode.Pmode!);
        if (!result.IsValid)
        {
            throw new InvalidPModeException("Invalid PMode", result);
        }
    }
}
