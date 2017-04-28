using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace QueryMutatorv2.Conventions
{
    public class AssignWithSameNameConvention2 : IConvention
    {

        public bool Apply<TMap>(object source, object destination, ConventionContext context)
        {
            var sourceProperty = (PropertyInfo)source;
            Type mapType = typeof(TMap);

            var a = mapType.GetRuntimeProperties();

            foreach (var mapProperty in mapType.GetRuntimeProperties())
            {
                if (sourceProperty.Name == mapProperty.Name &&
                mapProperty.GetType().GetTypeInfo().IsAssignableFrom(sourceProperty.GetType().GetTypeInfo()))
                {
                    context.storage.TryGetValue("Bindings", out var bindings);
                    var typedBindings = bindings as List<MemberBinding>;
                    context.storage.TryGetValue("Parameter", out object parameter);
                    typedBindings.Add(Expression.Bind(mapProperty, Expression.PropertyOrField(parameter as ParameterExpression, sourceProperty.Name)));


                    return true;
                }

            }






            return false;
        }
    }
}
