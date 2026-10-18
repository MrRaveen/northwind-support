namespace SupportDesk.Application.Exceptions;

/// <summary>Thrown when the caller could not be authenticated. Surfaces as HTTP 401.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);