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
        static private string storageName = "ExplicitConventions";
        Guid id = Guid.NewGuid();

        Expression<Func<TSource, TMap2>> AssignExpression { get; }
        void AddBindings(ConventionContext context)
        {
            context.storage.TryGetValue("Parameter", out var param);
            var newexpr = LambdaRewriter.Rewrite(AssignExpression, (ParameterExpression)param);
            var newbindings = (newexpr.Body as MemberInitExpression).Bindings.ToList();
            context.storage.TryGetValue("Bindings", out var bindings);
            var typedBindings = bindings as List<MemberBinding>;
            newbindings.ForEach(b => typedBindings.Add(b));

        }
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
                else {
                    AddBindings(context);
                    typedExplicitConventions.Add(id, 1);
                }
            }

            return true;

        }
        public ExplicitMappingConvention(Expression<Func<TSource, TMap2>> assignExpression)
        {
            AssignExpression = assignExpression;

        }
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
