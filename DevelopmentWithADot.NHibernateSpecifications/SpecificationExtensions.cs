using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NHibernate;
using NHibernate.Linq;

namespace DevelopmentWithADot.NHibernateSpecifications
{
	public static class SpecificationExtensions
	{
		#region Private static helper methods

		private static IQueryable<T> CreateQueryable<T>()
		{
			return (new NhQueryable<T>(null));
		}

		internal static Expression<Func<T, Boolean>> ExtractCondition<T>(Expression expression)
		{
			if (expression is Expression<Func<T, Boolean>>)
			{
				return (expression as Expression<Func<T, Boolean>>);
			}
			else if (expression is MethodCallExpression)
			{
				foreach (var argument in (expression as MethodCallExpression).Arguments)
				{
					var condition = ExtractCondition<T>(argument);

					if (condition != null)
					{
						return (condition);
					}
				}
			}
			else if (expression is UnaryExpression)
			{
				return (ExtractCondition<T>((expression as UnaryExpression).Operand));
			}

			return (null);
		}

		private static MethodCallExpression FindMethodCallExpression(Expression expression, Type type, String name)
		{
			if (expression is MethodCallExpression)
			{
				var methodCallExp = (expression as MethodCallExpression);

				if ((methodCallExp.Method.DeclaringType == type) && (methodCallExp.Method.Name == name))
				{
					return (methodCallExp);
				}

				foreach (var argument in methodCallExp.Arguments)
				{
					methodCallExp = FindMethodCallExpression(argument, type, name);

					if (methodCallExp != null)
					{
						return (methodCallExp);
					}
				}
			}

			return (null);
		}

		private static Expression<Func<T, Object>> ExtractOrder<T>(Expression expression, String orderKind)
		{
			var methodCallExp = FindMethodCallExpression(expression, typeof(Queryable), orderKind);

			if (methodCallExp != null)
			{
				var lambda = (methodCallExp.Arguments.Last() as UnaryExpression).Operand as LambdaExpression;

				lambda = Expression.Lambda<Func<T, Object>>(Expression.Convert(lambda.Body, typeof(Object)), lambda.Name, lambda.TailCall, lambda.Parameters);

				return (lambda as Expression<Func<T, Object>>);
			}

			return (null);

		}

		private static Expression<Func<T, Object>> ExtractOrderBy<T>(Expression expression)
		{
			return (ExtractOrder<T>(expression, "OrderBy"));
		}

		private static Expression<Func<T, Object>> ExtractOrderByDescending<T>(Expression expression)
		{
			return (ExtractOrder<T>(expression, "OrderByDescending"));
		}

		public static Expression<Func<T, Object>> ExtractThenBy<T>(Expression expression)
		{
			return (ExtractOrder<T>(expression, "ThenBy"));
		}

		public static Expression<Func<T, Object>> ExtractThenByDescending<T>(Expression expression)
		{
			return (ExtractOrder<T>(expression, "ThenByDescending"));
		}

		private static Int32 ExtractPaging<T>(Expression expression, String pagingKind)
		{
			var methodCallExp = FindMethodCallExpression(expression, typeof(Queryable), pagingKind);

			if (methodCallExp != null)
			{
				return ((Int32)(methodCallExp.Arguments.Last() as ConstantExpression).Value);
			}

			return (0);
		}

		private static Int32 ExtractTake<T>(Expression expression)
		{
			return (ExtractPaging<T>(expression, "Take"));
		}

		private static Int32 ExtractSkip<T>(Expression expression)
		{
			return (ExtractPaging<T>(expression, "Skip"));
		}

		public static Expression<Func<T, Object>> ExtractFetch<T>(Expression expression)
		{
			var methodCallExp = FindMethodCallExpression(expression, typeof(EagerFetchingExtensionMethods), "Fetch");

			if (methodCallExp != null)
			{
				return ((methodCallExp.Arguments.Last() as UnaryExpression).Operand as Expression<Func<T, Object>>);
			}

			return (null);
		}

