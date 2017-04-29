using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace QueryMutatorv2.Conventions
{
    /// <summary>
    /// Convention which maps two  property with the same name to each other
    /// if the source is assignable to the destination property
    /// </summary>
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
                    if (!context.BoundedPropertys.ContainsKey(mapProperty.Name))
                    {
                        context.storage.TryGetValue("Bindings", out var bindings);
                        var typedBindings = bindings as List<MemberBinding>;
                        context.storage.TryGetValue("Parameter", out object parameter);
                        typedBindings.Add(Expression.Bind(mapProperty, Expression.PropertyOrField(parameter as ParameterExpression, sourceProperty.Name)));

                        context.BoundedPropertys.Add(mapProperty.Name, true);
                        return true;
                    }
                    else return false;
                }

            }






            return false;
        }
    }
}
