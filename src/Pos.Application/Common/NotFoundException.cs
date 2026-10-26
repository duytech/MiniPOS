namespace Pos.Application.Common;

public class NotFoundException(string resource, Guid id) : Exception($"{resource} '{id}' was not found.");