		private static IQueryable<T> AddFetching<T>(IQueryable<T> queryable, Expression source, Boolean skipOrdering, Boolean skipPaging)
		{
			if (skipOrdering == false)
			{
				queryable = AddOrdering(queryable, source, true);
			}

			if (skipPaging == false)
			{
				queryable = AddPaging(queryable, source, false);
			}

			var fetch = ExtractFetch<T>(source);

			if (fetch != null)
			{
				queryable = queryable.Fetch(fetch);
			}

			return (queryable);
		}

		private static IQueryable<T> AddPaging<T>(IQueryable<T> queryable, Expression source, Boolean skipOrdering)
		{
			var take = ExtractTake<T>(source);
			var skip = ExtractSkip<T>(source);

			if (skipOrdering == false)
			{
				queryable = AddOrdering(queryable, source, true);
			}

			if (skip != 0)
			{
				queryable = queryable.Skip(skip);
			}

			if (take != 0)
			{
				queryable = queryable.Take(take);
			}

			return (queryable);
		}

		private static IQueryable<T> AddOrdering<T>(IQueryable<T> queryable, Expression source, Boolean skipPaging)
		{
			var orderBy = ExtractOrderBy<T>(source);
			var orderByDescending = ExtractOrderByDescending<T>(source);
			var thenBy = ExtractThenBy<T>(source);
			var thenByDescending = ExtractThenByDescending<T>(source);

			if (orderBy != null)
			{
				queryable = queryable.OrderBy(orderBy);
			}

			if (orderByDescending != null)
			{
				queryable = queryable.OrderByDescending(orderByDescending);
			}

			if (thenBy != null)
			{
				queryable = (queryable as IOrderedQueryable<T>).ThenBy(thenBy);
			}

			if (thenByDescending != null)
			{
				queryable = (queryable as IOrderedQueryable<T>).ThenByDescending(thenByDescending);
			}

			if (skipPaging == false)
			{
				queryable = AddPaging(queryable, source, true);
			}

			return (queryable);
		}

		#endregion

		#region Public extension methods

