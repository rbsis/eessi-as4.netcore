using System.Net;
using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Models;
using Eu.EDelivery.AS4.Fe.Runtime;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.Settings;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Services.PullRequestAuthorization;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Transformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace Eu.EDelivery.AS4.Fe.Controllers;

/// <summary>
/// Controller to manage settings.xml
/// </summary>
/// <seealso cref="Controller" />
[Route("api/[controller]")]
public class ConfigurationController : Controller
{
    private readonly IAs4SettingsService _settingsService;
    private readonly IOptions<PortalSettings> _portalSettings;
    private readonly IPortalSettingsService _portalSettingsService;
    private readonly IRuntimeLoader _runtimeLoader;

    private readonly IDefaultAgentReceiverRegistry _defaultAgentReceiverRegistry;
    private readonly IDefaultAgentTransformerRegistry _defaultAgentTransformerRegistry;
    private readonly IDefaultAgentStepRegistry _defaultAgentStepRegistry;
    private readonly IPullAuthorizationMapProvider _pullAuthorizationMapProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationController" /> class.
    /// </summary>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="portalSettings">The portal settings.</param>
    /// <param name="portalSettingsService">The portal settings service.</param>
    /// <param name="runtimeLoader">The runtime loader.</param>
    /// <param name="defaultAgentReceiverRegistry">The default agent receiver registry.</param>
    /// <param name="defaultAgentTransformerRegistry"></param>
    /// <param name="defaultAgentStepRegistry"></param>
    /// <param name="pullAuthorizationMapProvider"></param>
    public ConfigurationController(
        IAs4SettingsService settingsService,
        IOptions<PortalSettings> portalSettings,
        IPortalSettingsService portalSettingsService,
        IRuntimeLoader runtimeLoader,
        IDefaultAgentReceiverRegistry defaultAgentReceiverRegistry,
        IDefaultAgentTransformerRegistry defaultAgentTransformerRegistry,
        IDefaultAgentStepRegistry defaultAgentStepRegistry,
        IPullAuthorizationMapProvider pullAuthorizationMapProvider)
    {
        _settingsService = settingsService;
        _portalSettings = portalSettings;
        _portalSettingsService = portalSettingsService;
        _runtimeLoader = runtimeLoader;
        _defaultAgentReceiverRegistry = defaultAgentReceiverRegistry;
        _defaultAgentTransformerRegistry = defaultAgentTransformerRegistry;
        _defaultAgentStepRegistry = defaultAgentStepRegistry;
        _pullAuthorizationMapProvider = pullAuthorizationMapProvider;
    }

