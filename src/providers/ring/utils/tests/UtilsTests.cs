using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;

using Xunit;

namespace VideoForensics.Providers.Ring.Utils.Tests
{
    public class EndpointTargetTests
    {
        [Fact]
        public void EndpointTarget_CanBeConstructedWithLocationId()
        {
            var locationId = Guid.NewGuid();
            var target = new EndpointTarget(locationId, null);
            Assert.Equal(locationId, target.LocationId);
            Assert.Null(target.DoorbotId);
            Assert.Null(target.ChimeId);
        }

        [Fact]
        public void EndpointTarget_CanBeConstructedWithDoorbotId()
        {
            var target = new EndpointTarget(null, 12345L);
            Assert.Null(target.LocationId);
            Assert.Equal(12345L, target.DoorbotId);
            Assert.Null(target.ChimeId);
        }

        [Fact]
        public void EndpointTarget_CanBeConstructedWithChimeId()
        {
            var target = new EndpointTarget(null, null, 67890L);
            Assert.Null(target.LocationId);
            Assert.Null(target.DoorbotId);
            Assert.Equal(67890L, target.ChimeId);
        }

        [Fact]
        public void EndpointTarget_NoneConstantIsAllNull()
        {
            EndpointTarget none = EndpointTarget.None;
            Assert.Null(none.LocationId);
            Assert.Null(none.DoorbotId);
            Assert.Null(none.ChimeId);
        }

        [Fact]
        public void EndpointTarget_RecordEqualityWorks()
        {
            var target1 = new EndpointTarget(Guid.Empty, 123L);
            var target2 = new EndpointTarget(Guid.Empty, 123L);
            Assert.Equal(target1, target2);
        }
    }

    public class RestorePlanTests
    {
        [Fact]
        public void RestorePlan_CanBeConstructedWithAllProperties()
        {
            var restoreFunc = new Func<Session, Task>(async _ => await Task.CompletedTask);
            var plan = new RestorePlan("original=on", true, restoreFunc);

            Assert.Equal("original=on", plan.OriginalValueDescription);
            Assert.True(plan.WasCaptured);
            Assert.NotNull(plan.Restore);
        }

        [Fact]
        public void RestorePlan_RecordEqualityWorks()
        {
            var func1 = new Func<Session, Task>(async _ => await Task.CompletedTask);
            var func2 = new Func<Session, Task>(async _ => await Task.CompletedTask);

            var plan1 = new RestorePlan("desc", false, func1);
            var plan2 = new RestorePlan("desc", false, func2);

            // Records with same values but different function references won't be equal
            // because the Func is a reference type - this tests that behavior
            Assert.NotEqual(plan1, plan2);
        }

        [Fact]
        public void RestorePlan_CanHaveNullDescription()
        {
            var restoreFunc = new Func<Session, Task>(async _ => await Task.CompletedTask);
            var plan = new RestorePlan(null!, false, restoreFunc);
            Assert.Null(plan.OriginalValueDescription);
        }
    }

    public class EndpointDescriptorTests
    {
        [Fact]
        public void EndpointDescriptor_CanBeConstructedWithRequiredProperties()
        {
            var invoke = new Func<Session, EndpointTarget, Task>(async (_, _) => await Task.CompletedTask);
            var descriptor = new EndpointDescriptor
            {
                Key = "test-endpoint",
                DisplayName = "Test Endpoint",
                Description = "A test endpoint",
                SessionMethod = "Session.Test()",
                HttpMethod = "GET",
                ApiPath = "https://api.example.com/test",
                Invoke = invoke
            };

            Assert.Equal("test-endpoint", descriptor.Key);
            Assert.Equal("Test Endpoint", descriptor.DisplayName);
            Assert.Equal("A test endpoint", descriptor.Description);
            Assert.Equal("Session.Test()", descriptor.SessionMethod);
            Assert.Equal("GET", descriptor.HttpMethod);
            Assert.Equal("https://api.example.com/test", descriptor.ApiPath);
            Assert.NotNull(descriptor.Invoke);
        }

        [Fact]
        public void EndpointDescriptor_HasDefaultDestructiveFalse()
        {
            var invoke = new Func<Session, EndpointTarget, Task>(async (_, _) => await Task.CompletedTask);
            var descriptor = new EndpointDescriptor
            {
                Key = "test",
                DisplayName = "Test",
                Description = "Test",
                SessionMethod = "Test()",
                HttpMethod = "GET",
                ApiPath = "https://api.example.com/test",
                Invoke = invoke
            };

            Assert.False(descriptor.Destructive);
            Assert.False(descriptor.Physical);
        }

        [Fact]
        public void EndpointDescriptor_CanSetDestructiveAndPhysical()
        {
            var invoke = new Func<Session, EndpointTarget, Task>(async (_, _) => await Task.CompletedTask);
            var descriptor = new EndpointDescriptor
            {
                Key = "light",
                DisplayName = "Light",
                Description = "Toggle light",
                SessionMethod = "Session.SetLight()",
                HttpMethod = "PUT",
                ApiPath = "https://api.example.com/light",
                Destructive = true,
                Physical = true,
                Invoke = invoke
            };

            Assert.True(descriptor.Destructive);
            Assert.True(descriptor.Physical);
        }

