using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace QueryMutatorv2.Provider
{
    public static class MappingProvider
    {
        public static ProviderConfiguration CurrentConfiguration=new ProviderConfiguration();


        private static readonly object syncRoot = new object();

        public static Expression<Func<TSource, TMap>> GetMapping<TSource, TMap>(bool checkCollectionsForNull) where TMap : new()
        {
            var sourceType = typeof(TSource);
            var mapType = typeof(TMap);
            if (!CurrentConfiguration.Mappings.TryGetValue(sourceType, out Dictionary<Type, Expression> mappings))
            {
                lock (syncRoot)
                {
                    if (!CurrentConfiguration.Mappings.TryGetValue(sourceType, out mappings))
                        CurrentConfiguration.Mappings[sourceType] = mappings = new Dictionary<Type, Expression>();
                }
            }

            if (!mappings.TryGetValue(mapType, out Expression expression)
                && (!CurrentConfiguration.Mappings.Any(m => m.Key.GetTypeInfo().IsAssignableFrom(sourceType.GetTypeInfo())
                    && (expression = m.Value.FirstOrDefault(em => em.Key.GetTypeInfo().IsAssignableFrom(mapType.GetTypeInfo())).Value) != null))
                && !CurrentConfiguration.GenerateMappingIfNotFound)
                throw new InvalidOperationException($"No mapping was specified between the source type {sourceType} for type {mapType}. Use the {nameof(RegisterMapping)} method to register or replace a static mapping expression.");

            var mapping = expression as Expression<Func<TSource, TMap>>;

            if (mapping == null)
            {
                if (!CurrentConfiguration.GenerateMappingIfNotFound)
                    throw new InvalidOperationException($"The provided mapping between the source type {sourceType}" +
                        $" for type {mapType} was not the correct type registered." +
                        $" Use the {nameof(RegisterMapping)} method to register or replace a mapping expression.");

                return GenerateMapping<TSource, TMap>(Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()),
                    new List<MemberBinding>(), checkCollectionsForNull, 0);
            }

            return mapping;
        }

        /// <summary>
        /// Used internally to register a mapping between the given types using the 
        /// <see cref="GenerateMapping{TSource, TMap}(ParameterExpression, List{MemberBinding}, int)"/> method.
        /// </summary>
        /// <param name="sourceType">The source type to map the properties from.</param>
        /// <param name="mappingType">The mapping type to map the properties to.</param>
        /// <param name="checkCollectionsForNull">Indicates whether to generate conditional null mapping for collections in the given type.</param>
        /// <param name="depth">This parameter indicates the possible recursion depth which might be used to detect a possible infinite recursion
        /// to prevent a <see cref="StackOverflowException"/> from occuring.</param>
        /// <returns>The <see cref="Expression"/> which is stored in the <see cref="StaticMappings"/> dictionary after generation.</returns>
        private static Expression RegisterMappingInternal(Type sourceType, Type mappingType, bool checkCollectionsForNull, int depth = 0)
        {
            if (depth > CurrentConfiguration.MaximumRecursionDepth && CurrentConfiguration.MaximumRecursionDepth > 1)
                throw new InvalidOperationException($"The mapping between {sourceType} and {mappingType} seems to have caused an infinite recursion of mappings.");

            if (!CurrentConfiguration.Mappings.TryGetValue(sourceType, out Dictionary<Type, Expression> mappings))
            {
                lock (syncRoot)
                {
                    if (!CurrentConfiguration.Mappings.TryGetValue(sourceType, out mappings))
                        CurrentConfiguration.Mappings[sourceType] = mappings = new Dictionary<Type, Expression>();
                }
            }

            if (mappingType.GetTypeInfo().DeclaredConstructors.Any(c => c.IsPublic && c.GetParameters().Length == 0))
            {
                return mappings[mappingType] = typeof(MappingProvider).GetTypeInfo()
                    .DeclaredMethods.First(m => m.IsStatic && !m.IsPublic && m.Name == nameof(MappingProvider.GenerateMapping))
                    .MakeGenericMethod(sourceType, mappingType)
                    .Invoke(null, new object[] { Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()), new List<MemberBinding>(), checkCollectionsForNull, depth + 1 }) as Expression;
            }

            return null;
        }


        /// <summary>
        /// Helper method to generate a <see cref="MemberAssignment"/> <see cref="Expression"/> in the following form: 
        /// <c>mapProperty = new [] { source.sourceProperty }.AsQueryable().Select(mapping).FirstOrDefault()</c>.
        /// This way a single property can be mapped using the queryable expression mapping selector.
        /// </summary>
        /// <param name="parameter">The input parameter being bound for the surrounding <see cref="LambdaExpression"/>.</param>
        /// <param name="sourceProperty">The property to map from using the provided mapping.</param>
        /// <param name="mapProperty">The property to map to using the provided mapping.</param>
        /// <param name="mapping">The mapping used to map the <paramref name="sourceProperty"/> to the <paramref name="mapProperty"/>.</param>
        /// <returns>The <see cref="MemberAssignment"/> <see cref="Expression"/> that can be used in an object initialization expression.</returns>
        private static MemberAssignment GenerateQueryableBinding(ParameterExpression parameter, PropertyInfo sourceProperty, PropertyInfo mapProperty, Expression mapping)
            => Expression.Bind(mapProperty,
                Expression.Call(typeof(Queryable), nameof(Queryable.FirstOrDefault), new[] { mapProperty.PropertyType },
                    Expression.Call(typeof(Queryable), nameof(Queryable.Select), new[] { sourceProperty.PropertyType, mapProperty.PropertyType },
                        Expression.Call(typeof(Queryable), nameof(Queryable.AsQueryable), new[] { sourceProperty.PropertyType }, Expression.NewArrayInit(sourceProperty.PropertyType, Expression.PropertyOrField(parameter, sourceProperty.Name))),
                            Expression.Constant(mapping))));


        /// <summary>
        /// Automatically register a mapping between the source and map types.
        /// </summary>
        /// <typeparam name="TSource">The source type to automatically map the properties from.</typeparam>
        /// <typeparam name="TMap">The map type to automatically map the properties to.</typeparam>
        /// <param name="checkCollectionsForNull">Indicates whether to generate conditional null mapping for collections in the given type.</param>
        public static void RegisterMapping<TSource, TMap>(bool checkCollectionsForNull) where TMap : new() => RegisterMappingInternal(typeof(TSource), typeof(TMap), checkCollectionsForNull);



        /// <summary>
        /// Generates a mapping dynamically between the given type parameters.
        /// </summary>
        /// <typeparam name="TSource">The source type to map properties from to the target (map) type.</typeparam>
        /// <typeparam name="TMap">The map type's properties will be populated from the source type and additional mappings.</typeparam>
        /// <param name="parameter">The parameter included in the <see cref="LambdaExpression"/> which is used to generate the mapping delegate 
        /// expression.</param>
        /// <param name="memberBindings">The initial bindings to use when building the property mappings.</param>
        /// <param name="checkCollectionsForNull">Indicates whether to generate conditional null mapping for collections in the given type.</param>
        /// <param name="depth">This parameter indicates the possible recursion depth which might be used to detect a possible
        /// infinite recursion to prevent a <see cref="StackOverflowException"/> from occuring.</param>
        /// <returns>The generated mapping between the <typeparamref name="TSource"/> and <typeparamref name="TMap"/> types.</returns>
        private static Expression<Func<TSource, TMap>> GenerateMapping<TSource, TMap>(ParameterExpression parameter, List<MemberBinding> memberBindings, bool checkCollectionsForNull, int depth = 0) where TMap : new()
        {
            var sourceType = typeof(TSource);
            var mapType = typeof(TMap);

            // Temporary variables used in the main loop.
            Type collectionSourceType = null, collectionMapType = null;
            Expression selectedMapping = null;

            // The property names which are already bound in the memberBindings parameter.
            var boundProperties = new HashSet<string>(memberBindings.Select(m => m.Member.Name));

            // Iterate over the source's properties.
            foreach (var sourceProperty in sourceType.GetRuntimeProperties().Where(p => !boundProperties.Contains(p.Name)))
            {
                var mapProperty = mapType.GetRuntimeProperty(sourceProperty.Name);
                if (mapProperty == null) // There is no matching property in the mapping object.
                    continue;

                if (mapProperty.PropertyType.GetTypeInfo().IsAssignableFrom(sourceProperty.PropertyType.GetTypeInfo()))
                {
                    // The source property can simply be assigned to the mapped property.
                    memberBindings.Add(Expression.Bind(mapProperty, Expression.PropertyOrField(parameter, sourceProperty.Name)));
                }
                else if (CurrentConfiguration.Mappings.Any(m => m.Key.GetTypeInfo().IsAssignableFrom(sourceProperty.PropertyType.GetTypeInfo()) && (selectedMapping = m.Value.FirstOrDefault(em => em.Key.GetTypeInfo().IsAssignableFrom(mapProperty.PropertyType.GetTypeInfo())).Value) != null))
                {
                    // There is already a mapping registered between the source and mapped property types, so reuse that.
                    memberBindings.Add(GenerateQueryableBinding(parameter, sourceProperty, mapProperty, selectedMapping));
                }
                else if (
                    sourceProperty.PropertyType.GetTypeInfo().ImplementedInterfaces.Concat(new[] { sourceProperty.PropertyType }).Any(i => i.GetTypeInfo().IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICollection<>) || i.GetGenericTypeDefinition() == typeof(IQueryable<>)) && (collectionSourceType = i.GenericTypeArguments.FirstOrDefault()) != null) &&
                    mapProperty.PropertyType.GetTypeInfo().ImplementedInterfaces.Concat(new[] { mapProperty.PropertyType }).Any(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>) && (collectionMapType = i.GenericTypeArguments.FirstOrDefault()) != null) &&
                    mapProperty.PropertyType.GetTypeInfo().IsAssignableFrom(typeof(List<>).MakeGenericType(collectionMapType).GetTypeInfo())
                    ) {
                    //{
                    //    // If it is a collection mapping that is not assignable because of variance, try explicit mapping between the two using already registered static mappings or try to generate them. Watch out for infinite recursion.
                    //    if (CurrentConfiguration.Mappings.Any(m => m.Key.GetTypeInfo().IsAssignableFrom(collectionSourceType.GetTypeInfo()) && (selectedMapping = m.Value.FirstOrDefault(em => em.Key.GetTypeInfo().IsAssignableFrom(collectionMapType.GetTypeInfo())).Value) != null) || (CurrentConfiguration.GenerateMappingIfNotFound && (selectedMapping = RegisterMappingInternal(collectionSourceType, collectionMapType, checkCollectionsForNull, depth + 1)) != null))
                    //    {
                    //        var falseBranch = Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), new[] { collectionMapType },
                    //                        Expression.Call(typeof(Queryable), nameof(Queryable.Select), new[] { collectionSourceType, collectionMapType },
                    //                            Expression.Call(typeof(Queryable), nameof(Queryable.AsQueryable), new[] { collectionSourceType }, Expression.PropertyOrField(parameter, sourceProperty.Name)),
                    //                                Expression.Constant(selectedMapping)));

                    //        if (checkCollectionsForNull)
                    //        {
                    //            // Generates the mapping between the two properties in the following form: mapProperty = source.sourceProperty == null ? null : source.sourceProperty.AsQueryable().Select(mapping).ToList().
                    //            memberBindings.Add(
                    //                Expression.Bind(mapProperty,
                    //                    Expression.Condition(
                    //                        test: Expression.Equal(Expression.PropertyOrField(parameter, sourceProperty.Name), Expression.Constant(null, sourceProperty.PropertyType)),
                    //                        ifTrue: Expression.Constant(null, falseBranch.Method.ReturnType),
                    //                        ifFalse: falseBranch
                    //                        )
                    //                    )
                    //                );
                    //        }
                    //        else
                    //        {
                    //            // Generates the mapping between the two properties in the following form: mapProperty = source.sourceProperty.AsQueryable().Select(mapping).ToList().
                    //            memberBindings.Add(Expression.Bind(mapProperty, falseBranch));
                    //        }
                    //    }
                    //}
                }
                else if (CurrentConfiguration.GenerateMappingIfNotFound)
                {
                    // No mapping is found, but the name matches. Try generating the mapping manually between the two.
                    selectedMapping = RegisterMappingInternal(sourceProperty.PropertyType, mapProperty.PropertyType, checkCollectionsForNull, depth + 1);
                    if (selectedMapping != null)
                        memberBindings.Add(GenerateQueryableBinding(parameter, sourceProperty, mapProperty, selectedMapping));
                }
                else if (CurrentConfiguration.ThrowOnPropertyNotMappable)
                {
                    // If no mapping is found and could not generate a static mapping, throw if the appropriate setting is set to true.
                    throw new InvalidOperationException($"The property {mapProperty.Name} on type {mapType.Name} " +
                        $"cannot be mapped from property {sourceProperty} on type {sourceType}." +
                        $" To suppress this exception, set the {nameof(MappingProvider)}.{nameof(CurrentConfiguration)}.{nameof(CurrentConfiguration.ThrowOnPropertyNotMappable)} property value to false.");
                }
            }

            // Create a lambda expression in the following form which is used to map a source element to a target element: 
            // source => source == null ? null : new TMap() { MapProperty = source.MapProperty, ... };
            return Expression.Lambda<Func<TSource, TMap>>(
                Expression.Condition(
                    Expression.Equal(parameter, Expression.Constant(null, sourceType)),
                    Expression.Constant(null, mapType),
                    Expression.MemberInit(Expression.New(mapType), memberBindings)
                ), parameter);
        }


    }
}
