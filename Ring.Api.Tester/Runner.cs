using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using KoenZomers.Ring.Api;

namespace Ring.Api.Tester
{
    /// <summary>
    /// What to run and how far to let it reach: which endpoint keys were requested, plus the two
    /// safety gates that keep destructive/physical calls opt-in.
    /// </summary>
    internal sealed record RunOptions(
        IReadOnlyList<string> RequestedKeys,
        bool Destructive,
        bool NoPhysical,
        Guid? LocationIdFilter,
        long? DoorbotIdFilter,
        long? ChimeIdFilter);

    /// <summary>
    /// Runs the selected endpoints against an authenticated session, capturing every raw HTTP
    /// call each one makes (via ApiRawLogger) to its own file, and builds index.json describing
    /// the whole run. For destructive calls, also captures a pre-mutation baseline where one is
    /// available and restores it immediately after - see EndpointDescriptor.PrepareRestore.
    /// </summary>
    internal sealed class Runner
    {
        private readonly Session _session;
        private readonly string _outputDir;
        private readonly bool _quiet;
        private int _fileSequence;

        public Runner(Session session, string outputDir, bool quiet)
        {
            _session = session;
            _outputDir = outputDir;
            _quiet = quiet;
        }

        public async Task<IndexDocument> RunAsync(RunOptions options, string credentialSource)
        {
            EndpointRegistry.OriginalDoorbotSettings = new Dictionary<long, DoorbotSettingsSnapshot>();
            EndpointRegistry.OriginalLocationModeByLocation.Clear();

            var index = new IndexDocument
            {
                GeneratedAtUtc = DateTime.UtcNow,
                CredentialSource = credentialSource,
                OutputDirectory = _outputDir
            };

            var selected = ResolveSelection(options.RequestedKeys, options.Destructive, options.NoPhysical);

            // devices/locations are the source of ids for every per-location/per-doorbot/per-chime
            // endpoint below, so they always run first (and only once) whenever anything
            // downstream of them was selected - regardless of whether they were explicitly
            // requested themselves.
            var needsDevices = selected.Any(e => e.Key == "devices") || selected.Any(e => e.Scope is EndpointScope.PerDoorbot or EndpointScope.PerChime);
            var needsLocations = selected.Any(e => e.Key == "locations") || selected.Any(e => e.Scope == EndpointScope.PerLocation);

            KoenZomers.Ring.Api.Entities.Devices? devices = null;
            List<KoenZomers.Ring.Api.Entities.Location>? locations = null;

            if (needsDevices)
            {
                var descriptor = EndpointRegistry.Find("devices")!;
                var (record, result) = await ExecuteAsync(descriptor, EndpointTarget.None, null);
                index.Calls.Add(record);
                devices = result as KoenZomers.Ring.Api.Entities.Devices;

                // Baseline for set-volume/set-chime-type/set-night-mode's restore: parsed from the
                // raw devices response rather than the `devices` entity above, since Doorbot.cs
                // doesn't model the settings sub-object at all (see DeviceSettingsSnapshot).
                if (record.Success && record.ResultFile != null)
                {
                    try
                    {
                        var rawJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, record.ResultFile));
                        EndpointRegistry.OriginalDoorbotSettings = DeviceSettingsSnapshot.ParseFromDevicesJson(rawJson);
                    }
                    catch
                    {
                        // Leave it empty - affected restores are then reported as un-capturable
                        // rather than the whole run failing over a diagnostics-only snapshot.
                    }
                }
            }

            if (needsLocations)
            {
                var descriptor = EndpointRegistry.Find("locations")!;
                var (record, result) = await ExecuteAsync(descriptor, EndpointTarget.None, null);
                index.Calls.Add(record);
                locations = result as List<KoenZomers.Ring.Api.Entities.Location>;
            }

            var locationNames = (locations ?? new()).Where(l => l.Id.HasValue)
                .ToDictionary(l => l.Id!.Value, l => l.Name ?? "(unnamed)");
            var doorbotNames = new Dictionary<long, string>();
            foreach (var d in devices?.Doorbots ?? new()) doorbotNames[d.Id] = d.Description ?? "(unnamed)";
            foreach (var d in devices?.AuthorizedDoorbots ?? new()) doorbotNames.TryAdd(d.Id, d.Description ?? "(unnamed)");
            foreach (var d in devices?.StickupCams ?? new()) if (d.Id.HasValue) doorbotNames.TryAdd(d.Id.Value, d.Description ?? "(unnamed)");
            var chimeNames = new Dictionary<long, string>();
            foreach (var c in devices?.Chimes ?? new()) chimeNames[c.Id] = c.Description ?? "(unnamed)";

            var locationIds = options.LocationIdFilter.HasValue
                ? new List<Guid> { options.LocationIdFilter.Value }
                : (locations ?? new()).Where(l => l.Id.HasValue).Select(l => l.Id!.Value).Distinct().ToList();

            var doorbotIds = options.DoorbotIdFilter.HasValue
                ? new List<long> { options.DoorbotIdFilter.Value }
                : doorbotNames.Keys.ToList();

