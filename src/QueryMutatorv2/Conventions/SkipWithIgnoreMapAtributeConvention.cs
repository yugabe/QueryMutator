using QueryMutatorv2.Conventions.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace QueryMutatorv2.Conventions
{
    public class SkipWithIgnoreMapAtributeConvention : IConvention
    {
        public bool Apply<TSource>(object Source, object Destination, ConventionContext Context) => ((PropertyInfo)Source).GetCustomAttribute<IgnoreMap>(true) != null;
    }
}
