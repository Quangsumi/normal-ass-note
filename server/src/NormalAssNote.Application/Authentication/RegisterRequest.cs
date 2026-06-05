namespace NormalAssNote.Application.Authentication;

public sealed record RegisterRequest(string UserName, string Password, string ConfirmPassword);
