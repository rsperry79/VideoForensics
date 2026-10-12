using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// What to run and how far to let it reach: which endpoint keys were requested, plus the two
    /// safety gates that keep destructive/physical calls opt-in.
    /// </summary>
    public sealed record RunOptions(
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
    public sealed class Runner
    {
        private readonly Session _session;
        private readonly string _outputDir;
        private readonly bool _quiet;
        private int _fileSequence;

        public Runner(Session session, string outputDir, bool quiet)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _outputDir = outputDir;
            _quiet = quiet;
        }

        public async Task<IndexDocument> RunAsync(RunOptions options, string credentialSource)
        {
            EndpointRegistry.OriginalDoorbotSettings = [];
            EndpointRegistry.OriginalLocationModeByLocation.Clear();

            var index = new IndexDocument
            {
                GeneratedAtUtc = DateTime.UtcNow,
                CredentialSource = credentialSource,
                OutputDirectory = _outputDir
            };

            List<EndpointDescriptor> selected = ResolveSelection(options.RequestedKeys, options.Destructive, options.NoPhysical);

            bool needsDevices = selected.Any(e => e.Key == "devices") || selected.Any(e => e.Scope is EndpointScope.PerDoorbot or EndpointScope.PerChime);
            bool needsLocations = selected.Any(e => e.Key == "locations") || selected.Any(e => e.Scope == EndpointScope.PerLocation);

            Devices? devices = null;
            List<Location>? locations = null;

            if (needsDevices)
            {
                EndpointDescriptor descriptor = EndpointRegistry.Find("devices")!;
                (CallRecord? record, object? result) = await ExecuteAsync(descriptor, EndpointTarget.None, null);
                index.Calls.Add(record);
                devices = result as Devices;

                if (record.Success && record.ResultFile != null)
                {
                    try
                    {
                        string rawJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, record.ResultFile));
                        EndpointRegistry.OriginalDoorbotSettings = DeviceSettingsSnapshot.ParseFromDevicesJson(rawJson);
                    }
                    catch
                    {
                    }
                }
            }

            if (needsLocations)
            {
                EndpointDescriptor descriptor = EndpointRegistry.Find("locations")!;
                (CallRecord? record, object? result) = await ExecuteAsync(descriptor, EndpointTarget.None, null);
                index.Calls.Add(record);
                locations = result as List<Location>;
            }

            var locationNames = (locations ?? []).Where(l => l.Id.HasValue)
                .ToDictionary(l => l.Id!.Value, l => l.Name ?? "(unnamed)");
            var doorbotNames = new Dictionary<long, string>();
            foreach (Doorbot d in devices?.Doorbots ?? [])
            {
                doorbotNames[d.Id] = d.Description ?? "(unnamed)";
            }

            foreach (Doorbot d in devices?.AuthorizedDoorbots ?? [])
            {
                _ = doorbotNames.TryAdd(d.Id, d.Description ?? "(unnamed)");
            }

            foreach (StickupCam d in devices?.StickupCams ?? [])
            {
                if (d.Id.HasValue)
                {
                    _ = doorbotNames.TryAdd(d.Id.Value, d.Description ?? "(unnamed)");
                }
            }

            var chimeNames = new Dictionary<long, string>();
            foreach (Chime c in devices?.Chimes ?? [])
            {
                chimeNames[c.Id] = c.Description ?? "(unnamed)";
            }

            List<Guid> locationIds = options.LocationIdFilter.HasValue
                ? [options.LocationIdFilter.Value]
                : (locations ?? []).Where(l => l.Id.HasValue).Select(l => l.Id!.Value).Distinct().ToList();

            List<long> doorbotIds = options.DoorbotIdFilter.HasValue
                ? [options.DoorbotIdFilter.Value]
                : doorbotNames.Keys.ToList();

            List<long> chimeIds = options.ChimeIdFilter.HasValue
                ? [options.ChimeIdFilter.Value]
                : chimeNames.Keys.ToList();

            foreach (EndpointDescriptor descriptor in selected)
            {
                if (descriptor.Key is "devices" or "locations")
                {
                    continue;
                }

                switch (descriptor.Scope)
                {
                    case EndpointScope.None:
                    {
                        (CallRecord? record, _) = await ExecuteAsync(descriptor, EndpointTarget.None, null);
                        index.Calls.Add(record);
                        break;
                    }

                    case EndpointScope.PerLocation:
                        if (locationIds.Count == 0)
                        {
                            Narrate($"Skipping {descriptor.Key}: no locations discovered (and no --location-id given).");
                        }

                        foreach (Guid locId in locationIds)
                        {
                            var target = new EndpointTarget(locId, null);
                            var targetRecord = new TargetRecord { LocationId = locId.ToString(), LocationName = locationNames.GetValueOrDefault(locId) };
                            (CallRecord? record, _) = await ExecuteAsync(descriptor, target, targetRecord);
                            index.Calls.Add(record);
                        }

                        break;

                    case EndpointScope.PerDoorbot:
                        if (doorbotIds.Count == 0)
                        {
                            Narrate($"Skipping {descriptor.Key}: no doorbots discovered (and no --doorbot-id given).");
                        }

                        foreach (long dbId in doorbotIds)
                        {
                            var target = new EndpointTarget(null, dbId);
                            var targetRecord = new TargetRecord { DoorbotId = dbId, DoorbotName = doorbotNames.GetValueOrDefault(dbId) };
                            (CallRecord? record, _) = await ExecuteAsync(descriptor, target, targetRecord);
                            index.Calls.Add(record);
                        }

                        break;

                    case EndpointScope.PerChime:
                        if (chimeIds.Count == 0)
                        {
                            Narrate($"Skipping {descriptor.Key}: no chimes discovered (and no --chime-id given).");
                        }

                        foreach (long chId in chimeIds)
                        {
                            var target = new EndpointTarget(null, null, chId);
                            var targetRecord = new TargetRecord { ChimeId = chId, ChimeName = chimeNames.GetValueOrDefault(chId) };
                            (CallRecord? record, _) = await ExecuteAsync(descriptor, target, targetRecord);
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
            bool wantsAll = requestedKeys.Any(k => string.Equals(k, "all", StringComparison.OrdinalIgnoreCase));

            List<EndpointDescriptor> result;
            if (wantsAll)
            {
                result = EndpointRegistry.All.Where(e => destructive || !e.Destructive).ToList();
            }
            else
            {
                result = [];
                foreach (string key in requestedKeys)
                {
                    EndpointDescriptor? descriptor = EndpointRegistry.Find(key);
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
            void Handler(RawApiCall call)
            {
                raw.Add(call);
            }

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
            int testCallCount = 0;
            try
            {
                Task task = descriptor.Invoke(_session, target);
                await task;
                testCallCount = raw.Count;

                Type taskType = task.GetType();
                PropertyInfo? resultProperty = taskType.GetProperty("Result");
                invokeResult = resultProperty?.GetValue(task);

                record.Success = true;
                Narrate($"   ok ({raw.Sum(r => r.Body?.Length ?? 0)} bytes, {raw.Count} http call(s))");

                if (raw.Count > 0 && raw[0].Body != null && EndpointSchemaMap.TryGetExpectedType(descriptor.Key, out Type? expectedType) && expectedType != null)
                {
                    try
                    {
                        JsonElement jsonResponse = JsonDocument.Parse(raw[0].Body).RootElement;
                        var validator = new JsonSchemaValidator();
                        List<JsonSchemaValidator.SchemaIssue> issues = validator.ValidateAgainstSchema(jsonResponse, expectedType);
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

            if (record.Success && descriptor.Destructive)
            {
                if (descriptor.PrepareRestore == null)
                {
                    record.RestoreSkippedReason = descriptor.NoRestoreReason ?? "not applicable";
                }
                else
                {
                    RestorePlan? plan = descriptor.PrepareRestore(target);
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
            for (int i = 0; i < raw.Count; i++)
            {
                RawApiCall call = raw[i];
                string phase = i < testCallCount ? "test" : "restore";
                string fileName = BuildFileName(descriptor.Key, targetRecord, raw.Count > 1, phase);
                string filePath = Path.Combine(_outputDir, fileName);
                await File.WriteAllTextAsync(filePath, call.Body ?? string.Empty);

                record.HttpCalls.Add(new HttpCallRecord
                {
                    Method = call.Method,
                    Url = call.Url,
                    StatusCode = call.StatusCode,
                    ResponseBodyBytes = call.Body?.Length ?? 0,
                    BodyFile = fileName,
                    Phase = phase,
                    Body = call.Body,
                    TimestampUtc = call.Timestamp
                });

                primaryResultFile ??= fileName;
            }

            record.ResultFile = primaryResultFile;

            return (record, invokeResult);
        }

        private static string DescribeTarget(TargetRecord? t)
        {
            return t == null
                ? ""
                : t.LocationId != null
                ? $"(location {t.LocationName ?? t.LocationId})"
                : t.DoorbotId != null
                ? $"(doorbot {t.DoorbotName ?? t.DoorbotId.ToString()})"
                : t.ChimeId != null ? $"(chime {t.ChimeName ?? t.ChimeId.ToString()})" : "";
        }

        private string BuildFileName(string endpointKey, TargetRecord? target, bool multipleCalls, string phase)
        {
            string targetPart = target switch
            {
                { LocationId: not null } => "_" + target.LocationId![..Math.Min(8, target.LocationId.Length)],
                { DoorbotId: not null } => "_" + target.DoorbotId,
                { ChimeId: not null } => "_" + target.ChimeId,
                _ => ""
            };
            string phaseSuffix = phase == "restore" ? "_restore" : "";
            string seq = multipleCalls ? $"_{++_fileSequence}" : "";
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfffZ");
            return $"{endpointKey}{targetPart}{phaseSuffix}_{timestamp}{seq}.json";
        }

        private void Narrate(string message)
        {
            if (!_quiet)
            {
                Console.WriteLine(message);
            }
        }
    }
}

