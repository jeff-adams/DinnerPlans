using System;

namespace DinnerPlansAPI.Repositories;

public class MenuRepositoryException : Exception
{
    public MenuRepositoryException() { }
    public MenuRepositoryException(string message) : base(message) { }
    public MenuRepositoryException(string message, Exception inner) : base(message, inner) { }
}