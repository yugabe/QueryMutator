using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2.Conventions
{
    /// <summary>
    /// Interface for conventions.
    /// </summary>
    public interface IConvention
    {
        /// <summary>
        /// The mappingProvider calls this method while iterating trough the sourceObject propertys. 
        /// It's supposed to add memberBindings based on some logic to the context.storage.trygetvalue("Bindings").
        /// </summary>
        /// <typeparam name="TMap">The type of the class for which we make the memberBindings</typeparam>
        /// <param name="source">the source property from which we bind</param>
        /// <param name="destination">typeof(TMap) at the moment its unnecesary</param>
        /// <param name="context">The context of the mapping  <see cref="ConventionContext"/></param>
        /// <returns></returns>
        bool Apply<TMap>(object source, object destination, ConventionContext context);
    }
}
