using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2.Mapable
{
    public class Mapable<T> : IMapable<T>   
    {
        private T source;
        public Mapable(T original) {
            source = original;
        }
        public T Source => source;

       
    }
}
