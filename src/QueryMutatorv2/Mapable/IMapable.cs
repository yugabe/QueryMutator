using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2
{
    public interface IMapable
    {
        object Source { get; }
    }
    public interface IMapable<T> : IMapable
    {
        new T Source { get; }
    }
}
