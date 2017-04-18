using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace QueryMutatorv2.Conventions
{
    public class AssignWithSameNameConvention : IConvention
    {
       
        public bool Apply(object Source, object Destination, ConventionContext Context)
        {
            var sourceProperty = (PropertyInfo) Source;
            var mapProperty = (PropertyInfo) Destination ;
            
            Console.WriteLine(sourceProperty.Name + "  "+ mapProperty.Name);
            if (sourceProperty.Name == mapProperty.Name &&
                mapProperty.GetType().GetTypeInfo().IsAssignableFrom(sourceProperty.GetType().GetTypeInfo())) {
                Object bindings;
                Context.storage.TryGetValue("Bindings", out bindings );
                var typedBindings = bindings as List<MemberBinding>;
                object parameter;
                Context.storage.TryGetValue("Parameter", out parameter);
                typedBindings.Add(Expression.Bind(mapProperty, Expression.PropertyOrField(parameter as ParameterExpression, sourceProperty.Name)));


                return true;
            }
            return false;
        }
    }
}
