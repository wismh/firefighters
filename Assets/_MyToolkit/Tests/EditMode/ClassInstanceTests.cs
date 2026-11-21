using MyToolkit.SourceGenerator.Editor;
using NUnit.Framework;

namespace MyToolkit.Tests.SourceGenerator
{
    public sealed class ClassInstanceTests
    {
        [Test]
        public void GetString_EmitsNamespaceClassAndConstFields()
        {
            string code = new ClassInstance()
                .AddNamespace("Demo.Generated")
                .SetPublic()
                .SetStatic()
                .SetName("Scenes")
                .AddField(new FieldInstance()
                    .SetPublic()
                    .SetConst()
                    .SetStringType()
                    .SetName("Main")
                    .SetAssignedValue("\"MainScene\""))
                .GetString();

            StringAssert.Contains("namespace Demo.Generated", code);
            StringAssert.Contains("public static class Scenes", code);
            StringAssert.Contains("public const System.String Main", code);
            StringAssert.Contains("\"MainScene\"", code);
        }

        [Test]
        public void GetString_IsDeterministic()
        {
            ClassInstance instance = new ClassInstance().SetPublic().SetName("Same");

            Assert.AreEqual(instance.GetString(), instance.GetString());
        }

        [Test]
        public void Copy_IsIndependentFromOriginal()
        {
            ClassInstance original = new ClassInstance().SetPublic().SetName("Original");
            ClassInstance copy = original.Copy().SetName("Copy");

            StringAssert.Contains("Original", original.GetString());
            StringAssert.DoesNotContain("Copy", original.GetString());
            StringAssert.Contains("Copy", copy.GetString());
        }
    }
}