		public static IQueryable All(this Type type)
		{
			return (typeof(SpecificationExtensions).GetMethod("All", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(type).Invoke(null, null) as IQueryable);
		}

		public static IQueryable<T> All<T>() where T : class
		{
			return (Where<T>(x => true));
		}

		public static IQueryable<T> Where<T>(Expression<Func<T, Boolean>> condition) where T : class
		{
			return (CreateQueryable<T>().Where(condition));
		}

		public static IQueryable<T> QueryBySpecification<T>(this ISession session, ISpecification<T> specification) where T : class
		{
			var queryable = session.Query<T>().Where(ExtractCondition<T>(specification.Expression));
			queryable = AddOrdering(queryable, specification.Expression, false);
			//queryable = AddPaging(queryable, specification.Expression, true);
			queryable = AddFetching(queryable, specification.Expression, true, true);

			return (queryable);
		}

		public static ISpecification<T> And<T>(this ISpecification<T> specification, Expression<Func<T, Boolean>> condition) where T : class
		{
			var current = ExtractCondition<T>(specification.Expression);
			var and = Expression.AndAlso(current.Body, condition.Body);
			var param = Expression.Parameter(typeof(T), "x");
			var body = Expression.AndAlso(Expression.Invoke(current, param), Expression.Invoke(condition, param));
			var lambda = Expression.Lambda<Func<T, Boolean>>(body, param);

			var queryable = CreateQueryable<T>().Where(lambda);
			queryable = AddOrdering(queryable, specification.Expression, false);
			queryable = AddPaging(queryable, specification.Expression, false);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> And<T>(this ISpecification<T> specification, ISpecification<T> other) where T : class
		{
			return (And(specification, ExtractCondition<T>(other.Expression)));
		}

		public static ISpecification<T> Or<T>(this ISpecification<T> specification, Expression<Func<T, Boolean>> condition) where T : class
		{
			var current = ExtractCondition<T>(specification.Expression);
			var or = Expression.OrElse(current.Body, condition.Body);
			var param = Expression.Parameter(typeof(T), "x");
			var body = Expression.OrElse(Expression.Invoke(current, param), Expression.Invoke(condition, param));
			var lambda = Expression.Lambda<Func<T, Boolean>>(body, param);

			var queryable = CreateQueryable<T>().Where(lambda);
			queryable = AddOrdering(queryable, specification.Expression, false);
			queryable = AddPaging(queryable, specification.Expression, false);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> Or<T>(this ISpecification<T> specification, ISpecification<T> other) where T : class
		{
			return (Or(specification, ExtractCondition<T>(other.Expression)));
		}

		public static ISpecification<T> Not<T>(this ISpecification<T> specification) where T : class
		{
			var not = Expression.Not(ExtractCondition<T>(specification.Expression).Body);

			var queryable = CreateQueryable<T>().Where(Expression.Lambda<Func<T, Boolean>>(not, ExtractCondition<T>(specification.Expression).Parameters));
			queryable = AddOrdering(queryable, specification.Expression, false);
			queryable = AddPaging(queryable, specification.Expression, false);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> AsSpecification<T>(this Expression<Func<T, Boolean>> expression) where T : class
		{
			return (Specification<T>.Create(expression));
		}

		public static Expression<Func<T, Boolean>> AsCondition<T>(this ISpecification<T> specification) where T : class
		{
			return (ExtractCondition<T>(specification.Expression));
		}

		public static ISpecification<T> Take<T>(this ISpecification<T> specification, Int32 count) where T : class
		{
			var queryable = CreateQueryable<T>();
			queryable = queryable.Where(ExtractCondition<T>(specification.Expression)).Take(count);

			var skip = ExtractSkip<T>(specification.Expression);

			if (skip != 0)
			{
				queryable = queryable.Skip(skip);
			}

			queryable = AddOrdering(queryable, specification.Expression, true);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> Skip<T>(this ISpecification<T> specification, Int32 count) where T : class
		{
			var queryable = CreateQueryable<T>();
			queryable = queryable.Where(ExtractCondition<T>(specification.Expression)).Skip(count);

			var take = ExtractTake<T>(specification.Expression);

			if (take != 0)
			{
				queryable = queryable.Take(take);
			}

			queryable = AddOrdering(queryable, specification.Expression, true);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> OrderBy<T>(this ISpecification<T> specification, Expression<Func<T, Object>> orderBy) where T : class
		{
			var queryable = CreateQueryable<T>();
			queryable = queryable.OrderBy(orderBy).Where(ExtractCondition<T>(specification.Expression));
			queryable = AddPaging(queryable, specification.Expression, true);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> OrderByDescending<T>(this ISpecification<T> specification, Expression<Func<T, Object>> orderByDescending) where T : class
		{
			var queryable = CreateQueryable<T>();
			queryable = queryable.OrderByDescending(orderByDescending).Where(ExtractCondition<T>(specification.Expression));
			queryable = AddPaging(queryable, specification.Expression, true);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> ThenBy<T>(this ISpecification<T> specification, Expression<Func<T, Object>> orderBy) where T : class
		{
			var queryable = CreateQueryable<T>();
			queryable = AddOrdering(queryable, specification.Expression, true);
			queryable = (queryable as IOrderedQueryable<T>).ThenBy(orderBy).Where(ExtractCondition<T>(specification.Expression));
			queryable = AddPaging(queryable, specification.Expression, true);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> ThenByDescending<T>(this ISpecification<T> specification, Expression<Func<T, Object>> orderBy) where T : class
		{
			var queryable = CreateQueryable<T>();
			queryable = AddOrdering(queryable, specification.Expression, true);
			queryable = (queryable as IOrderedQueryable<T>).ThenByDescending(orderBy).Where(ExtractCondition<T>(specification.Expression));
			queryable = AddPaging(queryable, specification.Expression, true);

			return (new Specification<T>(queryable.Expression));
		}

		public static ISpecification<T> Fetch<T>(this ISpecification<T> specification, Expression<Func<T, Object>> path) where T : class
		{
			var queryable = CreateQueryable<T>().Where(ExtractCondition<T>(specification.Expression));
			queryable = queryable.Fetch(path);
			queryable = AddOrdering(queryable, specification.Expression, false);
			queryable = AddPaging(queryable, specification.Expression, false);

			return (new Specification<T>(queryable.Expression));
		}

		#endregion
	}
}
