namespace Eu.EDelivery.AS4.Exceptions;

/// <summary>
/// AS4 Error Codes
/// </summary>
public enum ErrorCode
{
    NotApplicable = 0,

    // ebMS Processing Errors
    Ebms0001 = 1,
    Ebms0002 = 2,
    Ebms0003 = 3,
    Ebms0004 = 4,
    Ebms0005 = 5,
    Ebms0006 = 6,
    Ebms0007 = 7,
    Ebms0008 = 8,
    Ebms0009 = 9,
    Ebms0010 = 10,
    Ebms0011 = 11,

    // Security Processing Errors
    Ebms0101 = 101,
    /// <summary>
    /// Decryption Failed.
    /// </summary>
    Ebms0102 = 102,
    Ebms0103 = 103,

    // Reliable Messaging Errors
    Ebms0201 = 201,
    Ebms0202 = 202,

    // Additional Features Errors
    Ebms0301 = 301,
    Ebms0302 = 302,
    Ebms0303 = 303,
}

internal static class ErrorCodeUtils
{
    public static string GetString(this ErrorCode errorCode) => $"EBMS:{(int)errorCode:0000}";

    public static string? GetCategory(ErrorCode errorCode) => errorCode switch
    {
        ErrorCode.Ebms0001 or ErrorCode.Ebms0002 or ErrorCode.Ebms0003 or ErrorCode.Ebms0004 or ErrorCode.Ebms0011 => "Content",
        ErrorCode.Ebms0007 or ErrorCode.Ebms0008 or ErrorCode.Ebms0009 => "Unpackaging",
        ErrorCode.Ebms0005 or ErrorCode.Ebms0006 or ErrorCode.Ebms0301 or ErrorCode.Ebms0302 or ErrorCode.Ebms0303 => "Communication",
        ErrorCode.Ebms0010 => "Processing",
        _ => null,
    };

    private static readonly IDictionary<ErrorAlias, ErrorCode> _errorCodes = new Dictionary<ErrorAlias, ErrorCode>
    {
        [ErrorAlias.ValueNotRecognized] = ErrorCode.Ebms0001,
        [ErrorAlias.FeatureNotSupported] = ErrorCode.Ebms0002,
        [ErrorAlias.ValueInconsistent] = ErrorCode.Ebms0003,
        [ErrorAlias.Other] = ErrorCode.Ebms0004,
        [ErrorAlias.ConnectionFailure] = ErrorCode.Ebms0005,
        [ErrorAlias.EmptyMessagePartitionChannel] = ErrorCode.Ebms0006,
        [ErrorAlias.MimeInconsistency] = ErrorCode.Ebms0007,
        [ErrorAlias.InvalidHeader] = ErrorCode.Ebms0009,
        [ErrorAlias.ProcessingModeMismatch] = ErrorCode.Ebms0010,
        [ErrorAlias.ExternalPayloadError] = ErrorCode.Ebms0011,
        [ErrorAlias.FailedAuthentication] = ErrorCode.Ebms0101,
        [ErrorAlias.FailedDecryption] = ErrorCode.Ebms0102,
        [ErrorAlias.PolicyNonCompliance] = ErrorCode.Ebms0103,
        [ErrorAlias.MissingReceipt] = ErrorCode.Ebms0301,
        [ErrorAlias.InvalidReceipt] = ErrorCode.Ebms0302,
        [ErrorAlias.DecompressionFailure] = ErrorCode.Ebms0303,
    };

    public static ErrorCode GetErrorCode(ErrorAlias alias) =>
        _errorCodes.TryGetValue(alias, out var code) ? code : ErrorCode.Ebms0004;
}
