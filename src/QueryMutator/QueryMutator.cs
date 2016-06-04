using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
#if NETFX451 || NETFX46 || NET461
using System.Runtime.InteropServices;
#endif

#region Assembly information

[assembly: AssemblyTitle("QueryMutator")]
[assembly: AssemblyDescription("Queryable and Enumerable extensions for automapping objects and mapping multiple expressions into one.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("QueryMutator")]
[assembly: AssemblyCopyright("Copyright ©  2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
#if NETFX451 || NETFX46 || NET461
[assembly: ComVisible(false)]
[assembly: Guid("473fa649-78e3-4fa0-ab8e-b13a5f5d951d")]
#endif

#endregion

namespace System.Linq
{
    /// <summary>
    /// This class provides extensions and management features for dynamically mapping a type to another, and optionally merge object initializer expressions.
    /// </summary>
    public static class QueryMutator
    {

#region Configuration, static mappings

        /// <summary>
        /// The configration type for <see cref="QueryMutator"/>. Use this class to store references to the registered mapping and other configuration properties.
        /// </summary>
        public class Configuration
        {
            /// <summary>
            /// The generated ID for this <see cref="Configuration"/> object.
            /// </summary>
            public Guid Id { get; } = Guid.NewGuid();

            /// <summary>
            /// The stored static mappings. The dictionary contains the source types (TSource) as the keys and the stored mappings as another dictionary, which 
            /// holds the generated or registered <see cref="Expression"/> mapping (which is an <see cref="Expression{Func{TSource, TMap}}"/> type with the &lt;TSource&gt; 
            /// and &lt;TMap&gt; generic type parameters) as the value for the &lt;TMap&gt; key.
            /// </summary>
            internal Dictionary<Type, Dictionary<Type, Expression>> Mappings { get; } = new Dictionary<Type, Dictionary<Type, Expression>>();

            /// <summary>
            /// Setting this property to true generates a missing mapping on demand (at runtime) instead of throwing an <see cref="InvalidOperationException"/> when the 
            /// missing mapping is not found.
            /// The default value is true.
            /// </summary>
            public bool GenerateMappingIfNotFound { get; set; } = true;

            /// <summary>
            /// Setting this property to true indicates whether to throw an <see cref="InvalidOperationException"/> at runtime when a property on the type to be mapped cannot be mapped.
            /// The default value is false.
            /// </summary>
            public bool ThrowOnPropertyNotMappable { get; set; } = false;

            /// <summary>
            /// When a mapping tree is being recursively transversed, reaching this depth throws an exception. Setting this value to less than 1 disables checking for infinite recursion.
            /// The default value is 50.
            /// </summary>
            public int MaximumRecursionDepth { get; set; } = 50;
        }

        /// <summary>
        /// The <see cref="Configuration"/> object used by the extensions and static methods.
        /// </summary>
        public static Configuration CurrentConfiguration { get; set; } = new Configuration();

        /// <summary>
        /// A shorthand accessor for the <see cref="CurrentConfiguration"/> object's <see cref="Configuration.Mappings"/> property.
        /// </summary>
        private static Dictionary<Type, Dictionary<Type, Expression>> StaticMappings => CurrentConfiguration.Mappings;

#region Mapping generation and retrieval

        /// <summary>
        /// Helper method to generate a <see cref="MemberAssignment"/> <see cref="Expression"/> in the following form: <c>mapProperty = new [] { source.sourceProperty }.AsQueryable().Select(mapping).FirstOrDefault()</c>.
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
        /// Generates a mapping dynamically between the given type parameters.
        /// </summary>
        /// <typeparam name="TSource">The source type to map properties from to the target (map) type.</typeparam>
        /// <typeparam name="TMap">The map type's properties will be populated from the source type and additional mappings.</typeparam>
        /// <param name="parameter">The parameter included in the <see cref="LambdaExpression"/> which is used to generate the mapping delegate expression.</param>
        /// <param name="memberBindings">The initial bindings to use when building the property mappings.</param>
        /// <param name="depth">This parameter indicates the possible recursion depth which might be used to detect a possible
        /// infinite recursion to prevent a <see cref="StackOverflowException"/> from occuring.</param>
        /// <returns>The generated mapping between the <typeparamref name="TSource"/> and <typeparamref name="TMap"/> types.</returns>
        private static Expression<Func<TSource, TMap>> GenerateMapping<TSource, TMap>(ParameterExpression parameter, List<MemberBinding> memberBindings, int depth = 0) where TMap : new()
        {
            var sourceType = typeof(TSource);
            var mapType = typeof(TMap);

            // Temporary variables used in the main loop.
            Type collectionSourceType = null, collectionMapType = null;
            Expression selectedMapping = null;

            // The property names which are already bound in the memberBindings parameter.
            var boundProperties = new HashSet<string>(memberBindings.Select(m => m.Member.Name));

            // Iterate over the source's properties.
            foreach (var sourceProperty in sourceType.GetProperties().Where(p => !boundProperties.Contains(p.Name)))
            {
                var mapProperty = mapType.GetProperty(sourceProperty.Name);
                if (mapProperty == null) // There is no matching property in the mapping object.
                    continue;

                if (mapProperty.PropertyType.IsAssignableFrom(sourceProperty.PropertyType))
                {
                    // The source property can simply be assigned to the mapped property.
                    memberBindings.Add(Expression.Bind(mapProperty, Expression.PropertyOrField(parameter, sourceProperty.Name)));
                }
                else if (StaticMappings.Any(m => m.Key.IsAssignableFrom(sourceProperty.PropertyType) && (selectedMapping = m.Value.FirstOrDefault(em => em.Key.IsAssignableFrom(mapProperty.PropertyType)).Value) != null))
                {
                    // There is already a mapping registered between the source and mapped property types, so reuse that.
                    memberBindings.Add(GenerateQueryableBinding(parameter, sourceProperty, mapProperty, selectedMapping));
                }
                else if (
                    sourceProperty.PropertyType.GetInterfaces().Concat(new[] { sourceProperty.PropertyType }).Any(i => i.GetTypeInfo().IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICollection<>) || i.GetGenericTypeDefinition() == typeof(IQueryable<>)) && (collectionSourceType = i.GetGenericArguments().FirstOrDefault()) != null) &&
                    mapProperty.PropertyType.GetInterfaces().Concat(new[] { mapProperty.PropertyType }).Any(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>) && (collectionMapType = i.GetGenericArguments().FirstOrDefault()) != null) &&
                    mapProperty.PropertyType.IsAssignableFrom(typeof(List<>).MakeGenericType(collectionMapType))
                    )
                {
                    // If it is a collection mapping that is not assignable because of variance, try explicit mapping between the two using already registered static mappings or try to generate them. Watch out for infinite recursion.
                    if (StaticMappings.Any(m => m.Key.IsAssignableFrom(collectionSourceType) && (selectedMapping = m.Value.FirstOrDefault(em => em.Key.IsAssignableFrom(collectionMapType)).Value) != null) || (CurrentConfiguration.GenerateMappingIfNotFound && (selectedMapping = RegisterMappingInternal(collectionSourceType, collectionMapType, depth + 1)) != null))
                    {
                        // Generates the mapping between the two properties in the following form: mapProperty = source.sourceProperty.AsQueryable().Select(mapping).ToList().
                        memberBindings.Add(Expression.Bind(mapProperty,
                            Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), new[] { collectionMapType },
                                Expression.Call(typeof(Queryable), nameof(Queryable.Select), new[] { collectionSourceType, collectionMapType },
                                    Expression.Call(typeof(Queryable), nameof(Queryable.AsQueryable), new[] { collectionSourceType }, Expression.PropertyOrField(parameter, sourceProperty.Name)),
                                        Expression.Constant(selectedMapping)))));
                    }
                }
                else if (CurrentConfiguration.GenerateMappingIfNotFound)
                {
                    // No mapping is found, but the name matches. Try generating the mapping manually between the two.
                    selectedMapping = RegisterMappingInternal(sourceProperty.PropertyType, mapProperty.PropertyType, depth + 1);
                    if (selectedMapping != null)
                        memberBindings.Add(GenerateQueryableBinding(parameter, sourceProperty, mapProperty, selectedMapping));
                }
                else if (CurrentConfiguration.ThrowOnPropertyNotMappable)
                {
                    // If no mapping is found and could not generate a static mapping, throw if the appropriate setting is set to true.
                    throw new InvalidOperationException($"The property {mapProperty.Name} on type {mapType.Name} cannot be mapped from property {sourceProperty} on type {sourceType}. To suppress this exception, set the {nameof(QueryMutator)}.{nameof(CurrentConfiguration)}.{nameof(CurrentConfiguration.ThrowOnPropertyNotMappable)} property value to false.");
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

        /// <summary>
        /// Gets a mapping expression between the type <typeparamref name="TSource"/> and the type <typeparamref name="TMap"/>. Depending the current configuration, when a mapping is not found, one can be generated.
        /// </summary>
        /// <typeparam name="TSource">The source type to map the properties from.</typeparam>
        /// <typeparam name="TMap">The mapping type to map the properties to.</typeparam>
        /// <returns>The <see cref="Expression"/> representing the delegate for mapping between the source and map types.</returns>
        public static Expression<Func<TSource, TMap>> GetMapping<TSource, TMap>() where TMap : new()
        {
            var sourceType = typeof(TSource);
            var mapType = typeof(TMap);
            Dictionary<Type, Expression> mappings;
            Expression expression;
            Expression<Func<TSource, TMap>> mapping;
            if (!StaticMappings.TryGetValue(sourceType, out mappings))
                StaticMappings[sourceType] = mappings = new Dictionary<Type, Expression>();

            if (!mappings.TryGetValue(mapType, out expression)
                && (!StaticMappings.Any(m => m.Key.IsAssignableFrom(sourceType) && (expression = m.Value.FirstOrDefault(em => em.Key.IsAssignableFrom(mapType)).Value) != null))
                && !CurrentConfiguration.GenerateMappingIfNotFound)
                throw new InvalidOperationException($"No mapping was specified between the source type {sourceType} for type {mapType}. Use the {nameof(RegisterMapping)} method to register or replace a static mapping expression.");

            if ((mapping = (expression as Expression<Func<TSource, TMap>>)) == null)
            {
                if (!CurrentConfiguration.GenerateMappingIfNotFound)
                    throw new InvalidOperationException($"The provided mapping between the source type {sourceType} for type {mapType} was not the correct type registered. Use the {nameof(RegisterMapping)} method to register or replace a mapping expression.");

                return GenerateMapping<TSource, TMap>(Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()), new List<MemberBinding>(), 0);
            }

            return mapping;
        }

#endregion

#region Mapping registration

        /// <summary>
        /// Used internally to register a mapping between the given types using the <see cref="GenerateMapping{TSource, TMap}(ParameterExpression, List{MemberBinding}, int)"/> method.
        /// </summary>
        /// <param name="sourceType">The source type to map the properties from.</param>
        /// <param name="mappingType">The mapping type to map the properties to.</param>
        /// <param name="depth">This parameter indicates the possible recursion depth which might be used to detect a possible
        /// infinite recursion to prevent a <see cref="StackOverflowException"/> from occuring.</param>
        /// <returns>The <see cref="Expression"/> which is stored in the <see cref="StaticMappings"/> dictionary after generation.</returns>
        private static Expression RegisterMappingInternal(Type sourceType, Type mappingType, int depth = 0)
        {
            if (depth > CurrentConfiguration.MaximumRecursionDepth && CurrentConfiguration.MaximumRecursionDepth > 1)
                throw new InvalidOperationException($"The mapping between {sourceType} and {mappingType} seems to have caused an infinite recursion of mappings.");

            Dictionary<Type, Expression> mappings;
            if (!StaticMappings.TryGetValue(sourceType, out mappings))
                StaticMappings[sourceType] = mappings = new Dictionary<Type, Expression>();

            if (mappingType.GetConstructor(new Type[0]) != null)
                return mappings[mappingType] = typeof(QueryMutator).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).First(m => m.Name == nameof(QueryMutator.GenerateMapping)).MakeGenericMethod(sourceType, mappingType).Invoke(null, new object[] { Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()), new List<MemberBinding>(), depth + 1 }) as Expression;

            return null;
        }

        /// <summary>
        /// Automatically register a mapping between the source and map types.
        /// </summary>
        /// <typeparam name="TSource">The source type to automatically map the properties from.</typeparam>
        /// <typeparam name="TMap">The map type to automatically map the properties to.</typeparam>
        public static void RegisterMapping<TSource, TMap>() where TMap : new() => RegisterMappingInternal(typeof(TSource), typeof(TMap));

        /// <summary>
        /// Automatically register a mapping between the source and map types. Use this method when using reflection to explore the types in an assembly.
        /// </summary>
        /// <param name="sourceType">The source type to automatically map the properties from.</param>
        /// <param name="mappingType">The map type to automatically map the properties to.</param>
        public static void RegisterMapping(Type sourceType, Type mappingType) => RegisterMappingInternal(sourceType, mappingType);

        /// <summary>
        /// Register a manual mapping between the source and map types for retrieval and expression generation.
        /// </summary>
        /// <typeparam name="TSource">The type to map from.</typeparam>
        /// <typeparam name="TMap">The type to map to.</typeparam>
        /// <param name="mapping">The mapping expression to store for retrieval and expression generation.</param>
        public static void RegisterMapping<TSource, TMap>(Expression<Func<TSource, TMap>> mapping) where TMap : new()
        {
            Dictionary<Type, Expression> mappings;
            if (!StaticMappings.TryGetValue(typeof(TSource), out mappings))
                StaticMappings[typeof(TSource)] = mappings = new Dictionary<Type, Expression>();

            mappings[typeof(TMap)] = mapping;
        }

#endregion

#endregion

#region Mapping extensions for IQueryable

        /// <summary>
        /// Automatically map the source type to the target type. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static IQueryable<TMap> MapTo<TSource, TMap>(this IQueryable<TSource> source) where TMap : new()
            => source.Select(GetMapping<TSource, TMap>());

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The <see cref="Expression"/> to merge the automatic mapping with. The <see cref="Expression"/> has to be a simple object initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static IQueryable<TMap> MapTo<TSource, TMap>(this IQueryable<TSource> source, Expression<Func<TSource, TMap>> mergeWith) where TMap : new()
            => source.Select(GenerateMapping<TSource, TMap>(mergeWith.Parameters.Single(), (mergeWith.Body as MemberInitExpression).Bindings.ToList()));

        /// <summary>
        /// Automatically map the source type to the target type and call <see cref="Enumerable.ToList"/> to pull it to application memory. If previously registered, uses the 
        /// registered <see cref="StaticMappings"/> recursively or generates new mappings if <see cref="CurrentConfiguration"/> allows it.
        /// </summary>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <returns>The mapped instances queried to a <see cref="List{TMap}"/> collection.</returns>
        public static List<TMap> MapToList<TSource, TMap>(this IQueryable<TSource> source, Expression<Func<TSource, TMap>> mergeWith) where TMap : new()
            => source.MapTo(mergeWith).ToList();

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with and call <see cref="Enumerable.ToList"/> to pull it to application memory. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The expression to merge the automatic mapping with. The <see cref="Expression"/> has to be a simple object initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped instances queried to a <see cref="List{TMap}"/> collection.</returns>
        public static List<TMap> MapToList<TSource, TMap>(this IQueryable<TSource> source) where TMap : new()
            => source.MapTo<TSource, TMap>().ToList();


#endregion

#region Mapping extensions for IEnumerable

        /// <summary>
        /// Automatically map the source type to the target type. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <remarks>Compiles the generated or stored Expression to a delegate with every call.</remarks>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static IEnumerable<TMap> MapTo<TSource, TMap>(this IEnumerable<TSource> source) where TMap : new()
            => source.Select(GetMapping<TSource, TMap>().Compile());

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <remarks>Compiles the generated or stored Expression to a delegate with every call.</remarks>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The <see cref="Expression"/> to merge the automatic mapping with. The <see cref="Expression"/> has to be a simple object initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static IEnumerable<TMap> MapTo<TSource, TMap>(this IEnumerable<TSource> source, Expression<Func<TSource, TMap>> mergeWith) where TMap : new()
            => source.Select(GenerateMapping<TSource, TMap>(mergeWith.Parameters.Single(), (mergeWith.Body as MemberInitExpression).Bindings.ToList()).Compile());

        /// <summary>
        /// Automatically map the source type to the target type and call <see cref="Enumerable.ToList"/> to pull it to application memory. If previously registered, uses the 
        /// registered <see cref="StaticMappings"/> recursively or generates new mappings if <see cref="CurrentConfiguration"/> allows it.
        /// </summary>
        /// <remarks>Compiles the generated or stored Expression to a delegate with every call.</remarks>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <returns>The mapped instances queried to a <see cref="List{TMap}"/> collection.</returns>
        public static List<TMap> MapToList<TSource, TMap>(this IEnumerable<TSource> source, Expression<Func<TSource, TMap>> mergeWith) where TMap : new()
            => source.MapTo(mergeWith).ToList();

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with and call <see cref="Enumerable.ToList"/> to pull it to application memory. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <remarks>Compiles the generated or stored Expression to a delegate with every call.</remarks>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The expression to merge the automatic mapping with. The <see cref="Expression"/> has to be a simple object initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped instances queried to a <see cref="List{TMap}"/> collection.</returns>
        public static List<TMap> MapToList<TSource, TMap>(this IEnumerable<TSource> source) where TMap : new()
            => source.MapTo<TSource, TMap>().ToList();

#endregion

    }
}
