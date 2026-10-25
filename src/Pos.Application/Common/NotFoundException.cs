namespace Pos.Application.Common;

public class NotFoundException(string entityName, object key)
    : Exception($"{entityName} '{key}' was not found.");
