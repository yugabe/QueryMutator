using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;

namespace System.Linq
{
    /// <summary>
    /// This class provides extensions and management features for dynamically mapping a type to another, and optionally merge object initializer
    /// expressions.
    /// </summary>
    public static class QueryMutator
    {
        private static readonly object syncRoot = new object();

        #region Configuration, static mappings

        /// <summary>
        /// The configration type for <see cref="QueryMutator"/>. Use this class to store references to the registered mapping and other 
        /// configuration properties.
        /// </summary>
        public class Configuration
        {
            /// <summary>
            /// The generated ID for this <see cref="Configuration"/> object.
            /// </summary>
            public Guid Id { get; } = Guid.NewGuid();

            /// <summary>
            /// The stored static mappings. The dictionary contains the source types (<typeparamref name="TMap"/>) as the keys and the stored 
            /// mappings as another dictionary, which holds the generated or registered <see cref="Expression"/> mapping (which is an 
            /// <see cref="Expression{Func{TSource, TMap}}"/> enclosing a <see cref="Func{TSource, TMap}"/> type with the 
            /// <typeparamref name="TSource"/> and <typeparamref name="TMap"/> generic type parameters) as the value for the 
            /// <typeparamref name="TMap"/> type key.
            /// </summary>
            internal Dictionary<Type, Dictionary<Type, Expression>> Mappings { get; } = new Dictionary<Type, Dictionary<Type, Expression>>();

            /// <summary>
            /// Setting this property to true generates a missing mapping on demand (at runtime) instead of throwing an 
            /// <see cref="InvalidOperationException"/> when the missing mapping is not found.
            /// The default value is true.
            /// </summary>
            public bool GenerateMappingIfNotFound { get; set; } = true;

            /// <summary>
            /// Setting this property to true indicates whether to throw an <see cref="InvalidOperationException"/> at runtime when a property on
            /// the type to be mapped cannot be mapped.
            /// The default value is false.
            /// </summary>
            public bool ThrowOnPropertyNotMappable { get; set; } = false;

