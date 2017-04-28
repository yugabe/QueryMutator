using System;
using System.Collections.Generic;
using System.Text;

namespace QueryMutatorv2.Conventions.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class |
                           System.AttributeTargets.Property |
                           System.AttributeTargets.Struct)
    ]
    public class IgnoreMap : System.Attribute
    {
    }
}

