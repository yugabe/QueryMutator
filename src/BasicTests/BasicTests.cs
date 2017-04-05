using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QueryMutatorv2;

namespace BasicTests
{
    public class Kutya
    {
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
        [TestMethod]
        public void CheckBasicMappingKeepsSimpleProperty()
        {
            var kutya = new Kutya { Name = "BidriBodri", Labak = 5 };
            var cica = kutya.Map().To<Cica>();
            Assert.AreEqual(kutya.Labak, cica.Labak);
            Assert.AreEqual(kutya.Name, cica.Name);
            kutya.Labak = 6;
            Assert.AreNotEqual(kutya.Labak, cica.Labak);
            Assert.AreEqual(kutya.Labak, cica.Labak);
        }
    }
}
