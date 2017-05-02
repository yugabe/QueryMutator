using QueryMutatorv2.Provider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace QueryMutatorv2.Conventions
{
    public class RecursiveMappingConvention : IConvention
    {
        public bool Apply<TMap>(object source, object destination, ConventionContext context)
        {
            var sourceProperty = (PropertyInfo)source;
            Type mapType = typeof(TMap);

            foreach (var mapProperty in mapType.GetRuntimeProperties())
            {
                if (sourceProperty.Name == mapProperty.Name &&
                mapProperty.PropertyType.GetTypeInfo().IsClass && sourceProperty.PropertyType.GetTypeInfo().IsClass
                && mapProperty.PropertyType.GetTypeInfo().GetConstructor(Type.EmptyTypes) != null)
                {
                    if (!context.BoundedPropertys.ContainsKey(mapProperty.Name))
                    {
                        MethodInfo method = typeof(MappingProvider).GetTypeInfo().GetMethod("GenerateMappingV2");
                        var ms = typeof(MappingProvider).GetTypeInfo().GetMethods();
                        MethodInfo generic = method.MakeGenericMethod(new Type[] { sourceProperty.PropertyType, mapProperty.PropertyType });
                        var param = Expression.Parameter(sourceProperty.PropertyType, sourceProperty.Name[0].ToString().ToLower());
                        var mapping = (LambdaExpression)generic.Invoke(null, new object[]
                        {
                            param,
                            new List<MemberBinding>(),
                            context.CurrentRecursionDepth+1
                        });
                        //????????????????????????????????
                        //var falseBranch = Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), new[] { collectionMapType },
                        //                    Expression.Call(typeof(Queryable), nameof(Queryable.Select), new[] { collectionSourceType, collectionMapType },
                        //                        Expression.Call(typeof(Queryable), nameof(Queryable.AsQueryable), new[] { collectionSourceType }, Expression.PropertyOrField(parameter, sourceProperty.Name)),
                        //                            Expression.Constant(selectedMapping)));
                        //????????????????????????????????
                        context.storage.TryGetValue("Bindings", out var bindings);
                   
                        var typedBindings = bindings as List<MemberBinding>;
                        context.storage.TryGetValue("Parameter", out object parameter  );
                        var typedParam = parameter as ParameterExpression;
                        
                        typedBindings.Add(
                               Expression.Bind(mapProperty,
                                   Expression.Condition(
                                       test: Expression.Equal(Expression.PropertyOrField(typedParam, sourceProperty.Name), Expression.Constant(null, sourceProperty.PropertyType)),
                                       ifTrue: Expression.Constant(null, mapping.ReturnType),
                                       ifFalse: mapping
                                       )
                                   )
                               );

                        return true;
                    }
                    else return false;
                }

            }

            return false;
        }
    }
}
