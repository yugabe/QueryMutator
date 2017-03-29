using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace QueryMutatorv2
{
    public class ConventionContext
    {
        public Dictionary<string, Object> storage;
        int CurrentRecursionDepth { get; set; }
        public ConventionContext() {
            storage = new Dictionary<string, object>();
            storage.Add("Bindings", new List<MemberBinding>());
            CurrentRecursionDepth = 0;
        }

    }
}
