using System;
using System.Linq;
using System.Linq.Expressions;

namespace DevelopmentWithADot.NHibernateSpecifications
{
	public class Specification<T> : ISpecification<T> where T : class
	{
		private Func<T, Boolean> compiled;

		protected internal Specification(Expression expression)
		{
			this.Expression = expression;
		}

		public static ISpecification<T> Create(Expression<Func<T, Boolean>> expression)
		{
			return (new Specification<T>(expression));
		}

		public static IQueryable<T> All()
		{
			return (SpecificationExtensions.All<T>());
		}

		public static IQueryable<T> Where(Expression<Func<T, Boolean>> condition)
		{
			return (SpecificationExtensions.Where(condition));
		}

		#region ISpecification<T> Members

		public Boolean IsSatisfiedBy(T item)
		{
			if (this.compiled == null)
			{
				this.compiled = SpecificationExtensions.ExtractCondition<T>(this.Expression).Compile();
			}

			return (this.compiled(item));
		}

		#endregion

		#region ISpecification Members

		public virtual Expression Expression
		{
			get;
			protected set;
		}

		#endregion

		#region Public override methods

		public override Boolean Equals(Object obj)
		{
			var other = obj as Specification<T>;

			if ((other == null) || (other.GetType() != this.GetType()))
			{
				return (false);
			}

			if (Object.ReferenceEquals(this, obj) == true)
			{
				return (true);
			}

			return (this.Expression.Equals(other.Expression));
		}

		public override Int32 GetHashCode()
		{
			return (this.Expression.GetHashCode());
		}

		public override String ToString()
		{
			return (this.Expression.ToString());
		}

		#endregion
	}
}
