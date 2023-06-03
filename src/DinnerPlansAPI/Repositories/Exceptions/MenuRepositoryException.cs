using System;

namespace DinnerPlansAPI.Repositories;

public class DinnerPlansRepositoryException : Exception
{
    public DinnerPlansRepositoryException() { }
    public DinnerPlansRepositoryException(string message) : base(message) { }
    public DinnerPlansRepositoryException(string message, Exception inner) : base(message, inner) { }
}