            /// <summary>
            /// When a mapping tree is being recursively transversed, reaching this depth throws an exception. Setting this value to less than 1 
            /// disables checking for infinite recursion.
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
                else if (StaticMappings.Any(m => m.Key.GetTypeInfo().IsAssignableFrom(sourceProperty.PropertyType.GetTypeInfo()) && (selectedMapping = m.Value.FirstOrDefault(em => em.Key.GetTypeInfo().IsAssignableFrom(mapProperty.PropertyType.GetTypeInfo())).Value) != null))
                {
                    // There is already a mapping registered between the source and mapped property types, so reuse that.
                    memberBindings.Add(GenerateQueryableBinding(parameter, sourceProperty, mapProperty, selectedMapping));
                }
                else if (
                    sourceProperty.PropertyType.GetTypeInfo().ImplementedInterfaces.Concat(new[] { sourceProperty.PropertyType }).Any(i => i.GetTypeInfo().IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICollection<>) || i.GetGenericTypeDefinition() == typeof(IQueryable<>)) && (collectionSourceType = i.GenericTypeArguments.FirstOrDefault()) != null) &&
                    mapProperty.PropertyType.GetTypeInfo().ImplementedInterfaces.Concat(new[] { mapProperty.PropertyType }).Any(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>) && (collectionMapType = i.GenericTypeArguments.FirstOrDefault()) != null) &&
                    mapProperty.PropertyType.GetTypeInfo().IsAssignableFrom(typeof(List<>).MakeGenericType(collectionMapType).GetTypeInfo())
                    )
                {
                    // If it is a collection mapping that is not assignable because of variance, try explicit mapping between the two using already registered static mappings or try to generate them. Watch out for infinite recursion.
                    if (StaticMappings.Any(m => m.Key.GetTypeInfo().IsAssignableFrom(collectionSourceType.GetTypeInfo()) && (selectedMapping = m.Value.FirstOrDefault(em => em.Key.GetTypeInfo().IsAssignableFrom(collectionMapType.GetTypeInfo())).Value) != null) || (CurrentConfiguration.GenerateMappingIfNotFound && (selectedMapping = RegisterMappingInternal(collectionSourceType, collectionMapType, checkCollectionsForNull, depth + 1)) != null))
                    {
                        var falseBranch = Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), new[] { collectionMapType },
                                        Expression.Call(typeof(Queryable), nameof(Queryable.Select), new[] { collectionSourceType, collectionMapType },
                                            Expression.Call(typeof(Queryable), nameof(Queryable.AsQueryable), new[] { collectionSourceType }, Expression.PropertyOrField(parameter, sourceProperty.Name)),
                                                Expression.Constant(selectedMapping)));

                        if (checkCollectionsForNull)
                        {
                            // Generates the mapping between the two properties in the following form: mapProperty = source.sourceProperty == null ? null : source.sourceProperty.AsQueryable().Select(mapping).ToList().
                            memberBindings.Add(
                                Expression.Bind(mapProperty,
                                    Expression.Condition(
                                        test: Expression.Equal(Expression.PropertyOrField(parameter, sourceProperty.Name), Expression.Constant(null, sourceProperty.PropertyType)),
                                        ifTrue: Expression.Constant(null, falseBranch.Method.ReturnType),
                                        ifFalse: falseBranch
                                        )
                                    )
                                );
                        }
                        else
                        {
                            // Generates the mapping between the two properties in the following form: mapProperty = source.sourceProperty.AsQueryable().Select(mapping).ToList().
                            memberBindings.Add(Expression.Bind(mapProperty, falseBranch));
                        }
                    }
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
        /// Gets a mapping expression between the type <typeparamref name="TSource"/> and the type <typeparamref name="TMap"/>. 
        /// Depending on the current configuration, when a mapping is not found, one can be generated.
        /// </summary>
        /// <typeparam name="TSource">The source type to map the properties from.</typeparam>
        /// <typeparam name="TMap">The mapping type to map the properties to.</typeparam>
        /// <param name="checkCollectionsForNull">Indicates whether to generate conditional null mapping for collections in the given type.</param>
        /// <returns>The <see cref="Expression"/> representing the delegate for mapping between the source and map types.</returns>
        public static Expression<Func<TSource, TMap>> GetMapping<TSource, TMap>(bool checkCollectionsForNull) where TMap : new()
        {
            var sourceType = typeof(TSource);
            var mapType = typeof(TMap);
            if (!StaticMappings.TryGetValue(sourceType, out Dictionary<Type, Expression> mappings))
            {
                lock (syncRoot)
                {
                    if (!StaticMappings.TryGetValue(sourceType, out mappings))
                        StaticMappings[sourceType] = mappings = new Dictionary<Type, Expression>();
                }
            }

            if (!mappings.TryGetValue(mapType, out Expression expression)
                && (!StaticMappings.Any(m => m.Key.GetTypeInfo().IsAssignableFrom(sourceType.GetTypeInfo()) && (expression = m.Value.FirstOrDefault(em => em.Key.GetTypeInfo().IsAssignableFrom(mapType.GetTypeInfo())).Value) != null))
                && !CurrentConfiguration.GenerateMappingIfNotFound)
                throw new InvalidOperationException($"No mapping was specified between the source type {sourceType} for type {mapType}. Use the {nameof(RegisterMapping)} method to register or replace a static mapping expression.");

            var mapping = expression as Expression<Func<TSource, TMap>>;

            if (mapping == null)
            {
                if (!CurrentConfiguration.GenerateMappingIfNotFound)
                    throw new InvalidOperationException($"The provided mapping between the source type {sourceType} for type {mapType} was not the correct type registered. Use the {nameof(RegisterMapping)} method to register or replace a mapping expression.");

                return GenerateMapping<TSource, TMap>(Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()), new List<MemberBinding>(), checkCollectionsForNull, 0);
            }

            return mapping;
        }

        #endregion

        #region Mapping registration

        /// <summary>
        /// Used to generate predefined mappings for cached retrieval later. Pairs all found types in the matching assemblies where the source type is
        /// derived from or implements typeof(<paramref name="source"/>) and the target type is 
        /// typeof(T<paramref name="genericMapInterface"/>&lt;T<paramref name="source"/>&gt;).
        /// </summary>
        /// <param name="source">The interface or base class type for the source objects to be mapped.</param>
        /// <param name="genericMapInterface">The generic interface to be matched against.</param>
        /// <param name="checkCollectionsForNullOnSource">Indicates whether to generate conditional null mapping for collections in the given source type.</param>
        /// <param name="checkCollectionsForNullOnTarget">Indicates whether to generate conditional null mapping for collections in the given target type.</param>
        /// <param name="additionalAssemblies">Additional assemblies to scan, besides the ones of the <paramref name="source"/> and 
        /// <paramref name="genericMapInterface"/> parameters' assemblies</param>
        public static void RegisterMappings(Type source, Type genericMapInterface, bool? checkCollectionsForNullOnSource = false, bool? checkCollectionsForNullOnTarget = true, params Assembly[] additionalAssemblies)
        {
            var sourceTypeInfo = source.GetTypeInfo();
            var genericMapInterfaceTypeInfo = genericMapInterface.GetTypeInfo();

            if (!((sourceTypeInfo.IsGenericTypeDefinition && sourceTypeInfo.GenericTypeArguments.Length == 1) ^ (genericMapInterfaceTypeInfo.IsGenericTypeDefinition && genericMapInterfaceTypeInfo.GenericTypeParameters.Length == 1)))
                throw new ArgumentException($"Either the {nameof(sourceTypeInfo)} or the {nameof(genericMapInterfaceTypeInfo)} type should have exactly one generic type parameter, and the other none.");

            if (checkCollectionsForNullOnSource == null && checkCollectionsForNullOnTarget == null)
                throw new ArgumentException($"Either the {nameof(checkCollectionsForNullOnSource)} or the {nameof(checkCollectionsForNullOnTarget)} values must not be null to generate any mappings.");

            var types = additionalAssemblies.Concat(new[] { sourceTypeInfo.Assembly, genericMapInterfaceTypeInfo.Assembly }).Distinct().SelectMany(a => a.DefinedTypes).ToList();

            foreach (var type in types.Where(t => t.ImplementedInterfaces.Contains(source)))
                foreach (var stub in types.Where(t => t.ImplementedInterfaces.Any(i => i.GetTypeInfo().IsGenericType && i.GetTypeInfo().GenericTypeArguments.Any(g => g.GetTypeInfo() == type) && i.GetGenericTypeDefinition() == genericMapInterface)))
                    RegisterMappingBetweenSourceAndTarget(type.AsType(), stub.AsType(), checkCollectionsForNullOnSource, checkCollectionsForNullOnTarget);
        }

        /// <summary>
        /// Used to generate predefined mappings for cached retrieval later. Pairs all found types in the matching assemblies where the source type is
        /// derived from or implements any of the source type's elements and the target type is any of the target type elements.
        /// </summary>
        /// <param name="sources">The interface or base class types for the source objects to be mapped.</param>
        /// <param name="genericMapInterfaces">The generic interfaces to be matched against.</param>
        /// <param name="checkCollectionsForNullOnSource">Indicates whether to generate conditional null mapping for collections in the given source type.</param>
        /// <param name="checkCollectionsForNullOnTarget">Indicates whether to generate conditional null mapping for collections in the given target type.</param>
        /// <param name="additionalAssemblies">Additional assemblies to scan, besides the ones of the <paramref name="source"/> and 
        /// <paramref name="genericMapInterface"/> parameters' assemblies</param>
        public static void RegisterMappings(IEnumerable<Type> sources, IEnumerable<Type> genericMapInterfaces, bool? checkCollectionsForNullOnSource = false, bool? checkCollectionsForNullOnTarget = true, params Assembly[] additionalAssemblies)
        {
            var source = sources.ToArray();
            var generics = genericMapInterfaces.ToArray();
            if (!(sources.All(s => s.GetTypeInfo().IsGenericTypeDefinition && s.GenericTypeArguments.Length == 1) ^ genericMapInterfaces.All(i => i.GetTypeInfo().IsGenericTypeDefinition && i.GetTypeInfo().GenericTypeParameters.Length == 1)))
                throw new ArgumentException($"Either the {nameof(sources)} or the {nameof(genericMapInterfaces)} types should have exactly one generic type parameter, and the other none.");

            if (checkCollectionsForNullOnSource == null && checkCollectionsForNullOnTarget == null)
                throw new ArgumentException($"Either the {nameof(checkCollectionsForNullOnSource)} or the {nameof(checkCollectionsForNullOnTarget)} values must not be null to generate any mappings.");

            var types = additionalAssemblies.Concat(sources.Select(s => s.GetTypeInfo().Assembly)).Concat(genericMapInterfaces.Select(i => i.GetTypeInfo().Assembly)).Distinct().SelectMany(a => a.DefinedTypes).ToList();

            foreach (var type in types.Where(t => t.ImplementedInterfaces.Any(i => sources.Contains(i))))
                foreach (var stub in types.Where(t => t.ImplementedInterfaces.Any(i => i.GetTypeInfo().IsGenericType && i.GetTypeInfo().GenericTypeArguments.Any(g => g.GetTypeInfo() == type) && genericMapInterfaces.Any(g => i.GetGenericTypeDefinition() == g))))
                    RegisterMappingBetweenSourceAndTarget(type.AsType(), stub.AsType(), checkCollectionsForNullOnSource, checkCollectionsForNullOnTarget);
        }

        /// <summary>
        /// Registers the mappings between the given types based on the given collection check parameters. Either Nullable&lt;boolean&gt; value having null will
        /// skip generating the corresponding mappings.
        /// </summary>
        /// <param name="sourceType">The interface or base class type for the source objects to be mapped.</param>
        /// <param name="mappingType">The generic interface to be matched against.</param>
        /// <param name="checkCollectionsForNullOnSource">Indicates whether to generate conditional null mapping for collections in the given source type.</param>
        /// <param name="checkCollectionsForNullOnTarget">Indicates whether to generate conditional null mapping for collections in the given target type.</param>
        private static void RegisterMappingBetweenSourceAndTarget(Type sourceType, Type mappingType, bool? checkCollectionsForNullOnSource, bool? checkCollectionsForNullOnTarget)
        {
            if (checkCollectionsForNullOnSource != null)
                RegisterMappingInternal(sourceType, mappingType, checkCollectionsForNullOnSource.Value);
            if (checkCollectionsForNullOnTarget != null)
                RegisterMappingInternal(mappingType, sourceType, checkCollectionsForNullOnTarget.Value);
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

            if (!StaticMappings.TryGetValue(sourceType, out Dictionary<Type, Expression> mappings))
            {
                lock (syncRoot)
                {
                    if (!StaticMappings.TryGetValue(sourceType, out mappings))
                        StaticMappings[sourceType] = mappings = new Dictionary<Type, Expression>();
                }
            }

            if (mappingType.GetTypeInfo().DeclaredConstructors.Any(c => c.IsPublic && c.GetParameters().Length == 0))
            {
                return mappings[mappingType] = typeof(QueryMutator).GetTypeInfo()
                    .DeclaredMethods.First(m => m.IsStatic && !m.IsPublic && m.Name == nameof(QueryMutator.GenerateMapping))
                    .MakeGenericMethod(sourceType, mappingType)
                    .Invoke(null, new object[] { Expression.Parameter(sourceType, sourceType.Name[0].ToString().ToLower()), new List<MemberBinding>(), checkCollectionsForNull, depth + 1 }) as Expression;
            }

            return null;
        }

        /// <summary>
        /// Automatically register a mapping between the source and map types.
        /// </summary>
        /// <typeparam name="TSource">The source type to automatically map the properties from.</typeparam>
        /// <typeparam name="TMap">The map type to automatically map the properties to.</typeparam>
        /// <param name="checkCollectionsForNull">Indicates whether to generate conditional null mapping for collections in the given type.</param>
        public static void RegisterMapping<TSource, TMap>(bool checkCollectionsForNull) where TMap : new() => RegisterMappingInternal(typeof(TSource), typeof(TMap), checkCollectionsForNull);

        /// <summary>
        /// Automatically register a mapping between the source and map types. Use this method when using reflection to explore the types in an
        /// assembly.
        /// </summary>
        /// <param name="sourceType">The source type to automatically map the properties from.</param>
        /// <param name="mappingType">The map type to automatically map the properties to.</param>
        /// <param name="checkCollectionsForNull">Indicates whether to generate conditional null mapping for collections in the given type.</param>
        public static void RegisterMapping(Type sourceType, Type mappingType, bool checkCollectionsForNull) => RegisterMappingInternal(sourceType, mappingType, checkCollectionsForNull);

        /// <summary>
        /// Register a manual mapping between the source and map types for retrieval and expression generation.
        /// </summary>
        /// <typeparam name="TSource">The type to map from.</typeparam>
        /// <typeparam name="TMap">The type to map to.</typeparam>
        /// <param name="mapping">The mapping expression to store for retrieval and expression generation.</param>
        public static void RegisterMapping<TSource, TMap>(Expression<Func<TSource, TMap>> mapping) where TMap : new()
        {
            if (!StaticMappings.TryGetValue(typeof(TSource), out Dictionary<Type, Expression> mappings))
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
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="checkCollectionsForNull">Indicates whether to generate conditional null mapping for collections in the given type.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static IQueryable<TMap> MapTo<TSource, TMap>(this IQueryable<TSource> source, bool checkCollectionsForNull = false) where TMap : new()
            => source.Select(GetMapping<TSource, TMap>(checkCollectionsForNull));

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The <see cref="Expression"/> to merge the automatic mapping with. The <see cref="Expression"/> has to be a 
        /// simple object initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static IQueryable<TMap> MapTo<TSource, TMap>(this IQueryable<TSource> source, Expression<Func<TSource, TMap>> mergeWith, bool checkCollectionsForNull = false) where TMap : new()
            => source.Select(GenerateMapping<TSource, TMap>(mergeWith.Parameters.Single(), (mergeWith.Body as MemberInitExpression).Bindings.ToList(), checkCollectionsForNull));

        /// <summary>
        /// Automatically map the source type to the target type and call <see cref="Enumerable.ToList"/> to pull it to application memory. If 
        /// previously registered, uses the registered <see cref="StaticMappings"/> recursively or generates new mappings if 
        /// <see cref="CurrentConfiguration"/> allows it.
        /// </summary>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <returns>The mapped instances queried to a <see cref="List{TMap}"/> collection.</returns>
        public static List<TMap> MapToList<TSource, TMap>(this IQueryable<TSource> source, Expression<Func<TSource, TMap>> mergeWith) where TMap : new()
            => source.MapTo(mergeWith).ToList();

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with and call <see cref="Enumerable.ToList"/>
        /// to pull it to application memory. If previously registered, uses the registered mappings recursively or generates new mappings if the
        /// current configuration allows it.
        /// </summary>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The expression to merge the automatic mapping with. The <see cref="Expression"/> has to be a simple object 
        /// initializer, otherwise a runtime exception will be thrown.</param>
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
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static IEnumerable<TMap> MapTo<TSource, TMap>(this IEnumerable<TSource> source, bool checkCollectionsForNull = true) where TMap : new()
            => source.Select(GetMapping<TSource, TMap>(checkCollectionsForNull).Compile());

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <remarks>Compiles the generated or stored Expression to a delegate with every call.</remarks>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The <see cref="Expression"/> to merge the automatic mapping with. The <see cref="Expression"/> has to be a 
        /// simple object initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static IEnumerable<TMap> MapTo<TSource, TMap>(this IEnumerable<TSource> source, Expression<Func<TSource, TMap>> mergeWith, bool checkCollectionsForNull = true) where TMap : new()
            => source.Select(GenerateMapping<TSource, TMap>(mergeWith.Parameters.Single(), (mergeWith.Body as MemberInitExpression).Bindings.ToList(), checkCollectionsForNull).Compile());

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with and call <see cref="Enumerable.ToList"/>
        /// to pull it to application memory. If previously registered, uses the registered mappings recursively or generates new mappings if the 
        /// current configuration allows it.
        /// </summary>
        /// <remarks>Compiles the generated or stored Expression to a delegate with every call.</remarks>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The expression to merge the automatic mapping with. The <see cref="Expression"/> has to be a simple object 
        /// initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped instances queried to a <see cref="List{TMap}"/> collection.</returns>
        public static List<TMap> MapToList<TSource, TMap>(this IEnumerable<TSource> source, Expression<Func<TSource, TMap>> mergeWith) where TMap : new()
            => source.MapTo(mergeWith).ToList();

        /// <summary>
        /// Automatically map the source type to the target type and call <see cref="Enumerable.ToList"/> to pull it to application memory. If 
        /// previously registered, uses the registered <see cref="StaticMappings"/> recursively or generates new mappings if
        /// <see cref="CurrentConfiguration"/> allows it.
        /// </summary>
        /// <remarks>Compiles the generated or stored Expression to a delegate with every call.</remarks>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <returns>The mapped instances queried to a <see cref="List{TMap}"/> collection.</returns>
        public static List<TMap> MapToList<TSource, TMap>(this IEnumerable<TSource> source) where TMap : new()
            => source.MapTo<TSource, TMap>().ToList();

        #endregion

        #region Mapping extensions for everything else

        /// <summary>
        /// Automatically map the source type to the target type. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <returns>The mapped <see cref="{TMap}"/> instance.</returns>
        public static TMap MapTo<TSource, TMap>(this TSource source) where TMap : new()
            => new[] { source }.MapTo<TSource, TMap>().FirstOrDefault();

        /// <summary>
        /// Automatically map the source type to the target type using a merging expression to merge with. If previously registered, uses the 
        /// registered mappings recursively or generates new mappings if the current configuration allows it.
        /// </summary>
        /// <remarks>Compiles the generated or stored Expression to a delegate with every call.</remarks>
        /// <typeparam name="TSource">The type to map from. When depending on automatic generation, the properties with the same property names in
        /// the source and map types are tried to map.</typeparam>
        /// <typeparam name="TMap">The type to map to. When depending on automatic generation, the properties with the same property names in the 
        /// source and map types are tried to map.</typeparam>
        /// <param name="source">The source to map to a target using the automatically generated and manually registered expressions.</param>
        /// <param name="mergeWith">The <see cref="Expression"/> to merge the automatic mapping with. The <see cref="Expression"/> has to be a 
        /// simple object initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped <see cref="IQueryable{TMap}"/> instance.</returns>
        public static TMap MapTo<TSource, TMap>(this TSource source, Expression<Func<TSource, TMap>> mergeWith) where TMap : new()
            => new[] { source }.MapTo(mergeWith).FirstOrDefault();

        /// <summary>
        /// Create a shallow copy of an object.
        /// </summary>
        /// <remarks>Remember that making a shallow copy of an object copies the properties by assignment, thus if a property is a value type
        /// it will be copied, and if it is a reference type, it's reference will be copied (not the object itself).</remarks>
        /// <typeparam name="T">The type to map from and to.</typeparam>
        /// <param name="source">The source object to create a copy of.</param>
        /// <returns>The mapped <typeparamref name="T"/> instance.</returns>
        public static T Clone<T>(this T source) where T : new()
            => new[] { source }.MapTo<T, T>().FirstOrDefault();

        /// <summary>
        /// Create a shallow copy of an object.
        /// </summary>
        /// <remarks>Remember that making a shallow copy of an object copies the properties by assignment, thus if a property is a value type
        /// it will be copied, and if it is a reference type, it's reference will be copied (not the object itself).</remarks>
        /// <typeparam name="T">The type to map from and to.</typeparam>
        /// <param name="source">The source object to create a copy of.</param>
        /// <param name="mergeWith">The <see cref="Expression"/> to merge the automatic mapping with. The <see cref="Expression"/> has to be a 
        /// simple object initializer, otherwise a runtime exception will be thrown.</param>
        /// <returns>The mapped <typeparamref name="T"/> instance.</returns>
        public static T Clone<T>(this T source, Expression<Func<T, T>> mergeWith) where T : new()
            => new[] { source }.MapTo(mergeWith).FirstOrDefault();

        #endregion

    }
}
