using QueryMutatorv2.Mapable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace QueryMutatorv2
{
    public static class ObjectMapper
    {

        private static Expression<Func<TSource, TMap>> GenerateMapping<TSource, TMap>(ParameterExpression parameter, List<MemberBinding> memberBindings, int depth = 0) where TMap : new()
        {
            var sourceType = typeof(TSource);
            var mapType = typeof(TMap);



            // The property names which are already bound in the memberBindings parameter.
            var boundProperties = new HashSet<string>(memberBindings.Select(m => m.Member.Name));
            var cc = new ConventionContext();
            cc.storage.Add("Parameter", parameter);
            // Iterate over the source's properties.
            foreach (var sourceProperty in sourceType.GetRuntimeProperties().Where(p => p.CanRead && !boundProperties.Contains(p.Name)))
            {

                foreach (var mapProperty in mapType.GetRuntimeProperties())
                {
                    var boundFlag = false;
                    foreach (var convention in MappingProvider.CurrentConfiguration.Conventions)
                    {
                        if (convention.Apply(sourceProperty, mapProperty, cc)) { boundFlag = true; break; }
                        

                    }
                    if (boundFlag) break;
                }



            }
            Object bindings;
            cc.storage.TryGetValue("Binding", out bindings);
            memberBindings=memberBindings.Concat(bindings as List<MemberBinding>).ToList();
            return Expression.Lambda<Func<TSource, TMap>>(
              Expression.Condition(
                  Expression.Equal(parameter, Expression.Constant(null, sourceType)),
                  Expression.Constant(null, mapType),
                  Expression.MemberInit(Expression.New(mapType), memberBindings)
              ), parameter);
        }
        public static TMap To<TMap>(this IMapable source) where TMap : new()
        {
            Type sourceType = source.Source.GetType(), mapType = typeof(TMap);

            Expression.Parameter(typeof(TSource), typeof(TSource).Name[0].ToString().ToLower());
            return  GenerateMapping<TSource, TMap>(Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()),
                 new List<MemberBinding>()).Compile().Invoke(source.Source);
            
        }
        public static TMap To<TSource, TMap>(this TSource source, Expression Binding) where TMap : new() where TSource : IMapable<TSource>
        {
            throw new NotImplementedException();
        }

    }

}
