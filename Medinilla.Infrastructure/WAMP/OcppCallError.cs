using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Medinilla.Infrastructure.WAMP;

public sealed class OcppCallError: BaseOcppMessage
{
    public static class ErrorCodes
    {
        /// <summary>
        /// Payload for Action is syntactically incorrect
        /// </summary>
        public const string FormatViolation = "FormatViolation";

        /// <summary>
        /// Any other error not covered by the more specific error codes in this table
        /// </summary>
        public const string GenericError = "GenericError";

        /// <summary>
        /// An internal error occurred and the receiver was not able to process the requested Action successfully
        /// </summary>
        public const string InternalError = "InternalError";

        /// <summary>
        /// A message with an Message Type Number received that is not supported by this implementation.
        /// </summary>
        public const string MessageTypeNotSupported = "MessageTypeNotSupported";

        /// <summary>
        /// Requested Action is not known by receiver
        /// </summary>
        public const string NotImplemented = "NotImplemented";

        /// <summary>
        /// Requested Action is recognized but not supported by the receiver
        /// </summary>
        public const string NotSupported = "NotSupported";

        /// <summary>
        /// Payload for Action is syntactically correct but at least one of the fields violates occurrence constraints
        /// </summary>
        public const string OccurrenceConstraintViolation = "OccurrenceConstraintViolation";

        /// <summary>
        /// Payload is syntactically correct but at least one field contains an invalid value
        /// </summary>
        public const string PropertyConstraintViolation = "PropertyConstraintViolation";

        /// <summary>
        /// Payload for Action is not conform the PDU structure
        /// </summary>
        public const string ProtocolError = "ProtocolError";

        /// <summary>
        /// Content of the call is not a valid RPC Request, for example: MessageId could not be read
        /// </summary>
        public const string RpcFrameworkError = "RpcFrameworkError";

        /// <summary>
        /// During the processing of Action a security issue occurred preventing receiver from completing the Action successfully
        /// </summary>
        public const string SecurityError = "SecurityError";

        /// <summary>
        /// Payload for Action is syntactically correct but at least one of the fields violates data type constraints(e.g. "somestring": 12)
        /// </summary>
        public const string TypeConstraintViolation = "TypeConstraintViolation";
    }

    public static OcppCallError InvalidMessageIdError = new OcppCallError("-1", ErrorCodes.RpcFrameworkError, "Couldn't parse MessageId");

    public static OcppCallError InternalError = new OcppCallError("-1", ErrorCodes.InternalError, "Internal RPC error.");

    public OcppCallError(string messageId, string errorCode, string errorDescription, string? errorDetails = null)
    {
        MessageType = OcppJMessageType.CALL_ERROR;
        MessageId = messageId;
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
        ErrorDetails = errorDetails;
    }

    public string ErrorCode { get; private set; }

    [MaxLength(255)]
    public string ErrorDescription { get; private set; }

    public string? ErrorDetails { get; private set; }

    public T? DetailsAs<T>() where T : class
    {
        if(ErrorDetails is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(ErrorDetails);
    }

    public byte[] ToByteArray()
    {
        var details = string.Compare(ErrorDetails, "null") != 0 ? ErrorDetails : "{}";
        var responseString = $"[{(int)MessageType},\"{MessageId}\",\"{ErrorCode}\",\"{ErrorDescription}\",{details}]";
#if DEBUG
        Console.WriteLine("-------------{0}OCPP Error Response: {1}{0}{2}{0}-------------", Environment.NewLine, responseString, details);
#endif
        return Encoding.UTF8.GetBytes(responseString);
    }
}
