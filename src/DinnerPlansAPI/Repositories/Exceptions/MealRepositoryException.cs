using System;

namespace DinnerPlansAPI.Repositories;

public class MealRepositoryException : Exception
{
    public MealRepositoryException() { }
    public MealRepositoryException(string message) : base(message) { }
    public MealRepositoryException(string message, Exception inner) : base(message, inner) { }
}