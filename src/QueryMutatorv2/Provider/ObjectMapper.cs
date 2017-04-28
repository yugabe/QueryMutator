using QueryMutatorv2.Mapable;
using QueryMutatorv2.Provider;
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
                        if (convention.Apply<TMap>(sourceProperty, mapProperty, cc)) { boundFlag = true; break; }


                    }
                    if (boundFlag) break;
                }



            }
            Object bindings;
            cc.storage.TryGetValue("Bindings", out bindings);
            memberBindings = memberBindings.Concat(bindings as List<MemberBinding>).ToList();

            return Expression.Lambda<Func<TSource, TMap>>(
              Expression.Condition(
                  Expression.Equal(parameter, Expression.Constant(null, sourceType)),
                  Expression.Constant(null, mapType),
                  Expression.MemberInit(Expression.New(mapType), memberBindings)
              ), parameter);
        }

        /// <summary>
        /// Generates mapping by iterating trough the source propertity, and checking if the 
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TMap"></typeparam>
        /// <param name="parameter"></param>
        /// <param name="memberBindings"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        private static Expression<Func<TSource, TMap>> GenerateMappingV2<TSource, TMap>(ParameterExpression parameter, List<MemberBinding> memberBindings, int depth = 0) where TMap : new()
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


                foreach (var convention in MappingProvider.CurrentConfiguration.Conventions)
                {
                    //workaround, for get propertites to work, would be better with refactoring apply to be template 

                    if (convention.Apply<TMap>(sourceProperty, mapType, cc)) break;
                }
            }
            Object bindings;
            cc.storage.TryGetValue("Bindings", out bindings);
            memberBindings = memberBindings.Concat(bindings as List<MemberBinding>).ToList();

            return Expression.Lambda<Func<TSource, TMap>>(
              Expression.Condition(
                  Expression.Equal(parameter, Expression.Constant(null, sourceType)),
                  Expression.Constant(null, mapType),
                  Expression.MemberInit(Expression.New(mapType), memberBindings)
              ), parameter);
        }


        public static TMap To<TSource, TMap>(this IMapable source) where TMap : new()
        {
            Type sourceType = source.Source.GetType(), mapType = typeof(TMap);

            Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower());
            var tmp1 = GenerateMapping<TSource, TMap>(Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()),
                 new List<MemberBinding>());
            System.Diagnostics.Debug.WriteLine(tmp1.ToString());
            var tmp2 = tmp1.Compile();
            System.Diagnostics.Debug.WriteLine(tmp2.ToString());
            TMap result = tmp2.Invoke((TSource)source.Source);
            System.Diagnostics.Debug.WriteLine(result.ToString());
            return result;


        }

        public static TMap ToV2<TSource, TMap>(this IMapable source) where TMap : new()
        {
            Type sourceType = source.Source.GetType(), mapType = typeof(TMap);

            Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower());
            var tmp1 = GenerateMappingV2<TSource, TMap>(Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()),
                 new List<MemberBinding>());
            System.Diagnostics.Debug.WriteLine(tmp1.ToString());
            var tmp2 = tmp1.Compile();
            System.Diagnostics.Debug.WriteLine(tmp2.ToString());
            TMap result = tmp2.Invoke((TSource)source.Source);
            System.Diagnostics.Debug.WriteLine(result.ToString());
            return result;


        }


        public static TMap To<TSource, TMap>(this TSource source, Expression Binding) where TMap : new() where TSource : IMapable<TSource>
        {
            throw new NotImplementedException();
        }

    }

}
