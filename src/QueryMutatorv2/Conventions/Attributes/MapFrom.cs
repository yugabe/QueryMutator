using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using static QueryMutatorv2.Conventions.Attributes.MapFrom;

namespace QueryMutatorv2.Conventions.Attributes
{
    // TODO #1: explicit mapping
    //[MapFrom<Kutya>(k => k.Name.Length)]
    //[MapFrom<Majom>(k => k.Name.Length)]
    //public int NameLength { get; set; }
    [AttributeUsage(System.AttributeTargets.Class |
                          System.AttributeTargets.Property |
                          System.AttributeTargets.Struct, AllowMultiple = true, Inherited = true)
    ]
    public class MapFrom : Attribute
    {
       
        public Expression E { get; }
        public MapFrom(LambdaExpression e)
        {
            E = e;

        }


    }
}
