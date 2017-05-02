using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace QueryMutatorv2
{
    /// <summary>
    /// The context of mappings, used by <see cref="MappingProvider"/>
    /// </summary>
    public class ConventionContext
    {
        /// <summary>
        /// The storage is in which we put data used by multiple conventions, like the expression Parameter(Parameter) and MemberBindings ("Bindings")
        /// </summary>
        public Dictionary<string, object> storage;
        /// <summary>
        /// The current recursion depth of the mapper.
        /// </summary>
        public int CurrentRecursionDepth { get; set; }
        public Dictionary<String, bool> BoundedPropertys = new Dictionary<string, bool>();
        public ConventionContext()
        {
          
            storage = new Dictionary<string, object>();
            storage.Add("Bindings", new List<MemberBinding>());
      
        }

    }
}
