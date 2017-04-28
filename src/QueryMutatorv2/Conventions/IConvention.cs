using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2.Conventions
{

    public interface IConvention
    {

        bool Apply<TSource>(object Source, object Destination, ConventionContext Context);
    }
}