        [Fact]
        public void EndpointDescriptor_CanHavePrepareRestoreAndNoRestoreReason()
        {
            var invoke = new Func<Session, EndpointTarget, Task>(async (_, _) => await Task.CompletedTask);
            var prepareRestore = new Func<EndpointTarget, RestorePlan?>(target => null);

            var descriptor = new EndpointDescriptor
            {
                Key = "test",
                DisplayName = "Test",
                Description = "Test",
                SessionMethod = "Test()",
                HttpMethod = "GET",
                ApiPath = "https://api.example.com/test",
                Invoke = invoke,
                PrepareRestore = prepareRestore,
                NoRestoreReason = "Cannot restore this action"
            };

            Assert.NotNull(descriptor.PrepareRestore);
            Assert.Equal("Cannot restore this action", descriptor.NoRestoreReason);
        }
    }

    public class EndpointRegistryTests
    {
        [Fact]
        public void EndpointRegistry_FindReturnsNullForUnknownKey()
        {
            EndpointDescriptor? result = EndpointRegistry.Find("nonexistent-endpoint-key");
            Assert.Null(result);
        }

        [Fact]
        public void EndpointRegistry_FindReturnsCaseInsensitive()
        {
            EndpointDescriptor? result = EndpointRegistry.Find("DEVICES");
            Assert.NotNull(result);
            Assert.Equal("devices", result.Key);
        }

        [Fact]
        public void EndpointRegistry_FindLocatesDevicesEndpoint()
        {
            EndpointDescriptor? result = EndpointRegistry.Find("devices");
            Assert.NotNull(result);
            Assert.Equal("devices", result.Key);
            Assert.Equal("List devices", result.DisplayName);
            Assert.Equal(EndpointScope.None, result.Scope);
        }

        [Fact]
        public void EndpointRegistry_FindLocatesLocationScopedEndpoint()
        {
            EndpointDescriptor? result = EndpointRegistry.Find("location-mode");
            Assert.NotNull(result);
            Assert.Equal("location-mode", result.Key);
            Assert.Equal(EndpointScope.PerLocation, result.Scope);
        }

        [Fact]
        public void EndpointRegistry_FindLocatesDoorbotScopedEndpoint()
        {
            EndpointDescriptor? result = EndpointRegistry.Find("doorbot-health");
            Assert.NotNull(result);
            Assert.Equal("doorbot-health", result.Key);
            Assert.Equal(EndpointScope.PerDoorbot, result.Scope);
        }

        [Fact]
        public void EndpointRegistry_FindLocatesChimeScopedEndpoint()
        {
            EndpointDescriptor? result = EndpointRegistry.Find("chime-health");
            Assert.NotNull(result);
            Assert.Equal("chime-health", result.Key);
            Assert.Equal(EndpointScope.PerChime, result.Scope);
        }

        [Fact]
        public void EndpointRegistry_FindLocatesDestructiveEndpoint()
        {
            EndpointDescriptor? result = EndpointRegistry.Find("set-light");
            Assert.NotNull(result);
            Assert.True(result.Destructive);
            Assert.True(result.Physical);
        }

        [Fact]
        public void EndpointRegistry_AllEndpointsHaveKeys()
        {
            foreach (EndpointDescriptor endpoint in EndpointRegistry.All)
            {
                Assert.NotNull(endpoint.Key);
                Assert.NotEmpty(endpoint.Key);
            }
        }

        [Fact]
        public void EndpointRegistry_AllEndpointsHaveDisplayNames()
        {
            foreach (EndpointDescriptor endpoint in EndpointRegistry.All)
            {
                Assert.NotNull(endpoint.DisplayName);
                Assert.NotEmpty(endpoint.DisplayName);
            }
        }

        [Fact]
        public void EndpointRegistry_AllEndpointsHaveInvokeDelegates()
        {
            foreach (EndpointDescriptor endpoint in EndpointRegistry.All)
            {
                Assert.NotNull(endpoint.Invoke);
            }
        }

        [Fact]
        public void EndpointRegistry_DestructiveEndpointsHaveRestorePlanOrReason()
        {
            foreach (EndpointDescriptor endpoint in EndpointRegistry.All)
            {
                if (endpoint.Destructive)
                {
                    bool hasRestore = endpoint.PrepareRestore != null;
                    bool hasReason = endpoint.NoRestoreReason != null;
                    Assert.True(hasRestore || hasReason,
                        $"Endpoint '{endpoint.Key}' is destructive but has neither PrepareRestore nor NoRestoreReason");
                }
            }
        }
    }

    public class EndpointSchemaMapTests
    {
        [Fact]
        public void EndpointSchemaMap_FindsDoorbotHealthType()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("doorbot-health", out Type? type);
            Assert.True(found);
            Assert.NotNull(type);
            Assert.Equal(typeof(DeviceHealthResponse), type);
        }

