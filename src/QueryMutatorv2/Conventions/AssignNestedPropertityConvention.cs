using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace QueryMutatorv2.Conventions
{
    public class AssignNestedPropertityWithNameAsPath : IConvention
    {

        public bool Apply<TMap>(object source, object destination, ConventionContext context)
        {
            var sourceProperty = (PropertyInfo)source;
            Type mapType = typeof(TMap);
            bool success = false;
            var a = mapType.GetRuntimeProperties();

            foreach (var mapProperty in mapType.GetRuntimeProperties())
            {
                TypeInfo ti = sourceProperty.PropertyType.GetTypeInfo();
                Boolean test1 = !(ti.IsPrimitive || mapProperty.PropertyType == typeof(string));
                Boolean test2 = sourceProperty.Name.ToLower().StartsWith(mapProperty.Name.ToLower());
                if (!(ti.IsPrimitive || sourceProperty.PropertyType == typeof(string)) && mapProperty.Name.ToLower().StartsWith(sourceProperty.Name.ToLower()))
                {
                    var propertyName = mapProperty.Name.Substring(sourceProperty.Name.Length);
                    var nestedSourceProperty=sourceProperty.PropertyType.GetRuntimeProperty(propertyName);
                    Boolean test3 = nestedSourceProperty != null;
                    var test5 = mapProperty.PropertyType.GetTypeInfo();
                    Boolean test4 = mapProperty.PropertyType.GetTypeInfo().IsAssignableFrom(nestedSourceProperty.PropertyType.GetTypeInfo());
                    if (nestedSourceProperty!=null &&
                        mapProperty.PropertyType.GetTypeInfo().IsAssignableFrom(nestedSourceProperty.PropertyType.GetTypeInfo()))
                    {
                        context.storage.TryGetValue("Bindings", out var bindings);
                        var typedBindings = bindings as List<MemberBinding>;
                        context.storage.TryGetValue("Parameter", out object parameter);
                        typedBindings.Add(Expression.Bind(mapProperty,Expression.PropertyOrField( Expression.PropertyOrField(parameter as ParameterExpression, sourceProperty.Name), nestedSourceProperty.Name)));
                        success = true;


                    }


                }

            }






            return success;
        }
    }
}
