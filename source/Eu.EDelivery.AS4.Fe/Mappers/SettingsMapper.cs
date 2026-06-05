using Eu.EDelivery.AS4.Fe.Models;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Fe.Mappers;

public class SettingsMapper :
    IMapper<BaseSettings, Model.Internal.Settings>,
    IMapper<Model.Internal.Settings, BaseSettings>,
    IMapper<SettingsSubmit, Model.Internal.Settings>,
    IMapper<SettingsSubmit, SettingsSubmit>,
    IMapper<SettingsPullSend, Model.Internal.Settings>,
    IMapper<SettingsPullSend, SettingsPullSend>,
    IMapper<CustomSettings, Model.Internal.Settings>,
    IMapper<CustomSettings, CustomSettings>,
    IMapper<SettingsDatabase, SettingsDatabase>,
    IMapper<AgentSettings, AgentSettings>
{
    Model.Internal.Settings IMapper<BaseSettings, Model.Internal.Settings>.Map(BaseSettings source) => new()
    {
        IdFormat = source.IdFormat,
        RetentionPeriod = source.RetentionPeriod.ToString(),
        RetryReliability = source.RetryReliability,
        CertificateStore = source.CertificateStore,
    };

    BaseSettings IMapper<Model.Internal.Settings, BaseSettings>.Map(Model.Internal.Settings source) => new()
    {
        IdFormat = source.IdFormat,
        RetentionPeriod = Convert.ToInt32(source.RetentionPeriod),
        RetryReliability = source.RetryReliability,
        CertificateStore = source.CertificateStore,
    };

    Model.Internal.Settings IMapper<SettingsSubmit, Model.Internal.Settings>.Map(SettingsSubmit source) => new()
    {
        Submit = new SettingsSubmit
        {
            PayloadRetrievalPath = source.PayloadRetrievalPath,
        },
    };

    SettingsSubmit IMapper<SettingsSubmit, SettingsSubmit>.Map(SettingsSubmit source) => new()
    {
        PayloadRetrievalPath = source.PayloadRetrievalPath,
    };

    Model.Internal.Settings IMapper<SettingsPullSend, Model.Internal.Settings>.Map(SettingsPullSend source) => new()
    {
        PullSend = new SettingsPullSend
        {
            AuthorizationMapPath = source.AuthorizationMapPath,
        },
    };

    SettingsPullSend IMapper<SettingsPullSend, SettingsPullSend>.Map(SettingsPullSend source) => new()
    {
        AuthorizationMapPath = source.AuthorizationMapPath,
    };

    Model.Internal.Settings IMapper<CustomSettings, Model.Internal.Settings>.Map(CustomSettings source) => new()
    {
        CustomSettings = new CustomSettings
        {
            Setting = source.Setting,
        },
    };

    CustomSettings IMapper<CustomSettings, CustomSettings>.Map(CustomSettings source) => new()
    {
        Setting = source.Setting,
    };

    SettingsDatabase IMapper<SettingsDatabase, SettingsDatabase>.Map(SettingsDatabase source) => new()
    {
        Provider = source.Provider,
        ConnectionString = source.ConnectionString,
        StoreLocation = source.StoreLocation,
    };

    AgentSettings IMapper<AgentSettings, AgentSettings>.Map(AgentSettings source) => new()
    {
        Name = source.Name,
        Receiver = source.Receiver,
        Transformer = source.Transformer,
        StepConfiguration = source.StepConfiguration,
    };
}
