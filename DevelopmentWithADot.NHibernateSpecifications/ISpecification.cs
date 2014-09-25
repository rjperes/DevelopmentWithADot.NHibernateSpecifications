using System;
using System.Linq.Expressions;

namespace DevelopmentWithADot.NHibernateSpecifications
{
	public interface ISpecification
	{
		Expression Expression
		{
			get;
		}
	}

	public interface ISpecification<T> : ISpecification where T : class
	{
		Boolean IsSatisfiedBy(T item);
	}
}
