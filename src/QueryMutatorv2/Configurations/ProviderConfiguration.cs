using QueryMutatorv2.Conventions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace QueryMutatorv2
{
    public class ProviderConfiguration
    {
        public bool AlwaysGenerateMapping { get; set; } = true;
        public bool GenerateMappingIfNotFound { get; set; } = true;
        public bool ThrowOnPropertyNotMappable { get; set; } = false;
        public List<IConvention> Conventions { get; set; } = new List<IConvention>();
        public int MaximumRecursionDepth { get; set; } = 50;
        internal Dictionary<Type, Dictionary<Type, Expression>> Mappings { get; } = new Dictionary<Type, Dictionary<Type, Expression>>();
        //public ConventionContext SharedContext=new ConventionContext();
    }
}
