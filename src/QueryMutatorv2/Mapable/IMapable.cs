using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2
{
    /// <summary>
    /// A wrapper interface used by mappingProviders extension methods, using this removes the need to pass type parameter while writing code.
    /// </summary>
    public interface IMapable
    {
        /// <summary>
        /// The real source object from which we map to another object.
        /// </summary>
        object Source { get; }
    }
    /// <summary>
    /// Generic version of the IMapable interface.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMapable<T> : IMapable
    {
        new T Source { get; }
    }
}
