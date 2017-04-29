using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Collections;
namespace QueryMutatorv2.Conventions
{
    public class ExplicitMappingConvention<TSource, TMap2> : IConvention
    {
        /// <summary>
        /// The key of the entry of the context.storage which identifies a dictionary of explicit Conventions. 
        /// </summary>
        static private string storageName = "ExplicitConventions";
        Guid id = Guid.NewGuid();

        Expression<Func<TSource, TMap2>> AssignExpression { get; }
        /// <summary>
        /// Adds the memberBindings extracted from the AssignExpression to the context.storage Bindings entry
        /// </summary>
        /// <param name="context">The context of the mapping passed by the <see cref="Provider.MappingProvider"></param>
        void AddBindings(ConventionContext context)
        {
            context.storage.TryGetValue("Parameter", out var param);
            var newexpr = LambdaRewriter.Rewrite(AssignExpression, (ParameterExpression)param);
            var newbindings = (newexpr.Body as MemberInitExpression).Bindings.ToList();
            context.storage.TryGetValue("Bindings", out var bindings);
            var typedBindings = bindings as List<MemberBinding>;
            newbindings.ForEach(b =>
            {
                if (!context.BoundedPropertys.ContainsKey(b.Member.Name))
                {
                    typedBindings.Add(b);
                    context.BoundedPropertys.Add(b.Member.Name, true);
                }
            });

        }
        /// <summary>
        /// Adds the bindings if they are not already in the context.storage
        /// For params see:<see cref="IConvention"/>
        /// </summary>
        /// <returns>true if the bindings are added else false</returns>
        public bool Apply<TMap>(object source, object destination, ConventionContext context)
        {

            context.storage.TryGetValue(storageName, out var explicitConventions);
            if (explicitConventions == null)
            {


                var dict = new Dictionary<Guid, int>();
                AddBindings(context);
                dict.Add(id, 1);
                context.storage.Add(storageName, dict);
            }
            else
            {
                var typedExplicitConventions = (Dictionary<Guid, int>)explicitConventions;
                if (typedExplicitConventions.ContainsKey(id))
                {
                    typedExplicitConventions.TryGetValue(id, out var count);
                    if (count == 0)
                    {
                        AddBindings(context);
                    }
                }
                else
                {
                    AddBindings(context);
                    typedExplicitConventions.Add(id, 1);
                }
            }

            return false;

        }

        public ExplicitMappingConvention(Expression<Func<TSource, TMap2>> assignExpression)
        {
            AssignExpression = assignExpression;

        }
        /// <summary>
        /// Rewrites a lambda Expression to use another Parameter.
        /// </summary>
        private class LambdaRewriter
        {
            public static Expression<Func<TSource, TMap2>> Rewrite<TSource, TMap2>(Expression<Func<TSource, TMap2>> exp, ParameterExpression p)
            {

                var newExpression = new LambdaRewriterVisitor(p).Visit(exp);

                return (Expression<Func<TSource, TMap2>>)newExpression;
            }

            private class LambdaRewriterVisitor : ExpressionVisitor
            {
                private readonly ParameterExpression _parameterExpression;

                public LambdaRewriterVisitor(ParameterExpression parameterExpression)
                {
                    _parameterExpression = parameterExpression;
                }

                protected override Expression VisitParameter(ParameterExpression node)
                {
                    return _parameterExpression;
                }
            }
        }
    }
}
