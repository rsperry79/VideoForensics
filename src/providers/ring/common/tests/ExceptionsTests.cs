using System;
using System.Net;

using VideoForensics.Providers.Ring.Exceptions;

namespace VideoForensics.Providers.Ring.Common.Tests;

/// <summary>
/// Tests for DeviceUnknownException.
/// </summary>
public class DeviceUnknownException_Construction_Tests
{
    [Fact]
    public void Construction_Default_CreatesWithDefaultMessage()
    {
        var exception = new DeviceUnknownException();

        Assert.NotNull(exception);
        Assert.Contains("unknown", exception.Message);
        Assert.Contains("could not be found", exception.Message);
    }

    [Fact]
    public void Construction_WithDeviceId_IncludesDeviceIdInMessage()
    {
        int deviceId = 12345;
        var exception = new DeviceUnknownException(deviceId);

        Assert.Contains(deviceId.ToString(), exception.Message);
        Assert.Contains("could not be found", exception.Message);
    }

    [Fact]
    public void Construction_WithNullDeviceId_UsesUnknownInMessage()
    {
        var exception = new DeviceUnknownException((int?)null);

        Assert.Contains("unknown", exception.Message);
    }

    [Fact]
    public void Construction_WithDeviceIdAndWebException_SetsBothMessage()
    {
        int deviceId = 12345;
        var innerException = new WebException("Network error");
        var exception = new DeviceUnknownException(deviceId, innerException);

        Assert.Contains(deviceId.ToString(), exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Construction_WithUri_CreatesMessageWithUri()
    {
        var uri = new Uri("https://api.ring.com/locations/123");
        var exception = new DeviceUnknownException(uri);

        Assert.Contains(uri.ToString(), exception.Message);
        Assert.Contains("404", exception.Message);
    }

    [Fact]
    public void StatusCode_WhenInitialized_IsStoredAsProperty()
    {
        var exception = new DeviceUnknownException(123) { StatusCode = HttpStatusCode.NotFound };

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public void ResponseBody_WhenInitialized_IsStoredAsProperty()
    {
        string responseBody = "Device not found";
        var exception = new DeviceUnknownException(123) { ResponseBody = responseBody };

        Assert.Equal(responseBody, exception.ResponseBody);
    }
}

/// <summary>
/// Tests for DownloadFailedException.
/// </summary>
public class DownloadFailedException_Construction_Tests
{
    [Fact]
    public void Construction_WithUrl_CreatesWithUrlInMessage()
    {
        string url = "https://api.ring.com/recordings/123/video";
        var exception = new DownloadFailedException(url);

        Assert.Contains(url, exception.Message);
        Assert.Contains("failed", exception.Message);
    }

    [Fact]
    public void Construction_WithUrlAndWebException_SetsBothMessage()
    {
        string url = "https://api.ring.com/recordings/123/video";
        var innerException = new WebException("Connection timeout");
        var exception = new DownloadFailedException(url, innerException);

        Assert.Contains(url, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void StatusCode_WhenInitialized_IsStoredAsProperty()
    {
        string url = "https://api.ring.com/recordings/123/video";
        var exception = new DownloadFailedException(url) { StatusCode = HttpStatusCode.Forbidden };

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
    }

    [Fact]
    public void ResponseBody_WhenInitialized_IsStoredAsProperty()
    {
        string url = "https://api.ring.com/recordings/123/video";
        string responseBody = "Access denied";
        var exception = new DownloadFailedException(url) { ResponseBody = responseBody };

        Assert.Equal(responseBody, exception.ResponseBody);
    }
}

/// <summary>
/// Tests for SharingFailedException.
/// </summary>
public class SharingFailedException_Construction_Tests
{
    [Fact]
    public void Construction_WithId_CreatesWithIdInMessage()
    {
        string id = "event-123";
        var exception = new SharingFailedException(id);

        Assert.Contains(id, exception.Message);
        Assert.Contains("failed", exception.Message);
    }

    [Fact]
    public void Construction_WithIdAndWebException_SetsBothMessage()
    {
        string id = "event-123";
        var innerException = new WebException("Permission denied");
        var exception = new SharingFailedException(id, innerException);

        Assert.Contains(id, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Construction_WithEmptyId_StillIncludesIdInMessage()
    {
        string id = "";
        var exception = new SharingFailedException(id);

        Assert.Contains("failed", exception.Message);
    }

    [Fact]
    public void InnerException_WhenProvided_IsAccessible()
    {
        string id = "event-123";
        var webException = new WebException("Network error");
        var exception = new SharingFailedException(id, webException);

        Assert.NotNull(exception.InnerException);
        _ = Assert.IsType<WebException>(exception.InnerException);
    }
}

/// <summary>
/// Tests for ThrottledException.
/// </summary>
public class ThrottledException_Construction_Tests
{
    [Fact]
    public void Construction_Default_CreatesWithDefaultMessage()
    {
        var exception = new ThrottledException();

        Assert.NotNull(exception);
        Assert.Contains("too many requests", exception.Message);
        Assert.Contains("Try again in a few minutes", exception.Message);
        Assert.False(exception.IsHardBan);
    }

    [Fact]
    public void Construction_WithInnerException_SetsBothMessage()
    {
        var innerException = new WebException("429 Too Many Requests");
        var exception = new ThrottledException(innerException);

        Assert.Contains("too many requests", exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.False(exception.IsHardBan);
    }

    [Fact]
    public void Construction_WithCustomMessage_UsesCustomMessage()
    {
        string customMessage = "Hard throttle ban applied";
        var exception = new ThrottledException(customMessage);

        Assert.Equal(customMessage, exception.Message);
        Assert.False(exception.IsHardBan);
    }

    [Fact]
    public void Construction_WithMessageAndIsHardBan_SetsIsHardBanFlag()
    {
        string message = "Hard ban";
        var exception = new ThrottledException(message, isHardBan: true);

        Assert.True(exception.IsHardBan);
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void StatusCode_WhenInitialized_IsStoredAsProperty()
    {
        var exception = new ThrottledException() { StatusCode = HttpStatusCode.TooManyRequests };

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public void ResponseBody_WhenInitialized_IsStoredAsProperty()
    {
        string responseBody = "Rate limit exceeded";
        var exception = new ThrottledException() { ResponseBody = responseBody };

        Assert.Equal(responseBody, exception.ResponseBody);
    }

    [Fact]
    public void IsHardBan_DefaultFalse_IndicatesNotHardBan()
    {
        var exception = new ThrottledException();

        Assert.False(exception.IsHardBan);
    }

    [Fact]
    public void IsHardBan_SetToTrue_IndicatesHardBan()
    {
        var exception = new ThrottledException("Hard ban active", isHardBan: true);

        Assert.True(exception.IsHardBan);
    }
}

/// <summary>
/// Tests for UnexpectedOutcomeException.
/// </summary>
public class UnexpectedOutcomeException_Construction_Tests
{
    [Fact]
    public void Construction_WithStatusCodes_CreateWithExpectationMessage()
    {
        HttpStatusCode returned = HttpStatusCode.BadRequest;
        HttpStatusCode expected = HttpStatusCode.OK;
        var exception = new UnexpectedOutcomeException(returned, expected);

        Assert.NotNull(exception);
        Assert.Contains("different response", exception.Message);
        Assert.Equal(returned, exception.ReturnedStatusCode);
        Assert.Equal(expected, exception.ExpectedStatusCode);
    }

    [Fact]
    public void Construction_WithStatusCodesAndWebException_SetsBothMessage()
    {
        HttpStatusCode returned = HttpStatusCode.Unauthorized;
        HttpStatusCode expected = HttpStatusCode.OK;
        var innerException = new WebException("Network error");
        var exception = new UnexpectedOutcomeException(returned, expected, innerException);

        Assert.Contains("different response", exception.Message);
        Assert.Equal(returned, exception.ReturnedStatusCode);
        Assert.Equal(expected, exception.ExpectedStatusCode);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Construction_WithOnlyReturnedStatus_CreateGenericMessage()
    {
        HttpStatusCode returned = HttpStatusCode.InternalServerError;
        var exception = new UnexpectedOutcomeException(returned);

        Assert.NotNull(exception);
        Assert.Contains("unsuccessful", exception.Message);
        Assert.Equal(returned, exception.ReturnedStatusCode);
        Assert.Null(exception.ExpectedStatusCode);
    }

    [Fact]
    public void ReturnedStatusCode_WhenSet_IsAccessible()
    {
        HttpStatusCode returned = HttpStatusCode.NotFound;
        HttpStatusCode expected = HttpStatusCode.OK;
        var exception = new UnexpectedOutcomeException(returned, expected);

        Assert.Equal(HttpStatusCode.NotFound, exception.ReturnedStatusCode);
    }

    [Fact]
    public void ExpectedStatusCode_WhenSet_IsAccessible()
    {
        HttpStatusCode returned = HttpStatusCode.BadRequest;
        HttpStatusCode expected = HttpStatusCode.Created;
        var exception = new UnexpectedOutcomeException(returned, expected);

        Assert.Equal(HttpStatusCode.Created, exception.ExpectedStatusCode);
    }

    [Fact]
    public void ExpectedStatusCode_WhenNotSet_IsNull()
    {
        HttpStatusCode returned = HttpStatusCode.InternalServerError;
        var exception = new UnexpectedOutcomeException(returned);

        Assert.Null(exception.ExpectedStatusCode);
    }

    [Fact]
    public void ResponseBody_WhenInitialized_IsStoredAsProperty()
    {
        HttpStatusCode returned = HttpStatusCode.BadRequest;
        HttpStatusCode expected = HttpStatusCode.OK;
        string responseBody = "Invalid request";
        var exception = new UnexpectedOutcomeException(returned, expected) { ResponseBody = responseBody };

        Assert.Equal(responseBody, exception.ResponseBody);
    }

    [Fact]
    public void DifferentStatusCodes_CreateDifferentMessages()
    {
        var exception1 = new UnexpectedOutcomeException(HttpStatusCode.NotFound, HttpStatusCode.OK);
        var exception2 = new UnexpectedOutcomeException(HttpStatusCode.Forbidden, HttpStatusCode.OK);

        Assert.NotEqual(exception1.Message, exception2.Message);
    }

    [Fact]
    public void InnerException_WhenProvided_IsAccessible()
    {
        HttpStatusCode returned = HttpStatusCode.ServiceUnavailable;
        HttpStatusCode expected = HttpStatusCode.OK;
        var webException = new WebException("Service error");
        var exception = new UnexpectedOutcomeException(returned, expected, webException);

        Assert.NotNull(exception.InnerException);
        _ = Assert.IsType<WebException>(exception.InnerException);
    }
}
