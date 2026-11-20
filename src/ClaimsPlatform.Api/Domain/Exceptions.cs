namespace ClaimsPlatform.Api.Domain;

// Each exception maps to one HTTP status in the error-handling middleware (Step 4).
public class InvalidInputException(string message) : Exception(message);  // 400
public class UnauthorizedException(string message) : Exception(message); // 401
public class ForbiddenException(string message) : Exception(message);     // 403
public class NotFoundException(string message) : Exception(message);      // 404
public class DomainException(string message) : Exception(message);        // 409 (rule/state conflict)