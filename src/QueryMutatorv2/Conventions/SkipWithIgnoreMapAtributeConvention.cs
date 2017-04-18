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
        public bool Apply(object Source, object Destination, ConventionContext Context)
        {
              var sourceProperty = (PropertyInfo)Source;
              var atributeMap = sourceProperty.GetCustomAttributes(false).ToDictionary(a => a.GetType().Name, a => a);
            if (atributeMap.Any(a => a.Key == "IgnoreMap")) return true;

            return false;
         
        }

    }
}
