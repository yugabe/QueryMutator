using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2.Mapable
{
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
