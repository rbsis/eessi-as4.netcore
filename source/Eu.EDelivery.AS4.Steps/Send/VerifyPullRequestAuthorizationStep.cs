using System.ComponentModel;
using System.Security;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services.PullRequestAuthorization;

namespace Eu.EDelivery.AS4.Steps.Send;

[Info("Verify pull request authorization")]
[Description("Verifies if the received PullRequest is authorized")]
public class VerifyPullRequestAuthorizationStep : IStep
{
    private readonly IPullAuthorizationMapService _pullAuthorizationMapService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerifyPullRequestAuthorizationStep" /> class.
    /// </summary>
    ///<param name="pullAuthorizationMapService">The IPullAuthorizationMapService instance that must be used</param>
    public VerifyPullRequestAuthorizationStep(IPullAuthorizationMapService pullAuthorizationMapService)
    {
        _pullAuthorizationMapService = pullAuthorizationMapService;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext" />.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext.AS4Message);

        if (_pullAuthorizationMapService.IsPullRequestAuthorized(messagingContext.AS4Message))
        {
            return StepResult.SuccessAsync(messagingContext);
        }

        var mpc = (messagingContext.AS4Message.FirstSignalMessage as PullRequest)?.Mpc ?? string.Empty;
        throw new SecurityException(
            $"{messagingContext.LogTag} PullRequest for MPC {mpc} is not authorized. " +
            "Either change the PullRequest MPC or add the MPC value to the authorization map");
    }
}
