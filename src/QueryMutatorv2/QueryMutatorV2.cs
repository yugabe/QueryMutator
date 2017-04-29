using QueryMutatorv2.Mapable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace QueryMutatorv2
{
    public static class QueryMutatorV2
    {
      


        public static IMapable<T> Map<T>(this T Source)
        {
            return new Mapable<T>(Source);
        }
        public static ProviderConfiguration CurrentConfiguration = new ProviderConfiguration();



    }
}