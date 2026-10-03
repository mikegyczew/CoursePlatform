namespace Backend.Services;

public sealed class DropboxLessonContentException(
    string message,
    Exception? innerException = null
) : Exception(message, innerException);