            var chimeIds = options.ChimeIdFilter.HasValue
                ? new List<long> { options.ChimeIdFilter.Value }
                : chimeNames.Keys.ToList();

            foreach (var descriptor in selected)
            {
                if (descriptor.Key is "devices" or "locations")
                {
                    // Already executed above as a shared prerequisite.
                    continue;
                }

                switch (descriptor.Scope)
                {
                    case EndpointScope.None:
                        {
                            var (record, _) = await ExecuteAsync(descriptor, EndpointTarget.None, null);
                            index.Calls.Add(record);
                            break;
                        }

                    case EndpointScope.PerLocation:
                        if (locationIds.Count == 0)
                        {
                            Narrate($"Skipping {descriptor.Key}: no locations discovered (and no --location-id given).");
                        }
                        foreach (var locId in locationIds)
                        {
                            var target = new EndpointTarget(locId, null);
                            var targetRecord = new TargetRecord { LocationId = locId.ToString(), LocationName = locationNames.GetValueOrDefault(locId) };
                            var (record, _) = await ExecuteAsync(descriptor, target, targetRecord);
                            index.Calls.Add(record);
                        }
                        break;

                    case EndpointScope.PerDoorbot:
                        if (doorbotIds.Count == 0)
                        {
                            Narrate($"Skipping {descriptor.Key}: no doorbots discovered (and no --doorbot-id given).");
                        }
                        foreach (var dbId in doorbotIds)
                        {
                            var target = new EndpointTarget(null, dbId);
                            var targetRecord = new TargetRecord { DoorbotId = dbId, DoorbotName = doorbotNames.GetValueOrDefault(dbId) };
                            var (record, _) = await ExecuteAsync(descriptor, target, targetRecord);
                            index.Calls.Add(record);
                        }
                        break;

                    case EndpointScope.PerChime:
                        if (chimeIds.Count == 0)
                        {
                            Narrate($"Skipping {descriptor.Key}: no chimes discovered (and no --chime-id given).");
                        }
                        foreach (var chId in chimeIds)
                        {
                            var target = new EndpointTarget(null, null, chId);
                            var targetRecord = new TargetRecord { ChimeId = chId, ChimeName = chimeNames.GetValueOrDefault(chId) };
                            var (record, _) = await ExecuteAsync(descriptor, target, targetRecord);
                            index.Calls.Add(record);
                        }
                        break;
                }
            }

            index.Summary.TotalCalls = index.Calls.Count;
            index.Summary.Succeeded = index.Calls.Count(c => c.Success);
            index.Summary.Failed = index.Calls.Count(c => !c.Success);

