using System.Reflection;
using Xunit.Sdk;

namespace Eu.EDelivery.AS4.PayloadService.UnitTests;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
internal class DeletePayloadsAttribute : BeforeAfterTestAttribute
{
    public override void After(MethodInfo methodUnderTest)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine("Payloads")))
        {
            File.Delete(file);
        }
    }
}
