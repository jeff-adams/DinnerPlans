using System;

namespace DinnerPlansAPI.Repositories;

public class TableRepositoryException : Exception
{
    public TableRepositoryException() { }
    public TableRepositoryException(string message) : base(message) { }
    public TableRepositoryException(string message, Exception inner) : base(message, inner) { }
}