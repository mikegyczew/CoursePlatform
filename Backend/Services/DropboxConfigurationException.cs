namespace Backend.Services;

public sealed class DropboxConfigurationException(string message)
    : Exception(message);