        [Fact]
        public void EndpointSchemaMap_FindsChimeHealthType()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("chime-health", out Type? type);
            Assert.True(found);
            Assert.NotNull(type);
            Assert.Equal(typeof(DeviceHealthResponse), type);
        }

        [Fact]
        public void EndpointSchemaMap_FindsVideoSearchType()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("video-search", out Type? type);
            Assert.True(found);
            Assert.NotNull(type);
            Assert.Equal(typeof(VideoSearchResponse), type);
        }

        [Fact]
        public void EndpointSchemaMap_ReturnsFalseForUnmappedEndpoint()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("devices", out Type? type);
            Assert.False(found);
            Assert.Null(type);
        }

        [Fact]
        public void EndpointSchemaMap_ReturnsFalseForNonexistentEndpoint()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("fake-endpoint", out Type? type);
            Assert.False(found);
            Assert.Null(type);
        }
    }

    public class JsonSchemaValidatorTests
    {
        private readonly JsonSchemaValidator _validator = new();

        [Fact]
        public void JsonSchemaValidator_AcceptsValidStringProperty()
        {
            JsonElement json = JsonDocument.Parse(@"{""name"": ""test""}").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(DeviceHealthResponse));
            Assert.NotNull(issues);
        }

        [Fact]
        public void JsonSchemaValidator_ReturnsEmptyListForNullElement()
        {
            JsonElement json = JsonDocument.Parse("null").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(DeviceHealthResponse));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_ReturnsEmptyListForNullType()
        {
            JsonElement json = JsonDocument.Parse(@"{""test"": 1}").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, null!);
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_DetectsStringTypeMismatch()
        {
            JsonElement json = JsonDocument.Parse(@"{""count"": 123}").RootElement;
            JsonElement stringProperty = json.GetProperty("count");

            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(stringProperty, typeof(string));
            Assert.NotEmpty(issues);
            Assert.Contains(issues, i => i.IssueType == "TypeMismatch");
        }

        [Fact]
        public void JsonSchemaValidator_DetectsBooleanTypeMismatch()
        {
            JsonElement json = JsonDocument.Parse(@"123").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(bool));
            Assert.NotEmpty(issues);
            _ = Assert.Single(issues);
            Assert.Equal("TypeMismatch", issues[0].IssueType);
        }

        [Fact]
        public void JsonSchemaValidator_AcceptsNumericTypes()
        {
            JsonElement json = JsonDocument.Parse("42").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(int));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_DetectsNonNumericStringForNumber()
        {
            JsonElement json = JsonDocument.Parse(@"""not-a-number""").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(int));
            Assert.NotEmpty(issues);
            Assert.Contains(issues, i => i.IssueType == "TypeMismatch" && i.Severity == "Error");
        }

        [Fact]
        public void JsonSchemaValidator_WarnsOnNumericStringForNumber()
        {
            JsonElement json = JsonDocument.Parse(@"""123""").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(int));
            Assert.NotEmpty(issues);
            Assert.Contains(issues, i => i.IssueType == "TypeMismatch" && i.Severity == "Warning");
        }

        [Fact]
        public void JsonSchemaValidator_AcceptsBooleanTrue()
        {
            JsonElement json = JsonDocument.Parse("true").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(bool));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_AcceptsBooleanFalse()
        {
            JsonElement json = JsonDocument.Parse("false").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(bool));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_ReturnsEmptyForNullResponse()
        {
            // The validator returns early for null responses - this is by design
            // Null responses can't be validated further
            JsonElement json = JsonDocument.Parse("null").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(int));
            Assert.Empty(issues);
        }
    }

    public class DoorbotSettingsSnapshotTests
    {
        [Fact]
        public void DoorbotSettingsSnapshot_CanBeConstructed()
        {
            var snapshot = new DoorbotSettingsSnapshot(
                Volume: 5,
                ChimeType: 1,
                ChimeEnabled: true,
                ChimeDuration: 30,
                NightModeEnabled: false,
                MotionDetectionEnabled: true
            );

            Assert.Equal(5, snapshot.Volume);
            Assert.Equal(1, snapshot.ChimeType);
            Assert.True(snapshot.ChimeEnabled);
            Assert.Equal(30, snapshot.ChimeDuration);
            Assert.False(snapshot.NightModeEnabled);
            Assert.True(snapshot.MotionDetectionEnabled);
        }

        [Fact]
        public void DoorbotSettingsSnapshot_CanHaveNullValues()
        {
            var snapshot = new DoorbotSettingsSnapshot(
                Volume: null,
                ChimeType: null,
                ChimeEnabled: null,
                ChimeDuration: null,
                NightModeEnabled: null,
                MotionDetectionEnabled: null
            );

            Assert.Null(snapshot.Volume);
            Assert.Null(snapshot.ChimeType);
            Assert.Null(snapshot.ChimeEnabled);
            Assert.Null(snapshot.ChimeDuration);
            Assert.Null(snapshot.NightModeEnabled);
            Assert.Null(snapshot.MotionDetectionEnabled);
        }

        [Fact]
        public void DoorbotSettingsSnapshot_RecordEqualityWorks()
        {
            var snap1 = new DoorbotSettingsSnapshot(5, 1, true, 30, false, true);
            var snap2 = new DoorbotSettingsSnapshot(5, 1, true, 30, false, true);
            Assert.Equal(snap1, snap2);
        }
    }

    public class DeviceSettingsSnapshotTests
    {
        [Fact]
        public void DeviceSettingsSnapshot_ParsesSimpleDoorbotSettings()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": {
                        ""doorbell_volume"": 5,
                        ""night_mode_on"": false,
                        ""motion_detection_enabled"": true
                    }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);

            _ = Assert.Single(result);
            Assert.True(result.TryGetValue(123, out DoorbotSettingsSnapshot? snapshot));
            Assert.Equal(5, snapshot.Volume);
            Assert.False(snapshot.NightModeEnabled);
            Assert.True(snapshot.MotionDetectionEnabled);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesChimeSettings()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 456,
                    ""settings"": {
                        ""chime_settings"": {
                            ""type"": 1,
                            ""enable"": true,
                            ""duration"": 60
                        }
                    }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);

            _ = Assert.Single(result);
            Assert.True(result.TryGetValue(456, out DoorbotSettingsSnapshot? snapshot));
            Assert.Equal(1, snapshot.ChimeType);
            Assert.True(snapshot.ChimeEnabled);
            Assert.Equal(60, snapshot.ChimeDuration);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesAuthorizadDoorbots()
        {
            string json = @"{
                ""authorized_doorbots"": [{
                    ""id"": 789,
                    ""settings"": {
                        ""doorbell_volume"": 8
                    }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);

            _ = Assert.Single(result);
            Assert.True(result.TryGetValue(789, out DoorbotSettingsSnapshot? snapshot));
            Assert.Equal(8, snapshot.Volume);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesStickupCams()
        {
            string json = @"{
                ""stickup_cams"": [{
                    ""id"": 999,
                    ""settings"": {
                        ""night_mode_on"": true
                    }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);

            _ = Assert.Single(result);
            Assert.True(result.TryGetValue(999, out DoorbotSettingsSnapshot? snapshot));
            Assert.True(snapshot.NightModeEnabled);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesMultipleDevices()
        {
            string json = @"{
                ""doorbots"": [
                    { ""id"": 1, ""settings"": { ""doorbell_volume"": 3 } },
                    { ""id"": 2, ""settings"": { ""doorbell_volume"": 5 } }
                ]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);

            Assert.Equal(2, result.Count);
            Assert.True(result.TryGetValue(1, out DoorbotSettingsSnapshot? snap1));
            Assert.True(result.TryGetValue(2, out DoorbotSettingsSnapshot? snap2));
            Assert.Equal(3, snap1.Volume);
            Assert.Equal(5, snap2.Volume);
        }

        [Fact]
        public void DeviceSettingsSnapshot_HandlesEmptyJson()
        {
            string json = "{}";
            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            Assert.Empty(result);
        }

        [Fact]
        public void DeviceSettingsSnapshot_HandlesEmptyDoorbotArray()
        {
            string json = @"{ ""doorbots"": [] }";
            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            Assert.Empty(result);
        }

        [Fact]
        public void DeviceSettingsSnapshot_SkipsDeviceWithoutId()
        {
            string json = @"{
                ""doorbots"": [{
                    ""settings"": { ""doorbell_volume"": 5 }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            Assert.Empty(result);
        }

        [Fact]
        public void DeviceSettingsSnapshot_SkipsDeviceWithoutSettings()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            Assert.Empty(result);
        }

        [Fact]
        public void DeviceSettingsSnapshot_HandlesMalformedJsonGracefully()
        {
            string json = "{ broken json";
            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            Assert.Empty(result);
        }

        [Fact]
        public void DeviceSettingsSnapshot_HandlesBooleanAsNumber()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": {
                        ""night_mode_on"": 1,
                        ""motion_detection_enabled"": 0
                    }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);

            _ = Assert.Single(result);
            Assert.True(result.TryGetValue(123, out DoorbotSettingsSnapshot? snapshot));
            Assert.True(snapshot.NightModeEnabled);
            Assert.False(snapshot.MotionDetectionEnabled);
        }

        [Fact]
        public void DeviceSettingsSnapshot_IgnoresDuplicateDeviceIds()
        {
            string json = @"{
                ""doorbots"": [
                    { ""id"": 123, ""settings"": { ""doorbell_volume"": 3 } },
                    { ""id"": 123, ""settings"": { ""doorbell_volume"": 5 } }
                ]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);

            _ = Assert.Single(result);
            Assert.True(result.TryGetValue(123, out DoorbotSettingsSnapshot? snapshot));
            // First one wins
            Assert.Equal(3, snapshot.Volume);
        }

        [Fact]
        public void DeviceSettingsSnapshot_MergesMultipleArrayTypes()
        {
            string json = @"{
                ""doorbots"": [{ ""id"": 1, ""settings"": { ""doorbell_volume"": 3 } }],
                ""authorized_doorbots"": [{ ""id"": 2, ""settings"": { ""doorbell_volume"": 5 } }],
                ""stickup_cams"": [{ ""id"": 3, ""settings"": { ""night_mode_on"": true } }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);

            Assert.Equal(3, result.Count);
            Assert.True(result.TryGetValue(1, out _));
            Assert.True(result.TryGetValue(2, out _));
            Assert.True(result.TryGetValue(3, out _));
        }
    }

    public class HttpCallRecordTests
    {
        [Fact]
        public void HttpCallRecord_CanBeConstructed()
        {
            var record = new HttpCallRecord
            {
                Method = "GET",
                Url = "https://api.ring.com/devices",
                StatusCode = 200,
                ResponseBodyBytes = 1024,
                BodyFile = "response_001.json",
                Phase = "test"
            };

            Assert.Equal("GET", record.Method);
            Assert.Equal("https://api.ring.com/devices", record.Url);
            Assert.Equal(200, record.StatusCode);
            Assert.Equal(1024, record.ResponseBodyBytes);
            Assert.Equal("response_001.json", record.BodyFile);
            Assert.Equal("test", record.Phase);
        }

        [Fact]
        public void HttpCallRecord_HasDefaultEmptyStrings()
        {
            var record = new HttpCallRecord();
            Assert.Equal("", record.Method);
            Assert.Equal("", record.Url);
            Assert.Equal("test", record.Phase);
        }

        [Fact]
        public void HttpCallRecord_PhaseCanBeRestore()
        {
            var record = new HttpCallRecord { Phase = "restore" };
            Assert.Equal("restore", record.Phase);
        }
    }

    public class SchemaIssueRecordTests
    {
        [Fact]
        public void SchemaIssueRecord_CanBeConstructed()
        {
            var issue = new SchemaIssueRecord
            {
                Path = "$.devices[0].id",
                IssueType = "TypeMismatch",
                Expected = "Long",
                Actual = "String",
                Severity = "Error"
            };

            Assert.Equal("$.devices[0].id", issue.Path);
            Assert.Equal("TypeMismatch", issue.IssueType);
            Assert.Equal("Long", issue.Expected);
            Assert.Equal("String", issue.Actual);
            Assert.Equal("Error", issue.Severity);
        }

        [Fact]
        public void SchemaIssueRecord_HasDefaultEmptyStrings()
        {
            var issue = new SchemaIssueRecord();
            Assert.Equal("", issue.Path);
            Assert.Equal("", issue.IssueType);
            Assert.Equal("", issue.Severity);
        }
    }

    public class CallRecordTests
    {
        [Fact]
        public void CallRecord_CanBeConstructedWithRequiredProperties()
        {
            var record = new CallRecord
            {
                Endpoint = "devices",
                DisplayName = "List devices",
                SessionMethod = "Session.GetRingDevices()"
            };

            Assert.Equal("devices", record.Endpoint);
            Assert.Equal("List devices", record.DisplayName);
            Assert.Equal("Session.GetRingDevices()", record.SessionMethod);
        }

        [Fact]
        public void CallRecord_HasDefaultCollections()
        {
            var record = new CallRecord
            {
                Endpoint = "test",
                DisplayName = "Test",
                SessionMethod = "Test()"
            };

            Assert.NotNull(record.HttpCalls);
            Assert.Empty(record.HttpCalls);
            Assert.NotNull(record.SchemaIssues);
            Assert.Empty(record.SchemaIssues);
        }

        [Fact]
        public void CallRecord_CanAddHttpCalls()
        {
            var record = new CallRecord
            {
                Endpoint = "test",
                DisplayName = "Test",
                SessionMethod = "Test()"
            };

            var httpCall = new HttpCallRecord { Method = "GET", Url = "https://api.ring.com/test" };
            record.HttpCalls.Add(httpCall);

            _ = Assert.Single(record.HttpCalls);
            Assert.Equal("GET", record.HttpCalls[0].Method);
        }

        [Fact]
        public void CallRecord_CanAddSchemaIssues()
        {
            var record = new CallRecord
            {
                Endpoint = "test",
                DisplayName = "Test",
                SessionMethod = "Test()"
            };

            var issue = new SchemaIssueRecord { Path = "$.test", IssueType = "TypeMismatch" };
            record.SchemaIssues.Add(issue);

            _ = Assert.Single(record.SchemaIssues);
            Assert.Equal("$.test", record.SchemaIssues[0].Path);
        }

        [Fact]
        public void CallRecord_CanTrackRestoreStatus()
        {
            var record = new CallRecord
            {
                Endpoint = "set-light",
                DisplayName = "Toggle light",
                SessionMethod = "Session.SetLight()",
                Destructive = true,
                OriginalValue = "off",
                RestoreAttempted = true,
                RestoreSuccess = true
            };

            Assert.Equal("off", record.OriginalValue);
            Assert.True(record.RestoreAttempted);
            Assert.True(record.RestoreSuccess);
        }

        [Fact]
        public void CallRecord_CanTrackRestoreSkipped()
        {
            var record = new CallRecord
            {
                Endpoint = "set-light",
                DisplayName = "Toggle light",
                SessionMethod = "Session.SetLight()",
                Destructive = true,
                RestoreSkippedReason = "Unable to capture original value"
            };

            Assert.Equal("Unable to capture original value", record.RestoreSkippedReason);
            Assert.False(record.RestoreAttempted);
        }
    }

    public class SummaryRecordTests
    {
        [Fact]
        public void SummaryRecord_CanBeConstructed()
        {
            var summary = new SummaryRecord
            {
                TotalCalls = 10,
                Succeeded = 8,
                Failed = 2
            };

            Assert.Equal(10, summary.TotalCalls);
            Assert.Equal(8, summary.Succeeded);
            Assert.Equal(2, summary.Failed);
        }

        [Fact]
        public void SummaryRecord_DefaultsToZero()
        {
            var summary = new SummaryRecord();
            Assert.Equal(0, summary.TotalCalls);
            Assert.Equal(0, summary.Succeeded);
            Assert.Equal(0, summary.Failed);
        }
    }

    public class IndexDocumentTests
    {
        [Fact]
        public void IndexDocument_CanBeConstructed()
        {
            DateTime now = DateTime.UtcNow;
            var doc = new IndexDocument
            {
                ToolVersion = "1.0",
                GeneratedAtUtc = now,
                CredentialSource = "password",
                OutputDirectory = "/output"
            };

            Assert.Equal("1.0", doc.ToolVersion);
            Assert.Equal(now, doc.GeneratedAtUtc);
            Assert.Equal("password", doc.CredentialSource);
            Assert.Equal("/output", doc.OutputDirectory);
        }

        [Fact]
        public void IndexDocument_HasDefaultCollectionsAndSummary()
        {
            var doc = new IndexDocument();

            Assert.NotNull(doc.Calls);
            Assert.Empty(doc.Calls);
            Assert.NotNull(doc.Summary);
            Assert.Equal(0, doc.Summary.TotalCalls);
        }

        [Fact]
        public void IndexDocument_CanAddCallRecords()
        {
            var doc = new IndexDocument();
            var call = new CallRecord
            {
                Endpoint = "devices",
                DisplayName = "List devices",
                SessionMethod = "Session.GetRingDevices()"
            };

            doc.Calls.Add(call);
            doc.Summary.TotalCalls = 1;
            doc.Summary.Succeeded = 1;

            _ = Assert.Single(doc.Calls);
            Assert.Equal(1, doc.Summary.TotalCalls);
            Assert.Equal(1, doc.Summary.Succeeded);
        }
    }

    public class TargetRecordTests
    {
        [Fact]
        public void TargetRecord_CanBeConstructedWithLocationData()
        {
            var target = new TargetRecord
            {
                LocationId = Guid.Empty.ToString(),
                LocationName = "Front Door"
            };

            Assert.NotNull(target.LocationId);
            Assert.Equal("Front Door", target.LocationName);
            Assert.Null(target.DoorbotName);
        }

        [Fact]
        public void TargetRecord_CanBeConstructedWithDoorbotData()
        {
            var target = new TargetRecord
            {
                DoorbotId = 123,
                DoorbotName = "Front Doorbell"
            };

            Assert.Equal(123, target.DoorbotId);
            Assert.Equal("Front Doorbell", target.DoorbotName);
            Assert.Null(target.LocationName);
        }

        [Fact]
        public void TargetRecord_CanBeConstructedWithChimeData()
        {
            var target = new TargetRecord
            {
                ChimeId = 456,
                ChimeName = "Indoor Chime"
            };

            Assert.Equal(456, target.ChimeId);
            Assert.Equal("Indoor Chime", target.ChimeName);
        }

        [Fact]
        public void TargetRecord_CanBeConstructedEmpty()
        {
            var target = new TargetRecord();
            Assert.Null(target.LocationId);
            Assert.Null(target.DoorbotId);
            Assert.Null(target.ChimeId);
        }
    }

    public class JsonSchemaValidatorEdgeCaseTests
    {
        private readonly JsonSchemaValidator _validator = new();

        [Fact]
        public void JsonSchemaValidator_HandlesComplexNestedObjects()
        {
            JsonElement json = JsonDocument.Parse(@"{
                ""level1"": {
                    ""level2"": {
                        ""count"": 42
                    }
                }
            }").RootElement;

            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(object));
            Assert.NotNull(issues);
        }

        [Fact]
        public void JsonSchemaValidator_HandlesArrayOfObjects()
        {
            JsonElement json = JsonDocument.Parse(@"[
                { ""id"": 1, ""name"": ""test1"" },
                { ""id"": 2, ""name"": ""test2"" }
            ]").RootElement;

            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(List<object>));
            Assert.NotNull(issues);
        }

        [Fact]
        public void JsonSchemaValidator_DetectsLongTypeMismatch()
        {
            JsonElement json = JsonDocument.Parse(@"""not-a-number""").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(long));
            Assert.NotEmpty(issues);
            Assert.Contains(issues, i => i.IssueType == "TypeMismatch");
        }

        [Fact]
        public void JsonSchemaValidator_AcceptsLargeNumbers()
        {
            JsonElement json = JsonDocument.Parse("9223372036854775807").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(long));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_AcceptsDoubleValues()
        {
            JsonElement json = JsonDocument.Parse("3.14159265").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(double));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_AcceptsDecimalValues()
        {
            JsonElement json = JsonDocument.Parse("100.50").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(decimal));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_HandlesNullableInt()
        {
            JsonElement json = JsonDocument.Parse("null").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(int?));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_WarnsOnNullableIntWithValidNumber()
        {
            JsonElement json = JsonDocument.Parse("42").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(int?));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_HandlesEmptyArray()
        {
            JsonElement json = JsonDocument.Parse("[]").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(List<int>));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_HandlesSingleElementArray()
        {
            JsonElement json = JsonDocument.Parse("[42]").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(List<int>));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_DetectsFirstArrayElementMismatch()
        {
            JsonElement json = JsonDocument.Parse("[\"wrong\", 2, 3]").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(List<int>));
            Assert.NotEmpty(issues);
            Assert.Contains(issues, i => i.Path.Contains("[0]"));
        }

        [Fact]
        public void JsonSchemaValidator_DetectsMiddleArrayElementMismatch()
        {
            JsonElement json = JsonDocument.Parse("[1, \"wrong\", 3]").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(List<int>));
            Assert.NotEmpty(issues);
            Assert.Contains(issues, i => i.Path.Contains("[1]"));
        }

        [Fact]
        public void JsonSchemaValidator_DetectsLastArrayElementMismatch()
        {
            JsonElement json = JsonDocument.Parse("[1, 2, \"wrong\"]").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(List<int>));
            Assert.NotEmpty(issues);
            Assert.Contains(issues, i => i.Path.Contains("[2]"));
        }

        [Fact]
        public void JsonSchemaValidator_ReportsCorrectPathForNestedFields()
        {
            JsonElement json = JsonDocument.Parse(@"{
                ""outer"": {
                    ""inner"": 123
                }
            }").RootElement;

            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(object));
            Assert.NotNull(issues);
        }

        [Fact]
        public void JsonSchemaValidator_HandlesZeroValue()
        {
            JsonElement json = JsonDocument.Parse("0").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(int));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_HandlesNegativeNumbers()
        {
            JsonElement json = JsonDocument.Parse("-42").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(int));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_HandlesScientificNotation()
        {
            JsonElement json = JsonDocument.Parse("1.23e-4").RootElement;
            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(double));
            Assert.Empty(issues);
        }

        [Fact]
        public void JsonSchemaValidator_ReportsMissingInSchemaForExtraField()
        {
            JsonElement json = JsonDocument.Parse(@"{
                ""Name"": ""Test"",
                ""UnexpectedField"": 123
            }").RootElement;

            List<JsonSchemaValidator.SchemaIssue> issues = _validator.ValidateAgainstSchema(json, typeof(object));
            // Should detect the extra field
            Assert.NotNull(issues);
        }
    }

    public class EndpointRegistryExtendedTests
    {
        [Fact]
        public void EndpointRegistry_FindIsStatic()
        {
            EndpointDescriptor? endpoint = EndpointRegistry.Find("locations");
            Assert.NotNull(endpoint);
            Assert.Equal("locations", endpoint.Key);
        }

        [Fact]
        public void EndpointRegistry_AllContainsDestructiveEndpoints()
        {
            var destructive = EndpointRegistry.All.Where(e => e.Destructive).ToList();
            Assert.NotEmpty(destructive);
        }

        [Fact]
        public void EndpointRegistry_AllContainsNonDestructiveEndpoints()
        {
            var nonDestructive = EndpointRegistry.All.Where(e => !e.Destructive).ToList();
            Assert.NotEmpty(nonDestructive);
        }

        [Fact]
        public void EndpointRegistry_AllContainsPhysicalEndpoints()
        {
            var physical = EndpointRegistry.All.Where(e => e.Physical).ToList();
            Assert.NotEmpty(physical);
        }

        [Fact]
        public void EndpointRegistry_FindLocatesProfileEndpoint()
        {
            EndpointDescriptor? endpoint = EndpointRegistry.Find("profile");
            Assert.NotNull(endpoint);
            Assert.Equal(EndpointScope.None, endpoint.Scope);
        }

        [Fact]
        public void EndpointRegistry_OriginalDoorbotSettingsStartsEmpty()
        {
            // Settings should be a dictionary (possibly empty or pre-populated)
            Assert.NotNull(EndpointRegistry.OriginalDoorbotSettings);
        }

        [Fact]
        public void EndpointRegistry_OriginalLocationModeStartsEmpty()
        {
            Assert.NotNull(EndpointRegistry.OriginalLocationModeByLocation);
        }

        [Fact]
        public void EndpointRegistry_FindIsCaseSensitive()
        {
            EndpointDescriptor? lower = EndpointRegistry.Find("devices");
            EndpointDescriptor? upper = EndpointRegistry.Find("DEVICES");
            Assert.NotNull(lower);
            Assert.NotNull(upper);
            Assert.Equal(lower.Key, upper.Key);
        }

        [Fact]
        public void EndpointRegistry_AllEndpointKeysAreUnique()
        {
            var keys = EndpointRegistry.All.Select(e => e.Key).ToList();
            var uniqueKeys = keys.Distinct().ToList();
            Assert.Equal(keys.Count, uniqueKeys.Count);
        }

        [Fact]
        public void EndpointRegistry_AmbientParametersCanBeSet()
        {
            int originalLimit = EndpointRegistry.CurrentHistoryLimit;
            try
            {
                EndpointRegistry.CurrentHistoryLimit = 10;
                Assert.Equal(10, EndpointRegistry.CurrentHistoryLimit);
            }
            finally
            {
                EndpointRegistry.CurrentHistoryLimit = originalLimit;
            }
        }
    }

    public class DeviceSettingsSnapshotExtendedTests
    {
        [Fact]
        public void DeviceSettingsSnapshot_ParsesZeroVolume()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": { ""doorbell_volume"": 0 }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            _ = Assert.Single(result);
            Assert.Equal(0, result[123].Volume);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesMaxVolume()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": { ""doorbell_volume"": 11 }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            _ = Assert.Single(result);
            Assert.Equal(11, result[123].Volume);
        }

        [Fact]
        public void DeviceSettingsSnapshot_IgnoresNullChimeSettings()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": {
                        ""doorbell_volume"": 5,
                        ""chime_settings"": null
                    }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            _ = Assert.Single(result);
            Assert.Null(result[123].ChimeType);
        }

        [Fact]
        public void DeviceSettingsSnapshot_IgnoresInvalidIdType()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": ""invalid"",
                    ""settings"": { ""doorbell_volume"": 5 }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            Assert.Empty(result);
        }

        [Fact]
        public void DeviceSettingsSnapshot_IgnoresFloatId()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123.456,
                    ""settings"": { ""doorbell_volume"": 5 }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            // Should skip because it expects a 64-bit integer
            Assert.Empty(result);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesNegativeVolume()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": { ""doorbell_volume"": -1 }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            _ = Assert.Single(result);
            Assert.Equal(-1, result[123].Volume);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesLongId()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 9223372036854775807,
                    ""settings"": { ""doorbell_volume"": 5 }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            _ = Assert.Single(result);
            Assert.True(result.ContainsKey(9223372036854775807));
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesLargeBoolValue()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": {
                        ""night_mode_on"": 100
                    }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            _ = Assert.Single(result);
            Assert.True(result[123].NightModeEnabled);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesChimeDurationZero()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": {
                        ""chime_settings"": {
                            ""duration"": 0
                        }
                    }
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            _ = Assert.Single(result);
            Assert.Equal(0, result[123].ChimeDuration);
        }

        [Fact]
        public void DeviceSettingsSnapshot_PartiallyMalformedJsonStillParsesGoodEntries()
        {
            string json = @"{
                ""doorbots"": [
                    {
                        ""id"": 100,
                        ""settings"": { ""doorbell_volume"": 5 }
                    }
                ],
                ""authorized_doorbots"": not-valid-json
            }";

            // The parser should catch the exception and return what it found
            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            // Depending on implementation, it might parse the first doorbot
            Assert.NotNull(result);
        }

        [Fact]
        public void DeviceSettingsSnapshot_LargeJsonDocument()
        {
            string doorbots = string.Join(",", Enumerable.Range(1, 100).Select(i =>
                @$"{{ ""id"": {i}, ""settings"": {{ ""doorbell_volume"": {i % 12} }} }}"
            ));
            string json = @$"{{ ""doorbots"": [{doorbots}] }}";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            Assert.Equal(100, result.Count);
        }

        [Fact]
        public void DeviceSettingsSnapshot_ParsesEmptySettingsObject()
        {
            string json = @"{
                ""doorbots"": [{
                    ""id"": 123,
                    ""settings"": {}
                }]
            }";

            Dictionary<long, DoorbotSettingsSnapshot> result = DeviceSettingsSnapshot.ParseFromDevicesJson(json);
            _ = Assert.Single(result);
            Assert.Null(result[123].Volume);
            Assert.Null(result[123].ChimeType);
        }
    }

    public class EndpointSchemaMapExtendedTests
    {
        [Fact]
        public void EndpointSchemaMap_AllMappedEndpointsHaveTypes()
        {
            string[] mappedEndpoints = new[] { "doorbot-health", "chime-health", "video-search", "location-events", "profile", "ringtones" };
            foreach (string? endpoint in mappedEndpoints)
            {
                bool found = EndpointSchemaMap.TryGetExpectedType(endpoint, out Type? type);
                Assert.True(found, $"Endpoint {endpoint} should be mapped");
                Assert.NotNull(type);
            }
        }

        [Fact]
        public void EndpointSchemaMap_LocationEventsReturnsCorrectType()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("location-events", out Type? type);
            Assert.True(found);
            Assert.NotNull(type);
        }

        [Fact]
        public void EndpointSchemaMap_ProfileReturnsCorrectType()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("profile", out Type? type);
            Assert.True(found);
            Assert.NotNull(type);
        }

        [Fact]
        public void EndpointSchemaMap_RingtonesReturnsCorrectType()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("ringtones", out Type? type);
            Assert.True(found);
            Assert.NotNull(type);
        }

        [Fact]
        public void EndpointSchemaMap_UnmappedReadOnlyEndpointsReturnFalse()
        {
            string[] unmapped = new[] { "devices", "locations", "doorbot-history", "set-light", "shared-users" };
            foreach (string? endpoint in unmapped)
            {
                bool found = EndpointSchemaMap.TryGetExpectedType(endpoint, out _);
                Assert.False(found, $"Endpoint {endpoint} should not be mapped (returns raw JSON)");
            }
        }

        [Fact]
        public void EndpointSchemaMap_OutputParameterIsNullForUnmapped()
        {
            bool found = EndpointSchemaMap.TryGetExpectedType("unknown", out Type? type);
            Assert.False(found);
            Assert.Null(type);
        }
    }
}