    /// <summary>
    /// Returns if the portal is in setup state
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("setup")]
    [AllowAnonymous]
    public async Task<IActionResult> IsSetup() =>
        new OkObjectResult(await _portalSettingsService.IsSetup());

    /// <summary>
    /// Saves the setup.
    /// </summary>
    /// <param name="setup">The setup.</param>
    /// <returns></returns>
    [HttpPost]
    [Route("setup")]
    [AllowAnonymous]
    public async Task<IActionResult> SaveSetup([FromBody] Setup setup)
    {
        await _portalSettingsService.SaveSetup(setup);
        return new OkResult();
    }

    /// <summary>
    /// Posts the authorization map.
    /// </summary>
    /// <param name="authorizationEntries">The authorization entries.</param>
    /// <returns></returns>
    [HttpPost]
    [Route("authorizationmap")]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    public IActionResult PostAuthorizationMap([FromBody] IEnumerable<PullRequestAuthorizationEntry> authorizationEntries)
    {
        _pullAuthorizationMapProvider.SavePullRequestAuthorizationEntries(authorizationEntries);
        return new OkResult();
    }

    /// <summary>
    /// Gets the authorization map.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("authorizationmap")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Authorization map retrieved successfully", typeof(IEnumerable<PullRequestAuthorizationEntry>))]
    public IActionResult GetAuthorizationMap() =>
        new OkObjectResult(_pullAuthorizationMapProvider.GetPullRequestAuthorizationEntryOverview());

    /// <summary>
    /// Get settings
    /// </summary>
    /// <returns>Settings object</returns>
    [HttpGet]
    [SwaggerResponse((int)HttpStatusCode.OK, "Settings retrieved successfully", typeof(Model.Internal.Settings))]
    public async Task<Model.Internal.Settings> Get(CancellationToken cancellationToken) =>
        await _settingsService.GetSettingsAsync(cancellationToken);

    /// <summary>
    /// Gets the portal settings.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("portal")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Portal settings retrieved successfully", typeof(PortalSettings))]
    public PortalSettings GetRuntimeSettings() => _portalSettings.Value;

    /// <summary>
    /// Saves the portal settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns></returns>
    [HttpPost]
    [Route("portal")]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    public async Task<IActionResult> SavePortalSettings([FromBody] PortalSettings settings)
    {
        await _portalSettingsService.Save(settings);
        return new OkResult();
    }

    /// <summary>
    /// Save basic settings
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>OkResult</returns>
    [HttpPost]
    [Route("basesettings")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Base settings saved successfully", typeof(OkResult))]
    public async Task<IActionResult> SaveBaseSettings([FromBody] BaseSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        await _settingsService.SaveBaseSettingsAsync(settings, cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Save submit settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>OkResult</returns>
    [HttpPost]
    [Route("submitsettings")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Submit settings saved successfully", typeof(OkResult))]
    public async Task<IActionResult> SaveSubmitSettings([FromBody] SettingsSubmit settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        await _settingsService.SaveSubmitSettingsAsync(settings, cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Save pull send settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>OkResult</returns>
    [HttpPost]
    [Route("pullsendsettings")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Pull send settings saved successfully", typeof(OkResult))]
    public async Task<IActionResult> SavePullSendSettings([FromBody] SettingsPullSend settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        await _settingsService.SavePullSendSettingsAsync(settings, cancellationToken);
        return new OkResult();
    }
    /// <summary>
    /// Save custom settings
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>OkResult</returns>
    [HttpPost]
    [Route("customsettings")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Custom settings saved successfully", typeof(OkResult))]
    public async Task<IActionResult> SaveCustomSettings([FromBody] CustomSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        await _settingsService.SaveCustomSettingsAsync(settings, cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Saves the database settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>OkResult</returns>
    [HttpPost]
    [Route("databasesettings")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Database settings saved successfully", typeof(OkResult))]
    public async Task<IActionResult> SaveDatabaseSettings([FromBody] SettingsDatabase settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        await _settingsService.SaveDatabaseSettingsAsync(settings, cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Gets the default agent receiver.
    /// </summary>
    /// <param name="agentType">Type of the agent</param>
    /// <returns>Receiver for the requested agent type</returns>
    [HttpGet]
    [Route("defaultagentreceiver/{agentType}")]
    public IActionResult GetDefaultAgentReceiver(AgentType agentType) =>
        new OkObjectResult(_defaultAgentReceiverRegistry.GetDefaultReceiver(agentType));

    /// <summary>
    /// Gets the default agent steps.
    /// </summary>
    /// <param name="agentType">Type of the agent.</param>
    /// <returns>StepConfiguration for the requested agent type</returns>
    [HttpGet]
    [Route("defaultagentsteps/{agentType}")]
    public IActionResult GetDefaultAgentSteps(AgentType agentType)
    {
        var steps = _defaultAgentStepRegistry.GetDefaultStepConfiguration(agentType);
        IEnumerable<ItemType> FilterStepsFor(IEnumerable<Step> xs)
            => xs.Select(x => _runtimeLoader.Steps.First(s => s.TechnicalName == x.Type));

        return new OkObjectResult(new
        {
            NormalPipeline = FilterStepsFor(steps.NormalPipeline ?? []),
            ErrorPipeline = FilterStepsFor(steps.ErrorPipeline ?? [])
        });
    }

    /// <summary>
    /// Gets the default agent transformer.
    /// </summary>
    /// <param name="agentType">Type of the agent.</param>
    /// <returns></returns>
    [HttpGet]
    [Route("defaultagenttransformer/{agentType}")]
    public IActionResult GetDefaultAgentTransformer(AgentType agentType)
    {
        var defaultTransformer = _defaultAgentTransformerRegistry.GetDefaultTransformer(agentType);
        var otherTransformer = _defaultAgentTransformerRegistry.GetOtherTransformers(agentType);
        var availableTransformers = otherTransformer.Concat([defaultTransformer]);
        var types = _runtimeLoader.Transformers.Where(t => availableTransformers.Any(x => x.Type == t.TechnicalName));

        return new OkObjectResult(new
        {
            DefaultTransformer = types.First(t => t.TechnicalName == defaultTransformer.Type),
            OtherTransformers = types.Where(t => t.TechnicalName != defaultTransformer.Type)
        });
    }

    /// <summary>
    /// Create a submit agent
    /// </summary>
    /// <param name="settingsAgent">The submit agent agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>OkResult</returns>
    [HttpPost]
    [Route("submitagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Submit agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreateSubmitAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(
            settingsAgent,
            agents => agents?.SubmitAgents ?? [],
            (settings, agents) => settings.SubmitAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Delete a submit agent
    /// </summary>
    /// <param name="name">The name of the submit agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>OkResult</returns>
    [HttpDelete]
    [Route("submitagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Submit agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> DeleteSubmitAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.SubmitAgents ?? [],
            (settings, agents) => settings.SubmitAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Update an existing submit agent
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("submitagents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Submit agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> UpdateSubmitAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(
            settingsAgent,
            originalName,
            agents => agents?.SubmitAgents ?? [],
            (settings, agents) => settings.SubmitAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Updates the outbound processing agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("outboundprocessingagents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Outbound processing agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested outbound processing agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> UpdateOutboundProcessingAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(settingsAgent,
            originalName,
            agents => agents?.OutboundProcessingAgents ?? [],
            (settings, agents) => settings.OutboundProcessingAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Creates the outbound processing agent agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("outboundprocessingagent")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Outbound processing agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreateOutboundProcessingAgentAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(
            settingsAgent,
            agents => agents?.OutboundProcessingAgents ?? [],
            (settings, agents) => settings.OutboundProcessingAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Deletes the outbound processing agent agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("outboundprocessingagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Outbound processing agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested outbound processing agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> DeleteOutboundProcessingAgentAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.OutboundProcessingAgents ?? [],
            (settings, agents) => settings.OutboundProcessingAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Creates the forward agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("forwardagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Forward agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreateForwardAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(settingsAgent,
            agents => agents?.ForwardAgents ?? [],
            (settings, agents) => settings.ForwardAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Updates the forward agent.
    /// </summary>
    /// <param name="settingsAgent"></param>
    /// <param name="originalName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("forwardagents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Forward agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested forward agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> UpdateForwardAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(settingsAgent,
            originalName,
            agents => agents?.ForwardAgents ?? [],
            (settings, agents) => settings.ForwardAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Deletes the forward agent.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("forwardagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Forward agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> DeleteForwardAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.ForwardAgents ?? [],
            (settings, agents) => settings.ForwardAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Creates the send agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("sendagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Send agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreateSendAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(settingsAgent,
            agents => agents?.SendAgents ?? [],
            (settings, agents) => settings.SendAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Deletes the send agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("sendagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Send agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> DeleteSendAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.SendAgents ?? [],
            (settings, agents) => settings.SendAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Updates the send agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("sendagents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Send agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested send agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> UpdateSendAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(settingsAgent,
            originalName,
            agents => agents?.SendAgents ?? [],
            (settings, agents) => settings.SendAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Creates the receive agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("receiveagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Receive agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreateReceiveAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(settingsAgent,
            agents => agents?.ReceiveAgents ?? [],
            (settings, agents) => settings.ReceiveAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Deletes the receive agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("receiveagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Receive agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested receive agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> DeleteReceiveAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.ReceiveAgents ?? [],
            (settings, agents) => settings.ReceiveAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Updates the receive agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("receiveagents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Receive agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested receive agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> UpdateReceiveAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(settingsAgent,
            originalName, agents => agents?.ReceiveAgents ?? [],
            (settings, agents) => settings.ReceiveAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Creates the deliver agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("deliveragents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Deliver agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreateDeliverAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(settingsAgent,
            agents => agents?.DeliverAgents ?? [],
            (settings, agents) => settings.DeliverAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Deletes the deliver agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("deliveragents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Deliver agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task DeleteDeliverAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.DeliverAgents ?? [],
            (settings, agents) => settings.DeliverAgents = agents,
            cancellationToken);
    }

    /// <summary>
    /// Updates the deliver agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("deliveragents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Deliver agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> UpdateDeliverAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(settingsAgent,
            originalName,
            agents => agents?.DeliverAgents ?? [],
            (settings, agents) => settings.DeliverAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Creates the notify agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("notifyagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Notify agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreateNotifyConsumerAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(settingsAgent,
            agents => agents?.NotifyAgents ?? [],
            (settings, agents) => settings.NotifyAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Deletes the notify agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("notifyagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Notify agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> DeleteNotifyConsumerAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.NotifyAgents ?? [],
            (settings, agents) => settings.NotifyAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Updates the notify agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("notifyagents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Notify agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> UpdateNotifyConsumerAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(settingsAgent,
            originalName,
            agents => agents?.NotifyAgents ?? [],
            (settings, agents) => settings.NotifyAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Creates the pull receive agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("pullreceiveagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Pull receive agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreatePullReceiveAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(settingsAgent,
            agents => agents?.PullReceiveAgents ?? [],
            (settings, agents) => settings.PullReceiveAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Deletes the pull receive agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("pullreceiveagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Pull receive agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> DeletePullReceiveAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.PullReceiveAgents ?? [],
            (settings, agents) => settings.PullReceiveAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Updates the pull receive agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("pullreceiveagents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Pull receive agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> UpdatePullReceiveAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(settingsAgent,
            originalName,
            agents => agents?.PullReceiveAgents ?? [],
            (settings, agents) => settings.PullReceiveAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Creates the pull send agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("pullsendagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Pull send agent created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task<IActionResult> CreatePullSendAgent([FromBody] AgentSettings settingsAgent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        await _settingsService.CreateAgentAsync(settingsAgent,
            agents => agents?.PullSendAgents ?? [],
            (settings, agents) => settings.PullSendAgents = agents,
            cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Deletes the pull send agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("pullsendagents")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Pull send agent deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task DeletePullSendAgent(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        await _settingsService.DeleteAgentAsync(name,
            agents => agents?.PullSendAgents ?? [],
            (settings, agents) => settings.PullSendAgents = agents,
            cancellationToken);
    }

    /// <summary>
    /// Updates the pull send agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("pullsendagents/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Pull send agent updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested submit agent doesn't exist", typeof(ErrorModel))]
    public async Task UpdatePullSendAgent([FromBody] AgentSettings settingsAgent, string originalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settingsAgent, nameof(settingsAgent));
        ArgumentException.ThrowIfNullOrEmpty(originalName, nameof(originalName));
        await _settingsService.UpdateAgentAsync(settingsAgent,
            originalName,
            agents => agents?.PullSendAgents ?? [],
            (settings, agents) => settings.PullSendAgents = agents,
            cancellationToken);
    }
}
