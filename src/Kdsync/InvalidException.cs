using System;
using System.IO;

namespace Kdsync
{
    public sealed class InvalidException : IOException
    {
        internal InvalidException(string message)
            : base(message)
        {
        }

        internal InvalidException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal static InvalidException MoreDataAvailable()
        {
            return new InvalidException("Completed reading a message while more data was available in the stream.");
        }

        internal static InvalidException TruncatedMessage()
        {
            return new InvalidException("While parsing a kdsync message, the input ended unexpectedly in the middle of a field.  This could mean either that the input has been truncated or that an embedded message misreported its own length.");
        }

        internal static InvalidException NegativeSize()
        {
            return new InvalidException("CodedInputStream encountered an embedded string or message which claimed to have negative size.");
        }

        internal static InvalidException MalformedVarint()
        {
            return new InvalidException("CodedInputStream encountered a malformed varint.");
        }

        //
        // 摘要:
        //     Creates an exception for an error condition of an invalid tag being encountered.
        internal static InvalidException InvalidTag()
        {
            return new InvalidException("Kdsync message contained an invalid tag (zero).");
        }

        internal static InvalidException InvalidWireType()
        {
            return new InvalidException("Kdsync message contained a tag with an invalid wire type.");
        }

        internal static InvalidException InvalidBase64(Exception innerException)
        {
            return new InvalidException("Invalid base64 data", innerException);
        }

        internal static InvalidException InvalidUtf8(Exception innerException)
        {
            return new InvalidException("String is invalid UTF-8.", innerException);
        }

        internal static InvalidException InvalidEndTag()
        {
            return new InvalidException("Kdsync message end-group tag did not match expected tag.");
        }

        internal static InvalidException RecursionLimitExceeded()
        {
            return new InvalidException("Kdsync message had too many levels of nesting.  May be malicious.  Use CodedInputStream.SetRecursionLimit() to increase the depth limit.");
        }

        internal static InvalidException JsonRecursionLimitExceeded()
        {
            return new InvalidException("Kdsync message had too many levels of nesting.  May be malicious.  Use JsonParser.Settings to increase the depth limit.");
        }

        internal static InvalidException SizeLimitExceeded()
        {
            return new InvalidException("Kdsync message was too large.  May be malicious.  Use CodedInputStream.SetSizeLimit() to increase the size limit.");
        }

        internal static InvalidException InvalidMessageStreamTag()
        {
            return new InvalidException("Stream of kdsync messages had invalid tag. Expected tag is length-delimited field 1.");
        }

        internal static InvalidException MissingFields()
        {
            return new InvalidException("Message was missing required fields");
        }
    }
}