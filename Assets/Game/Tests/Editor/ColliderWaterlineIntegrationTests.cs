using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace GreatVoyage.Tests
{
    public sealed class ColliderWaterlineIntegrationTests
    {
        // The authoring tool lives in Unity's predefined Editor assembly; reflection avoids a cyclic assembly reference.
        private static object Invoke(string method)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GreatVoyage.Editor.ColliderWaterlineValidation",false)).FirstOrDefault(t=>t!=null);
            Assert.That(type,Is.Not.Null,"Collider authoring tools must be loaded.");
            try{return type.GetMethod(method,BindingFlags.Public|BindingFlags.Static).Invoke(null,null);}
            catch(TargetInvocationException ex){throw ex.InnerException??ex;}
        }
        private static bool Passed(object result)=>(bool)result.GetType().GetProperty("passed").GetValue(result);
        [Test] public void EveryInstalledColliderFamilyBakesWithoutMutatingOriginalPhysics()
        {
            var count=0;foreach(var result in (IEnumerable)Invoke("TestAllTypes"))
            {Assert.That(Passed(result),Is.True,result.GetType().GetProperty("type").GetValue(result)?.ToString());count++;}
            Assert.That(count,Is.EqualTo(8));
        }
        [Test] public void BuildValidationPreservesContactDefinitionsAndGameplay()=>Assert.That(Passed(Invoke("TestBuildStrip")),Is.True);
        [Test] public void CompositeContributionDeduplicationPreservesDifferenceHole()=>Assert.That(Passed(Invoke("TestCompositeSemantics")),Is.True);
    }
}
