using System;
using System.Collections.Generic;
using System.Text;

namespace  QueryMutatorv2.Conventions
{
    
    public interface IConvention
    {
         
        bool Apply(Object Source,Object Destination,ConventionContext Context);
    }
}
