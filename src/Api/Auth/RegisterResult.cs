namespace Api.Auth;

public record RegisterResult(bool Succeeded, IEnumerable<string> Errors);