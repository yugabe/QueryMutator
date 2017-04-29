using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2.Mapable
{
    /// <summary>
    /// Implementation of the IMapable interface,
    /// the .Map() generic extension method return the original object wrapped into this class.
    /// </summary>
    /// <typeparam name="T">Type of the original object to be mapped.</typeparam>
    internal class Mapable<T> : IMapable<T>
    {
        public Mapable(T original)
        {
            Source = original;
        }
        public T Source { get; }

        object IMapable.Source => Source;
    }
}
