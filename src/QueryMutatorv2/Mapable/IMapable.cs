using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2
{
   public interface IMapable<T>  
    {
        T Source { get; }

    }
}