            return index;
        }

        private static List<EndpointDescriptor> ResolveSelection(IReadOnlyList<string> requestedKeys, bool destructive, bool noPhysical)
        {
            var wantsAll = requestedKeys.Any(k => string.Equals(k, "all", StringComparison.OrdinalIgnoreCase));

            List<EndpointDescriptor> result;
            if (wantsAll)
            {
                result = EndpointRegistry.All.Where(e => destructive || !e.Destructive).ToList();
            }
            else
            {
                result = new List<EndpointDescriptor>();
                foreach (var key in requestedKeys)
                {
                    var descriptor = EndpointRegistry.Find(key);
                    if (descriptor == null)
                    {
                        throw new ArgumentException($"Unknown endpoint key '{key}'. Run --list to see valid keys.");
                    }
                    if (descriptor.Destructive && !destructive)
                    {
                        throw new ArgumentException($"Endpoint '{key}' is destructive and was requested explicitly, but --destructive was not passed. Add --destructive to confirm you want to run it.");
                    }
                    result.Add(descriptor);
                }
            }

            if (noPhysical)
            {
                result = result.Where(e => !e.Physical).ToList();
            }

            return result;
        }

        private async Task<(CallRecord record, object? result)> ExecuteAsync(EndpointDescriptor descriptor, EndpointTarget target, TargetRecord? targetRecord)
        {
            var raw = new List<RawApiCall>();
            void Handler(RawApiCall call) => raw.Add(call);

            ApiRawLogger.OnRawResponse += Handler;

            var record = new CallRecord
            {
                Endpoint = descriptor.Key,
                DisplayName = descriptor.DisplayName,
                SessionMethod = descriptor.SessionMethod,
                Destructive = descriptor.Destructive,
                Physical = descriptor.Physical,
                Target = targetRecord,
                StartedAtUtc = DateTime.UtcNow
            };

            Narrate($"-> {descriptor.Key} {DescribeTarget(targetRecord)}".TrimEnd());

            var sw = Stopwatch.StartNew();
            object? invokeResult = null;
            var testCallCount = 0;
            try
            {
                var task = descriptor.Invoke(_session, target);
                await task;
                testCallCount = raw.Count;

                // Session methods return Task<T>; pull the boxed result off via reflection so the
                // caller (devices/locations) can hand ids to the rest of the run without every
                // Invoke delegate needing a non-generic Task<object> signature.
                var taskType = task.GetType();
                var resultProperty = taskType.GetProperty("Result");
                invokeResult = resultProperty?.GetValue(task);

                record.Success = true;
                Narrate($"   ok ({raw.Sum(r => r.Body?.Length ?? 0)} bytes, {raw.Count} http call(s))");

                // Validate response against declared entity schema
                if (raw.Count > 0 && raw[0].Body != null && EndpointSchemaMap.TryGetExpectedType(descriptor.Key, out var expectedType) && expectedType != null)
                {
                    try
                    {
                        var jsonResponse = JsonDocument.Parse(raw[0].Body).RootElement;
                        var validator = new JsonSchemaValidator();
                        var issues = validator.ValidateAgainstSchema(jsonResponse, expectedType);
                        record.SchemaIssues = issues.ConvertAll(i => new SchemaIssueRecord
                        {
                            Path = i.Path,
                            IssueType = i.IssueType,
                            Expected = i.Expected,
                            Actual = i.Actual,
                            Severity = i.Severity
                        });
                    }
                    catch
                    {
                        // Schema validation is best-effort; don't fail the test if validation itself fails
                    }
                }
            }
            catch (Exception ex)
            {
                testCallCount = raw.Count;
                record.Success = false;
                record.Error = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException?.Message ?? ex.Message : ex.Message;
                Narrate($"   FAILED: {record.Error}");
            }

            // Restore, still inside the same subscription window so its own raw HTTP traffic is
            // captured too (tagged "restore" below, alongside the "test" calls from Invoke).
            if (record.Success && descriptor.Destructive)
            {
                if (descriptor.PrepareRestore == null)
                {
                    record.RestoreSkippedReason = descriptor.NoRestoreReason ?? "not applicable";
                }
                else
                {
                    var plan = descriptor.PrepareRestore(target);
                    if (plan == null)
                    {
                        record.RestoreSkippedReason = "no original value could be captured for this target - the mutated value was left as set by --" +
                            (descriptor.Key switch
                            {
                                "set-volume" => "volume-level",
                                "set-chime-type" => "chime-type-value",
                                _ => "the value flag used"
                            });
                    }
                    else
                    {
                        record.OriginalValue = plan.OriginalValueDescription;
                        record.RestoreAttempted = true;
                        try
                        {
                            await plan.Restore(_session);
                            record.RestoreSuccess = true;
                            Narrate($"   restored ({plan.OriginalValueDescription})");
                        }
                        catch (Exception ex)
                        {
                            record.RestoreSuccess = false;
                            record.RestoreError = ex is System.Reflection.TargetInvocationException tie2 ? tie2.InnerException?.Message ?? ex.Message : ex.Message;
                            Narrate($"   RESTORE FAILED: {record.RestoreError} - device may be left with the test value ({plan.OriginalValueDescription} was the target to restore to)");
                        }
                    }
                }
            }

            ApiRawLogger.OnRawResponse -= Handler;
            sw.Stop();
            record.DurationMs = sw.Elapsed.TotalMilliseconds;

            string? primaryResultFile = null;
            for (var i = 0; i < raw.Count; i++)
            {
                var call = raw[i];
                var phase = i < testCallCount ? "test" : "restore";
                var fileName = BuildFileName(descriptor.Key, targetRecord, raw.Count > 1, phase);
                var filePath = Path.Combine(_outputDir, fileName);
                await File.WriteAllTextAsync(filePath, call.Body ?? string.Empty);

                record.HttpCalls.Add(new HttpCallRecord
                {
                    Method = call.Method,
                    Url = call.Url,
                    StatusCode = call.StatusCode,
                    ResponseBodyBytes = call.Body?.Length ?? 0,
                    BodyFile = fileName,
                    Phase = phase
                });

                primaryResultFile ??= fileName;
            }
            record.ResultFile = primaryResultFile;

            return (record, invokeResult);
        }

        private static string DescribeTarget(TargetRecord? t)
        {
            if (t == null) return "";
            if (t.LocationId != null) return $"(location {t.LocationName ?? t.LocationId})";
            if (t.DoorbotId != null) return $"(doorbot {t.DoorbotName ?? t.DoorbotId.ToString()})";
            if (t.ChimeId != null) return $"(chime {t.ChimeName ?? t.ChimeId.ToString()})";
            return "";
        }

        private string BuildFileName(string endpointKey, TargetRecord? target, bool multipleCalls, string phase)
        {
            var targetPart = target switch
            {
                { LocationId: not null } => "_" + target.LocationId!.Substring(0, Math.Min(8, target.LocationId.Length)),
                { DoorbotId: not null } => "_" + target.DoorbotId,
                { ChimeId: not null } => "_" + target.ChimeId,
                _ => ""
            };
            var phaseSuffix = phase == "restore" ? "_restore" : "";
            var seq = multipleCalls ? $"_{++_fileSequence}" : "";
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfffZ");
            return $"{endpointKey}{targetPart}{phaseSuffix}_{timestamp}{seq}.json";
        }

        private void Narrate(string message)
        {
            if (!_quiet) Console.WriteLine(message);
        }
    }
}
