using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QueryMutatorv2;
using QueryMutatorv2.Provider;
using QueryMutatorv2.Conventions.Attributes;
using QueryMutatorv2.Conventions;
using System.Collections.Generic;

namespace BasicTests
{
    public class Kutya
    {
        [IgnoreMap]
        public string Name { get; set; }
        public int Labak { get; set; }
    }
    public class Cica
    {
        public string Name { get; set; }
        public int Labak { get; set; }
    }

    [TestClass]
    public class BasicTests
    {
        public void ResetConventions() {
            MappingProvider.CurrentConfiguration.Conventions = new List<IConvention>();
        }
        [TestMethod]
        public void CheckBasicMappingKeepsSimpleProperty()
        {
            ResetConventions();
            MappingProvider.CurrentConfiguration.Conventions.Add(new QueryMutatorv2.Conventions.AssignWithSameNameConvention());
            var kutya = new Kutya { Name = "BidriBodri", Labak = 5 };
            var cica = kutya.Map().To<Kutya,Cica>();
            Assert.AreEqual(kutya.Labak, cica.Labak);
            Assert.AreEqual(kutya.Name, cica.Name);
            kutya.Labak = 6;
            Assert.AreNotEqual(kutya.Labak, cica.Labak);
       
        }


        [TestMethod]
        public void CheckIgnoreMapConvention()
        {
            ResetConventions();
            MappingProvider.CurrentConfiguration.Conventions.Add(new QueryMutatorv2.Conventions.SkipWithIgnoreMapAtributeConvention());
            MappingProvider.CurrentConfiguration.Conventions.Add(new QueryMutatorv2.Conventions.AssignWithSameNameConvention());
            var kutya = new Kutya { Name = "BidriBodri", Labak = 5 };
            var cica = kutya.Map().To<Kutya, Cica>();
            Assert.AreEqual(kutya.Labak, cica.Labak);
            Assert.AreNotEqual(kutya.Name, cica.Name);
            kutya.Labak = 6;
            Assert.AreNotEqual(kutya.Labak, cica.Labak);

        }
    }
}
