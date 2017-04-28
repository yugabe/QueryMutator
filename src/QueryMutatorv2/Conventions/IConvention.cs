using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2.Conventions
{

    public interface IConvention
    {

        bool Apply<TMap>(object source, object destination, ConventionContext context);
    }
}
