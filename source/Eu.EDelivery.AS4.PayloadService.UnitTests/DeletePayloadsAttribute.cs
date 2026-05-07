using System.Reflection;
using Xunit.v3;

namespace Eu.EDelivery.AS4.PayloadService.UnitTests;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
internal class DeletePayloadsAttribute(string folderName) : BeforeAfterTestAttribute
{
    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(folderName, "Payloads")))
        {
            try
            {
                File.Delete(file);
            }
            catch { }
        }
    }
}
